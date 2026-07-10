using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class StockTransferService
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;

    public StockTransferService(XavissaDbContext db, TenantAccessService tenantAccess)
    {
        _db = db;
        _tenantAccess = tenantAccess;
    }

    public async Task<StockTransfer> CreateAsync(StockTransferCreateDto request)
    {
        var userId = RequireUser();
        if (request.FromStoreId == request.ToStoreId)
            throw new ArgumentException("FromStoreId and ToStoreId must be different.");
        if (request.Items.Count == 0 || request.Items.Any(x => x.VariantId <= 0 || x.QuantityRequested <= 0))
            throw new ArgumentException("Transfer items must contain positive variants and quantities.");

        var (tenantId, _, _) = await ValidateStoresAsync(request.FromStoreId, request.ToStoreId);
        EnsureCanManage(request.FromStoreId, tenantId);
        EnsureCanManage(request.ToStoreId, tenantId);

        var transfer = new StockTransfer
        {
            TenantId = tenantId,
            TransferNumber = $"TR-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
            FromStoreId = request.FromStoreId,
            ToStoreId = request.ToStoreId,
            Status = "Draft",
            RequestedBy = userId,
            Notes = request.Notes,
            CreatedBy = userId,
            UpdatedBy = userId,
            Items = request.Items.Select(item => new StockTransferItem
            {
                VariantId = item.VariantId,
                QuantityRequested = item.QuantityRequested,
                QuantityApproved = item.QuantityApproved,
                Notes = item.Notes,
            }).ToList(),
        };

        await ValidateVariantsAsync(tenantId, transfer.Items.Select(x => x.VariantId));
        _db.StockTransfers.Add(transfer);
        await _db.SaveChangesAsync();
        return transfer;
    }

    public Task<List<StockTransfer>> ListAsync()
    {
        var query = _db.StockTransfers
            .Include(x => x.Items)
            .AsQueryable();
        return query.OrderByDescending(x => x.RequestedAt).ToListAsync();
    }

    public Task<StockTransfer?> GetAsync(int id) =>
        _db.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<StockTransfer> ApproveAsync(int id)
    {
        var transfer = await RequireTransferAsync(id);
        EnsureCanManageTransfer(transfer);
        EnsureStatus(transfer, "Draft", "Pending");
        transfer.Status = "Approved";
        transfer.ApprovedBy = RequireUser();
        transfer.ApprovedAt = DateTime.UtcNow;
        foreach (var item in transfer.Items)
            item.QuantityApproved ??= item.QuantityRequested;
        await _db.SaveChangesAsync();
        return transfer;
    }

    public async Task<StockTransfer> ShipAsync(int id)
    {
        var userId = RequireUser();
        var transfer = await RequireTransferAsync(id);
        EnsureCanManageTransfer(transfer);
        EnsureStatus(transfer, "Approved");

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var item in transfer.Items)
        {
            var qty = item.QuantityApproved ?? item.QuantityRequested;
            var updated = await _db.StockLevels.IgnoreQueryFilters()
                .Where(x => x.TenantId == transfer.TenantId
                    && x.StoreId == transfer.FromStoreId
                    && x.VariantId == item.VariantId
                    && x.QuantityOnHand >= qty)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand - qty)
                    .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                    .SetProperty(x => x.UpdatedBy, userId));
            if (updated == 0)
                throw new InvalidOperationException($"Insufficient source stock for variant {item.VariantId}.");

            item.QuantitySent = qty;
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = transfer.TenantId,
                StoreId = transfer.FromStoreId,
                VariantId = item.VariantId,
                Quantity = -qty,
                MovementType = "TransferOut",
                ReferenceType = "StockTransfer",
                ReferenceId = transfer.Id,
                CreatedBy = userId,
            });
        }

        transfer.Status = "Shipped";
        transfer.SentBy = userId;
        transfer.SentAt = DateTime.UtcNow;
        transfer.UpdatedBy = userId;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return transfer;
    }

    public async Task<StockTransfer> ReceiveAsync(int id)
    {
        var userId = RequireUser();
        var transfer = await RequireTransferAsync(id);
        EnsureCanManageTransfer(transfer);
        EnsureStatus(transfer, "Shipped");

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var item in transfer.Items)
        {
            var qty = item.QuantitySent ?? item.QuantityApproved ?? item.QuantityRequested;
            await UpsertDestinationStockAsync(transfer.TenantId!.Value, transfer.ToStoreId, item.VariantId, qty, userId);
            item.QuantityReceived = qty;
            _db.StockMovements.Add(new StockMovement
            {
                TenantId = transfer.TenantId,
                StoreId = transfer.ToStoreId,
                VariantId = item.VariantId,
                Quantity = qty,
                MovementType = "TransferIn",
                ReferenceType = "StockTransfer",
                ReferenceId = transfer.Id,
                CreatedBy = userId,
            });
        }

        transfer.Status = "Received";
        transfer.ReceivedBy = userId;
        transfer.ReceivedAt = DateTime.UtcNow;
        transfer.UpdatedBy = userId;
        transfer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return transfer;
    }

    public async Task<StockTransfer> CancelAsync(int id)
    {
        var transfer = await RequireTransferAsync(id);
        EnsureCanManageTransfer(transfer);
        if (transfer.Status is "Shipped" or "Received" or "Cancelled")
            throw new InvalidOperationException("This transfer cannot be cancelled in its current status.");
        transfer.Status = "Cancelled";
        transfer.CancelledBy = RequireUser();
        transfer.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return transfer;
    }

    private async Task<(int TenantId, Store From, Store To)> ValidateStoresAsync(int fromStoreId, int toStoreId)
    {
        var stores = await _db.Stores.IgnoreQueryFilters().Where(x => x.Id == fromStoreId || x.Id == toStoreId).ToListAsync();
        var from = stores.FirstOrDefault(x => x.Id == fromStoreId) ?? throw new ArgumentException("Source store was not found.");
        var to = stores.FirstOrDefault(x => x.Id == toStoreId) ?? throw new ArgumentException("Destination store was not found.");
        if (!from.TenantId.HasValue || from.TenantId != to.TenantId)
            throw new ArgumentException("Both stores must belong to the same tenant.");
        return (from.TenantId.Value, from, to);
    }

    private async Task ValidateVariantsAsync(int tenantId, IEnumerable<int> variantIds)
    {
        var ids = variantIds.Distinct().ToList();
        var found = await _db.ProductVariants.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && ids.Contains(x.Id) && x.IsActive);
        if (found != ids.Count)
            throw new ArgumentException("One or more variants are not active in this tenant.");
    }

    private async Task UpsertDestinationStockAsync(int tenantId, int storeId, int variantId, int quantity, int userId)
    {
        var updated = await _db.StockLevels.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.StoreId == storeId && x.VariantId == variantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.QuantityOnHand, x => x.QuantityOnHand + quantity)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow)
                .SetProperty(x => x.UpdatedBy, userId));
        if (updated == 0)
        {
            _db.StockLevels.Add(new StockLevel
            {
                TenantId = tenantId,
                StoreId = storeId,
                VariantId = variantId,
                QuantityOnHand = quantity,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId,
            });
        }
    }

    private async Task<StockTransfer> RequireTransferAsync(int id) =>
        await _db.StockTransfers.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id)
        ?? throw new KeyNotFoundException("Stock transfer was not found.");

    private void EnsureCanManageTransfer(StockTransfer transfer)
    {
        if (!transfer.TenantId.HasValue)
            throw new InvalidOperationException("Transfer tenant is missing.");
        EnsureCanManage(transfer.FromStoreId, transfer.TenantId.Value);
        EnsureCanManage(transfer.ToStoreId, transfer.TenantId.Value);
    }

    private void EnsureCanManage(int storeId, int tenantId)
    {
        if (_tenantAccess.IsPlatformAdmin || _tenantAccess.IsSupport || _tenantAccess.CanManageTenant(tenantId) || _tenantAccess.CanManageStore(storeId))
            return;
        throw new UnauthorizedAccessException("Unauthorized stock transfer operation.");
    }

    private int RequireUser() =>
        _tenantAccess.CurrentUserId ?? throw new UnauthorizedAccessException("Invalid user claim.");

    private static void EnsureStatus(StockTransfer transfer, params string[] allowed)
    {
        if (!allowed.Contains(transfer.Status, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Invalid status transition from {transfer.Status}.");
    }
}
