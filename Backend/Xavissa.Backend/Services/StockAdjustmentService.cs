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
