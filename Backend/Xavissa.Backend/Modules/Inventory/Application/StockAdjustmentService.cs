using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class StockAdjustmentService
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;

    public StockAdjustmentService(XavissaDbContext db, TenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<StockAdjustment> CreateAsync(StockAdjustmentCreateDto request)
    {
        var userId = RequireUser();
        if (request.Items.Count == 0 || request.Items.Any(x => x.VariantId <= 0 || x.NewQuantity < 0))
            throw new ArgumentException("Adjustment items must contain valid variants and non-negative new quantities.");

        var store = await RequireManageableStoreAsync(request.StoreId);
        var variantIds = request.Items.Select(x => x.VariantId).Distinct().ToList();
        var found = await _db.ProductVariants.IgnoreQueryFilters().CountAsync(x => x.TenantId == store.TenantId && x.IsActive && variantIds.Contains(x.Id));
        if (found != variantIds.Count)
            throw new ArgumentException("One or more variants are not active in this tenant.");

        var current = await _db.StockLevels.IgnoreQueryFilters()
            .Where(x => x.TenantId == store.TenantId && x.StoreId == request.StoreId && variantIds.Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId);

        var adjustment = new StockAdjustment
        {
            TenantId = store.TenantId,
            StoreId = request.StoreId,
            AdjustmentNumber = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
            Reason = request.Reason.Trim(),
            Notes = request.Notes,
            Status = "Draft",
            CreatedBy = userId,
            UpdatedBy = userId,
            Items = request.Items.Select(item =>
            {
                var oldQty = current.TryGetValue(item.VariantId, out var level) ? level.QuantityOnHand : 0;
                return new StockAdjustmentItem
                {
                    VariantId = item.VariantId,
                    OldQuantity = oldQty,
                    NewQuantity = item.NewQuantity,
                    DifferenceQuantity = item.NewQuantity - oldQty,
                    Reason = item.Reason,
                    Notes = item.Notes,
                };
            }).ToList(),
        };

        _db.StockAdjustments.Add(adjustment);
        await _db.SaveChangesAsync();
        return adjustment;
    }

    public Task<List<StockAdjustment>> ListAsync() =>
        _db.StockAdjustments.Include(x => x.Items).OrderByDescending(x => x.CreatedAt).ToListAsync();

    public async Task<StockAdjustment> ApplySyncedAsync(StockAdjustmentSyncRequestDto request)
    {
        if (request.SyncId == Guid.Empty)
            throw new ArgumentException("A sync id is required for an offline stock adjustment.");
        if (request.Items.Count == 0 || request.Items.Any(item => item.VariantId <= 0 || item.NewQuantity < 0))
            throw new ArgumentException("Adjustment items must contain valid variants and non-negative new quantities.");

        var userId = RequireUser();
        var store = await RequireManageableStoreAsync(request.StoreId);
        if (store.TenantId != request.TenantId)
            throw new UnauthorizedAccessException("The stock adjustment tenant does not match the store.");

        var existing = await _db.StockAdjustments
            .IgnoreQueryFilters()
            .Include(adjustment => adjustment.Items)
            .FirstOrDefaultAsync(adjustment =>
                adjustment.SyncId == request.SyncId
                && adjustment.TenantId == request.TenantId
                && adjustment.StoreId == request.StoreId);
        if (existing != null)
            return existing;

        var lines = request.Items
            .GroupBy(item => item.VariantId)
            .Select(group => group.Last())
            .ToList();
        var variantIds = lines.Select(item => item.VariantId).ToList();
        var found = await _db.ProductVariants
            .IgnoreQueryFilters()
            .CountAsync(variant =>
                variant.TenantId == request.TenantId
                && variant.IsActive
                && variantIds.Contains(variant.Id));
        if (found != variantIds.Count)
            throw new ArgumentException("One or more variants are not active in this tenant.");

        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var currentLevels = await _db.StockLevels
            .IgnoreQueryFilters()
            .Where(level =>
                level.TenantId == request.TenantId
                && level.StoreId == request.StoreId
                && variantIds.Contains(level.VariantId))
            .ToDictionaryAsync(level => level.VariantId);

        var adjustment = new StockAdjustment
        {
            SyncId = request.SyncId,
            SourceDeviceId = request.SourceDeviceId,
            ClientCreatedAt = request.ClientCreatedAt,
            ClientUpdatedAt = request.ClientUpdatedAt,
            LastSyncedAt = now,
            TenantId = request.TenantId,
            StoreId = request.StoreId,
            AdjustmentNumber = $"ADJ-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
            Reason = request.Reason.Trim(),
            Status = "Applied",
            CreatedBy = userId,
            UpdatedBy = userId,
            AppliedBy = userId,
            AppliedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = lines.Select(line =>
            {
                var oldQuantity = currentLevels.TryGetValue(line.VariantId, out var level)
                    ? level.QuantityOnHand
                    : 0;
                var offlineDifference = line.NewQuantity - line.OldQuantity;
                var mergedQuantity = oldQuantity + offlineDifference;
                if (mergedQuantity < 0)
                    throw new InvalidOperationException(
                        $"Stock adjustment for variant {line.VariantId} conflicts with newer server stock.");
                return new StockAdjustmentItem
                {
                    SyncId = line.SyncId == Guid.Empty ? Guid.NewGuid() : line.SyncId,
                    SourceDeviceId = request.SourceDeviceId,
                    ClientCreatedAt = request.ClientCreatedAt,
                    ClientUpdatedAt = request.ClientUpdatedAt,
                    LastSyncedAt = now,
                    VariantId = line.VariantId,
                    OldQuantity = oldQuantity,
                    // Offline adjustments merge as movements, not absolute overwrites.
                    // This preserves sales that another device uploaded meanwhile.
                    NewQuantity = mergedQuantity,
                    DifferenceQuantity = offlineDifference,
                    Reason = line.Reason,
                    Notes = line.Notes,
                };
            }).ToList(),
        };

        _db.StockAdjustments.Add(adjustment);
        await _db.SaveChangesAsync();

        foreach (var item in adjustment.Items)
        {
            if (currentLevels.TryGetValue(item.VariantId, out var level))
            {
                level.QuantityOnHand = item.NewQuantity;
                level.UpdatedAt = now;
                level.UpdatedBy = userId;
            }
            else
            {
                _db.StockLevels.Add(new StockLevel
                {
                    TenantId = request.TenantId,
                    StoreId = request.StoreId,
                    VariantId = item.VariantId,
                    QuantityOnHand = item.NewQuantity,
                    UpdatedAt = now,
                    UpdatedBy = userId,
                });
            }

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = request.TenantId,
                StoreId = request.StoreId,
                VariantId = item.VariantId,
                Quantity = item.DifferenceQuantity,
                MovementType = "Adjustment",
                ReferenceType = "StockAdjustment",
                ReferenceId = adjustment.Id,
                Notes = item.Reason ?? adjustment.Reason,
                CreatedAt = now,
                CreatedBy = userId,
            });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return adjustment;
    }

    public Task<StockAdjustment?> GetAsync(int id) =>
        _db.StockAdjustments.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<StockAdjustment> ApproveAsync(int id)
    {
        var adjustment = await RequireAdjustmentAsync(id);
        await RequireManageableStoreAsync(adjustment.StoreId);
        EnsureStatus(adjustment, "Draft");
        adjustment.Status = "Approved";
        adjustment.ApprovedBy = RequireUser();
        adjustment.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return adjustment;
    }

    public async Task<StockAdjustment> ApplyAsync(int id)
    {
        var userId = RequireUser();
        var adjustment = await RequireAdjustmentAsync(id);
        await RequireManageableStoreAsync(adjustment.StoreId);
        EnsureStatus(adjustment, "Approved");

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var item in adjustment.Items)
        {
            if (item.NewQuantity < 0)
                throw new InvalidOperationException("Negative stock is not allowed.");

            var updated = await _db.StockLevels.IgnoreQueryFilters()
                .Where(x => x.TenantId == adjustment.TenantId && x.StoreId == adjustment.StoreId && x.VariantId == item.VariantId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.QuantityOnHand, item.NewQuantity)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedBy, userId));
            if (updated == 0)
            {
                _db.StockLevels.Add(new StockLevel
                {
                    TenantId = adjustment.TenantId,
                    StoreId = adjustment.StoreId,
                    VariantId = item.VariantId,
                    QuantityOnHand = item.NewQuantity,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId,
                });
            }

            item.DifferenceQuantity = item.NewQuantity - item.OldQuantity;
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = adjustment.TenantId,
                StoreId = adjustment.StoreId,
                VariantId = item.VariantId,
                Quantity = item.DifferenceQuantity,
                MovementType = "Adjustment",
                ReferenceType = "StockAdjustment",
                ReferenceId = adjustment.Id,
                Notes = item.Reason ?? adjustment.Reason,
                CreatedBy = userId,
            });
        }

        adjustment.Status = "Applied";
        adjustment.AppliedBy = userId;
        adjustment.AppliedAt = DateTime.UtcNow;
        adjustment.UpdatedAt = DateTime.UtcNow;
        adjustment.UpdatedBy = userId;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return adjustment;
    }

    public async Task<StockAdjustment> CancelAsync(int id)
    {
        var adjustment = await RequireAdjustmentAsync(id);
        await RequireManageableStoreAsync(adjustment.StoreId);
        EnsureStatus(adjustment, "Draft", "Approved");
        adjustment.Status = "Cancelled";
        adjustment.CancelledBy = RequireUser();
        adjustment.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return adjustment;
    }

    private async Task<Store> RequireManageableStoreAsync(int storeId)
    {
        var store = await _db.Stores.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == storeId)
            ?? throw new ArgumentException("Store was not found.");
        if (!store.TenantId.HasValue)
            throw new InvalidOperationException("Store tenant is missing.");
        if (_tenantAccess.IsPlatformAdmin || _tenantAccess.IsSupport || _tenantAccess.CanManageTenant(store.TenantId.Value) || _tenantAccess.CanManageStore(storeId))
            return store;
        throw new UnauthorizedAccessException("Unauthorized stock adjustment operation.");
    }

    private async Task<StockAdjustment> RequireAdjustmentAsync(int id) =>
        await _db.StockAdjustments.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new KeyNotFoundException("Stock adjustment was not found.");

    private int RequireUser() =>
        _tenantAccess.CurrentUserId ?? throw new UnauthorizedAccessException("Invalid user claim.");

    private static void EnsureStatus(StockAdjustment adjustment, params string[] allowed)
    {
        if (!allowed.Contains(adjustment.Status, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Invalid status transition from {adjustment.Status}.");
    }
}
