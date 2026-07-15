using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Services;

public sealed class StockAdjustmentService : IStockAdjustmentService, IStockAdjustmentSyncService
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;
    private readonly IBackgroundSyncService _backgroundSync;
    private readonly IConnectivityService _connectivity;
    private readonly IApiTokenProvider _tokens;
    private readonly IHttpClientFactory _httpClientFactory;

    public StockAdjustmentService(
        IDbContextFactory<LocalDbContext> dbFactory,
        IBackgroundSyncService backgroundSync,
        IConnectivityService connectivity,
        IApiTokenProvider tokens,
        IHttpClientFactory httpClientFactory)
    {
        _dbFactory = dbFactory;
        _backgroundSync = backgroundSync;
        _connectivity = connectivity;
        _tokens = tokens;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<StockAdjustment> ApplyLocalAsync(
        int tenantId,
        int storeId,
        string reason,
        IEnumerable<LocalStockAdjustmentLine> items)
    {
        var lines = items
            .Where(item => item.VariantId > 0 && item.NewQuantity >= 0)
            .GroupBy(item => item.VariantId)
            .Select(group => group.Last())
            .ToList();
        if (tenantId <= 0 || storeId <= 0 || lines.Count == 0)
            throw new ArgumentException("A tenant, store, and at least one valid stock adjustment line are required.");

        await using var db = _dbFactory.CreateDbContext();
        await db.EnsureLocalSchemaAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var variantIds = lines.Select(item => item.VariantId).ToList();
        var snapshots = await db.SellableVariants
            .Where(variant => variant.StoreId == storeId && variantIds.Contains(variant.VariantId))
            .ToDictionaryAsync(variant => variant.VariantId);
        if (snapshots.Count != variantIds.Count)
            throw new InvalidOperationException("One or more variants are missing from the local store cache.");

        var now = DateTime.UtcNow;
        var adjustment = new StockAdjustment
        {
            TenantId = tenantId,
            StoreId = storeId,
            AdjustmentNumber = $"LOCAL-ADJ-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
            Reason = reason.Trim(),
            Status = "Applied",
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var line in lines)
        {
            var snapshot = snapshots[line.VariantId];
            var oldQuantity = snapshot.QuantityOnHand;
            snapshot.QuantityOnHand = line.NewQuantity;
            snapshot.UpdatedAt = now;
            adjustment.Items.Add(new StockAdjustmentItem
            {
                VariantId = line.VariantId,
                OldQuantity = oldQuantity,
                NewQuantity = line.NewQuantity,
                DifferenceQuantity = line.NewQuantity - oldQuantity,
                Reason = line.Reason,
                Notes = line.Notes,
            });
        }

        var productVariants = await db.ProductVariants
            .Where(variant => variant.StoreId == storeId && variantIds.Contains(variant.Id))
            .ToListAsync();
        foreach (var variant in productVariants)
            variant.StockQuantity = snapshots[variant.Id].QuantityOnHand;

        db.StockAdjustments.Add(adjustment);
        await db.SaveChangesAsync();

        db.StockMovements.AddRange(adjustment.Items.Select(item => new StockMovement
        {
            TenantId = tenantId,
            StoreId = storeId,
            VariantId = item.VariantId,
            Quantity = item.DifferenceQuantity,
            MovementType = "Adjustment",
            ReferenceType = "StockAdjustment",
            ReferenceId = adjustment.Id,
            Notes = item.Reason ?? adjustment.Reason,
            CreatedAt = now,
        }));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        _backgroundSync.RequestSync(BackgroundSyncReason.StockAdjusted);
        return adjustment;
    }

    public async Task SyncPendingAsync()
    {
        if (!_connectivity.IsOnline() || string.IsNullOrWhiteSpace(_tokens.Token))
            return;

        await using var db = _dbFactory.CreateDbContext();
        await db.EnsureLocalSchemaAsync();
        var pending = await db.StockAdjustments
            .Include(adjustment => adjustment.Items)
            .Where(adjustment => adjustment.OnlineId == 0 && adjustment.LastSyncedAt == null && adjustment.Status == "Applied")
            .OrderBy(adjustment => adjustment.Id)
            .ToListAsync();
        if (pending.Count == 0)
            return;

        var client = _httpClientFactory.CreateClient("backend");
        foreach (var adjustment in pending)
        {
            var response = await client.PostAsJsonAsync(
                "api/stock-adjustments/sync-apply",
                new StockAdjustmentSyncRequestDto
                {
                    SyncId = adjustment.SyncId,
                    SourceDeviceId = adjustment.SourceDeviceId,
                    ClientCreatedAt = adjustment.ClientCreatedAt,
                    ClientUpdatedAt = adjustment.ClientUpdatedAt,
                    TenantId = adjustment.TenantId,
                    StoreId = adjustment.StoreId,
                    Reason = adjustment.Reason,
                    Items = adjustment.Items.Select(item => new StockAdjustmentItemSyncDto
                    {
                        SyncId = item.SyncId,
                        VariantId = item.VariantId,
                        OldQuantity = item.OldQuantity,
                        NewQuantity = item.NewQuantity,
                        Reason = item.Reason,
                        Notes = item.Notes,
                    }).ToList(),
                });
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StockAdjustmentSyncResultDto>()
                ?? throw new InvalidOperationException("The stock adjustment sync response was empty.");

            adjustment.OnlineId = result.Id;
            adjustment.LastSyncedAt = DateTimeOffset.UtcNow;
            foreach (var item in adjustment.Items)
                item.LastSyncedAt = adjustment.LastSyncedAt;
            await db.SaveChangesAsync();
        }
    }
}
