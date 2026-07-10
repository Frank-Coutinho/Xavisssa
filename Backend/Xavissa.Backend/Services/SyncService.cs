using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Services;

public class SyncService
{
    private readonly XavissaDbContext _db;
    private readonly TenantAccessService _tenantAccess;
    private readonly SalesService _salesService;
    private readonly SyncConflictService _syncConflictService;

    public SyncService(
        XavissaDbContext db,
        TenantAccessService tenantAccess,
        SalesService salesService,
        SyncConflictService syncConflictService)
    {
        _db = db;
        _tenantAccess = tenantAccess;
        _salesService = salesService;
        _syncConflictService = syncConflictService;
    }

    public async Task<StoreBootstrapSyncDto> GetBootstrapAsync(bool includeCatalog = false)
    {
        var now = DateTime.UtcNow;
        var response = new StoreBootstrapSyncDto
        {
            ServerUtcNow = now,
            ScopeRole = ResolveScopeRole(),
            TenantId = _tenantAccess.SelectedTenantId,
            StoreId = _tenantAccess.SelectedStoreId,
        };

        if (_tenantAccess.SelectedStoreId.HasValue)
        {
            response.SellableVariants = await QuerySellableVariantsAsync(
                _tenantAccess.SelectedStoreId.Value,
                null);
            response.StockLevels = await QueryStockLevelsAsync(_tenantAccess.SelectedStoreId.Value, null);
            response.SellableVariantsCursor = response.SellableVariants.Count == 0
                ? now
                : response.SellableVariants.Max(x => x.UpdatedAt);
            response.StockCursor = response.StockLevels.Count == 0
                ? now
                : response.StockLevels.Max(x => x.UpdatedAt);

            if (_tenantAccess.ActingRole.IsStoreManager())
            {
                response.StoreProducts = await QueryStoreProductsAsync(_tenantAccess.SelectedStoreId.Value, null);
                response.ProductVariants = await QueryProductVariantsForStoreAsync(
                    _tenantAccess.SelectedStoreId.Value,
                    null);
            }

            response.SalesCursor = await _db.Sales
                .AsNoTracking()
                .Where(x => x.StoreId == _tenantAccess.SelectedStoreId.Value)
                .Select(x => (DateTime?)(x.UpdatedAt ?? x.SaleDate))
                .MaxAsync()
                ?? now;
        }

        if (ShouldIncludeCatalog(includeCatalog))
        {
            var tenantId = _tenantAccess.SelectedTenantId;
            if (tenantId.HasValue)
            {
                response.Categories = await QueryCategoriesAsync(tenantId.Value, null);
                response.Products = await QueryProductsAsync(tenantId.Value, null);
                response.CatalogCursor = MaxDate(
                    response.Categories.Select(x => x.UpdatedAt),
                    response.Products.Select(x => x.UpdatedAt))
                    ?? now;
            }
        }
        if (!response.SalesCursor.HasValue)
            response.SalesCursor = now;
        return response;
    }

    public async Task<StoreSellableVariantsDeltaDto> GetSellableVariantsDeltaAsync(
        int storeId,
        DateTime? updatedAfter)
    {
        EnsureStoreAccess(storeId);
        updatedAfter = NormalizeUtc(updatedAfter);
        var now = DateTime.UtcNow;
        var items = await QuerySellableVariantsAsync(storeId, updatedAfter);
        var removedVariantIds = await QueryRemovedSellableVariantIdsAsync(storeId, updatedAfter);

        return new StoreSellableVariantsDeltaDto
        {
            ServerUtcNow = now,
            Cursor = items.Count == 0 ? now : items.Max(x => x.UpdatedAt),
            Items = items,
            RemovedVariantIds = removedVariantIds,
        };
    }

    public async Task<StockLevelsDeltaDto> GetStockLevelsDeltaAsync(int storeId, DateTime? updatedAfter)
    {
        EnsureStoreAccess(storeId);
        updatedAfter = NormalizeUtc(updatedAfter);
        var now = DateTime.UtcNow;
        var items = await QueryStockLevelsAsync(storeId, updatedAfter);
        return new StockLevelsDeltaDto
        {
            ServerUtcNow = now,
            Cursor = items.Count == 0 ? now : items.Max(x => x.UpdatedAt),
            Items = items,
        };
    }

    public async Task<CatalogDeltaDto> GetCatalogDeltaAsync(int tenantId, DateTime? updatedAfter)
    {
        EnsureTenantAccess(tenantId);
        updatedAfter = NormalizeUtc(updatedAfter);
        var now = DateTime.UtcNow;
        var categories = await QueryCategoriesAsync(tenantId, updatedAfter);
        var products = await QueryProductsAsync(tenantId, updatedAfter);
        var storeProducts = _tenantAccess.ActingRole.IsTenantAdmin() || _tenantAccess.IsPlatformAdmin || _tenantAccess.IsSupport
            ? await QueryStoreProductsForTenantAsync(tenantId, updatedAfter)
            : new List<StoreProductDeltaItemDto>();
        var variants = _tenantAccess.ActingRole.IsTenantAdmin() || _tenantAccess.IsPlatformAdmin || _tenantAccess.IsSupport
            ? await QueryProductVariantsForTenantAsync(tenantId, updatedAfter)
            : new List<ProductVariantDeltaItemDto>();

        return new CatalogDeltaDto
        {
            ServerUtcNow = now,
            Cursor = MaxDate(
                    categories.Select(x => x.UpdatedAt),
                    products.Select(x => x.UpdatedAt),
                    storeProducts.Select(x => x.UpdatedAt),
                    variants.Select(x => x.UpdatedAt))
                ?? now,
            Categories = categories,
            Products = products,
            StoreProducts = storeProducts,
            ProductVariants = variants,
        };
    }

    public async Task<SalesDeltaDto> GetSalesDeltaAsync(int storeId, DateTime? updatedAfter)
    {
        EnsureStoreAccess(storeId);
        updatedAfter = NormalizeUtc(updatedAfter);
        var now = DateTime.UtcNow;
        var salesQuery = _db.Sales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Payments)
            .Where(x => x.StoreId == storeId);
        if (updatedAfter.HasValue)
            salesQuery = salesQuery.Where(x => (x.UpdatedAt ?? x.SaleDate) > updatedAfter.Value);

        var saleRows = await salesQuery
            .OrderBy(x => x.SaleDate)
            .Select(sale => new
            {
                Cursor = sale.UpdatedAt ?? sale.SaleDate,
                    Sale = new SaleReadDto
                    {
                        Id = sale.Id,
                        OnlineId = sale.Id,
                        SyncId = sale.SyncId,
                        TenantId = sale.TenantId ?? 0,
                        StoreId = sale.StoreId,
                        SourceDeviceId = sale.SourceDeviceId,
                        ClientCreatedAt = sale.ClientCreatedAt,
                        ClientUpdatedAt = sale.ClientUpdatedAt,
                        LastSyncedAt = sale.LastSyncedAt,
                        CreatedAt = sale.CreatedAt,
                        UpdatedAt = sale.UpdatedAt,
                        SaleDate = sale.SaleDate,
                    TotalAmount = sale.TotalAmount,
                    Discount = sale.Discount,
                    TotalPaid = sale.Payments.Sum(payment => payment.Amount),
                    PaymentSummary = string.Join(", ", sale.Payments.Select(payment => payment.PaymentMethod).Distinct()),
                    PaymentStatus = sale.PaymentStatus,
                    ChangeGiven = sale.ChangeGiven,
                    ReceiptNumber = sale.ReceiptNumber,
                    Status = sale.Status,
                    IsRefunded = sale.IsRefunded,
                    IsVoided = sale.IsVoided,
                    RefundReason = sale.RefundReason,
                    VoidedAt = sale.VoidedAt,
                    VoidedByUserId = sale.VoidedByUserId,
                    VoidReason = sale.VoidReason,
                    CashRegisterSessionId = sale.CashRegisterSessionId,
                    CashRegisterTrackingMode = sale.CashRegisterTrackingMode,
                    HasUntrackedCashPayment = sale.HasUntrackedCashPayment,
                    SalePayments = sale.Payments.Select(payment => new SalePaymentReadDto
                    {
                        Id = payment.Id,
                        OnlineId = payment.Id,
                        SyncId = payment.SyncId,
                        SourceDeviceId = payment.SourceDeviceId,
                        ClientCreatedAt = payment.ClientCreatedAt,
                        ClientUpdatedAt = payment.ClientUpdatedAt,
                        LastSyncedAt = payment.LastSyncedAt,
                        CreatedAt = payment.CreatedAt,
                        PaymentMethod = payment.PaymentMethod,
                        Amount = payment.Amount,
                        CashRegisterSessionId = payment.CashRegisterSessionId,
                        ReferenceNumber = payment.ReferenceNumber,
                        Notes = payment.Notes,
                    }).ToList(),
                },
            })
            .ToListAsync();

        var saleHeaders = saleRows.Select(row => row.Sale).ToList();
        var saleIds = saleHeaders.Select(sale => sale.Id).ToList();
        var saleItems = saleIds.Count == 0
            ? new List<SaleItemDeltaRow>()
            : await _db.SaleItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => saleIds.Contains(item.SaleId))
                .Select(item => new SaleItemDeltaRow
                {
                    SaleId = item.SaleId,
                    Item = new SaleItemReadDto
                    {
                        Id = item.Id,
                        OnlineId = item.Id,
                        SyncId = item.SyncId,
                        TenantId = item.TenantId ?? 0,
                        StoreId = item.StoreId,
                        SourceDeviceId = item.SourceDeviceId,
                        ClientCreatedAt = item.ClientCreatedAt,
                        ClientUpdatedAt = item.ClientUpdatedAt,
                        LastSyncedAt = item.LastSyncedAt,
                        CreatedAt = item.CreatedAt,
                        UpdatedAt = item.UpdatedAt,
                        ProductId = item.Variant != null && item.Variant.ProductStoreAssignment != null
                            ? item.Variant.ProductStoreAssignment.ProductId
                            : 0,
                        VariantId = item.VariantId,
                        ProductName = item.Variant != null && item.Variant.ProductStoreAssignment != null
                            ? item.Variant.ProductStoreAssignment.Product.Name
                            : string.Empty,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.UnitPrice * item.Quantity,
                        ProductCategory = item.Variant != null
                            && item.Variant.ProductStoreAssignment != null
                            && item.Variant.ProductStoreAssignment.Product.CategoryNavigation != null
                            ? item.Variant.ProductStoreAssignment.Product.CategoryNavigation.Name
                            : string.Empty,
                        IsRefunded = item.IsRefunded,
                        RefundedQuantity = item.RefundedQuantity,
                        RefundableQuantity = item.Quantity - item.RefundedQuantity > 0
                            ? item.Quantity - item.RefundedQuantity
                            : 0,
                        RefundReason = item.RefundReason,
                    },
                })
                .ToListAsync();

        var itemsBySaleId = saleItems
            .GroupBy(x => x.SaleId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.Item).ToList());

        foreach (var sale in saleHeaders)
            sale.SaleItems = itemsBySaleId.TryGetValue(sale.Id, out var items)
                ? items
                : new List<SaleItemReadDto>();

        var cursor = saleRows.Count == 0
            ? now
            : saleRows.Max(x => x.Cursor);

        return new SalesDeltaDto
        {
            ServerUtcNow = now,
            Cursor = cursor,
            Sales = saleHeaders,
        };
    }

    public async Task<SalesUploadBatchResultDto> UploadSalesBatchAsync(SalesUploadBatchRequestDto request)
    {
        var results = new List<SalesUploadResultItemDto>();
        var now = DateTime.UtcNow;

        foreach (var pendingSale in request.Sales)
        {
            try
            {
                var dto = pendingSale.Sale;
                var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
                var storeId = dto.StoreId ?? _tenantAccess.SelectedStoreId;

                if (!tenantId.HasValue || !storeId.HasValue || !_tenantAccess.CurrentUserId.HasValue)
                {
                    results.Add(new SalesUploadResultItemDto
                    {
                        ClientSaleId = pendingSale.ClientSaleId,
                        Success = false,
                        Error = "Tenant, store, and current user are required.",
                    });
                    continue;
                }

                if (!_tenantAccess.CanAccessStore(storeId.Value))
                {
                    results.Add(new SalesUploadResultItemDto
                    {
                        ClientSaleId = pendingSale.ClientSaleId,
                        Success = false,
                        Error = "Unauthorized store.",
                    });
                    continue;
                }

                if (dto.SyncId != Guid.Empty)
                {
                    var existing = await _db.Sales
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(sale =>
                            sale.SyncId == dto.SyncId
                            && sale.TenantId == tenantId.Value
                            && sale.StoreId == storeId.Value);
                    if (existing != null)
                    {
                        results.Add(new SalesUploadResultItemDto
                        {
                            ClientSaleId = pendingSale.ClientSaleId,
                            ServerSaleId = existing.Id,
                            SyncId = existing.SyncId,
                            Success = true,
                            Status = "Existing",
                        });
                        continue;
                    }
                }

                var sale = await _salesService.CreateSaleAsync(
                    dto.SaleItems,
                    dto.SalePayments,
                    _tenantAccess.CurrentUserId.Value,
                    tenantId.Value,
                    storeId.Value,
                    dto.Discount ?? 0,
                    dto.SyncId,
                    dto.SourceDeviceId,
                    dto.ClientCreatedAt,
                    dto.ClientUpdatedAt);

                results.Add(new SalesUploadResultItemDto
                {
                    ClientSaleId = pendingSale.ClientSaleId,
                    ServerSaleId = sale.Id,
                    SyncId = sale.SyncId,
                    Success = true,
                    Status = "Created",
                });
            }
            catch (Exception ex)
            {
                var dto = pendingSale.Sale;
                var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
                var storeId = dto.StoreId ?? _tenantAccess.SelectedStoreId;
                int? conflictId = null;
                if (tenantId.HasValue)
                {
                    var conflictType = ResolveUploadConflictType(ex);
                    var conflict = await _syncConflictService.CreateAsync(
                        tenantId.Value,
                        storeId,
                        "Sale",
                        dto.SyncId == Guid.Empty ? null : dto.SyncId,
                        conflictType,
                        JsonSerializer.Serialize(dto));
                    conflictId = conflict.Id;
                }

                results.Add(new SalesUploadResultItemDto
                {
                    ClientSaleId = pendingSale.ClientSaleId,
                    Success = false,
                    Status = conflictId.HasValue ? "Conflict" : "Rejected",
                    ConflictId = conflictId,
                    Error = ex.Message,
                });
            }
        }

        return new SalesUploadBatchResultDto
        {
            ServerUtcNow = now,
            Results = results,
        };
    }

    private Task<List<SellableVariantSyncItemDto>> QuerySellableVariantsAsync(
        int storeId,
        DateTime? updatedAfter)
    {
        var query = _db.StoreSellableVariants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.StoreId == storeId && x.IsSellable);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value);

        return query
            .OrderBy(x => x.ProductName)
            .Select(x => new SellableVariantSyncItemDto
            {
                VariantId = x.VariantId,
                StoreProductId = x.StoreProductId,
                ProductId = x.ProductId,
                TenantId = x.TenantId,
                StoreId = x.StoreId,
                ProductName = x.ProductName,
                VariantLabel = x.VariantLabel,
                Barcode = x.Barcode,
                SKU = x.SKU,
                Price = x.Price,
                QuantityOnHand = x.QuantityOnHand,
                IsSellable = x.IsSellable,
                UpdatedAt = x.UpdatedAt,
            })
            .ToListAsync();
    }

    private async Task<List<int>> QueryRemovedSellableVariantIdsAsync(int storeId, DateTime? updatedAfter)
    {
        if (!updatedAfter.HasValue)
            return new List<int>();

        var changedVariantIds = await _db.ProductVariants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v =>
                v.ProductStoreAssignment != null
                && v.ProductStoreAssignment.StoreId == storeId
                && (v.UpdatedAt > updatedAfter.Value
                    || v.ProductStoreAssignment.UpdatedAt > updatedAfter.Value
                    || (v.ProductStoreAssignment.Product.UpdatedAt ?? v.ProductStoreAssignment.Product.CreatedAt) > updatedAfter.Value
                    || (v.DeletedAt.HasValue && v.DeletedAt > updatedAfter.Value)
                    || (v.ProductStoreAssignment.DeletedAt.HasValue && v.ProductStoreAssignment.DeletedAt > updatedAfter.Value)
                    || (v.ProductStoreAssignment.Product.DeletedAt.HasValue && v.ProductStoreAssignment.Product.DeletedAt > updatedAfter.Value)))
            .Select(v => v.Id)
            .Distinct()
            .ToListAsync();

        if (changedVariantIds.Count == 0)
            return new List<int>();

        var activeIds = await _db.StoreSellableVariants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.StoreId == storeId && changedVariantIds.Contains(v.VariantId) && v.IsSellable)
            .Select(v => v.VariantId)
            .ToListAsync();

        return changedVariantIds.Except(activeIds).ToList();
    }

    private Task<List<StockLevelDeltaItemDto>> QueryStockLevelsAsync(int storeId, DateTime? updatedAfter)
    {
        var query = _db.StockLevels
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.StoreId == storeId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value);

        return query
            .Select(x => new StockLevelDeltaItemDto
            {
                VariantId = x.VariantId,
                StoreId = x.StoreId,
                TenantId = x.TenantId ?? 0,
                QuantityOnHand = x.QuantityOnHand,
                UpdatedAt = x.UpdatedAt ?? DateTime.UtcNow,
            })
            .ToListAsync();
    }

    private Task<List<CategoryDeltaItemDto>> QueryCategoriesAsync(int tenantId, DateTime? updatedAfter)
    {
        var query = _db.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new CategoryDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId ?? tenantId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            Name = x.Name,
            IsActive = x.IsActive,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private Task<List<ProductCatalogDeltaItemDto>> QueryProductsAsync(int tenantId, DateTime? updatedAfter)
    {
        var query = _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new ProductCatalogDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId ?? tenantId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            CategoryId = x.CategoryId,
            Name = x.Name,
            Description = x.Description,
            Code = x.Code,
            Brand = x.Brand,
            IsActive = x.IsActive,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private Task<List<StoreProductDeltaItemDto>> QueryStoreProductsAsync(int storeId, DateTime? updatedAfter)
    {
        var query = _db.ProductStoreAssignments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.StoreId == storeId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new StoreProductDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            ProductId = x.ProductId,
            StoreId = x.StoreId,
            IsActive = x.IsActive,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private Task<List<StoreProductDeltaItemDto>> QueryStoreProductsForTenantAsync(int tenantId, DateTime? updatedAfter)
    {
        var query = _db.ProductStoreAssignments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new StoreProductDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            ProductId = x.ProductId,
            StoreId = x.StoreId,
            IsActive = x.IsActive,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private Task<List<ProductVariantDeltaItemDto>> QueryProductVariantsForStoreAsync(
        int storeId,
        DateTime? updatedAfter)
    {
        var query = _db.ProductVariants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.ProductStoreAssignment != null && x.ProductStoreAssignment.StoreId == storeId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new ProductVariantDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId,
            StoreProductId = x.ProductStoreAssignmentId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            SKU = x.SKU,
            Barcode = x.Barcode,
            Description = x.Description,
            Price = x.Price,
            IsActive = x.IsActive,
            Label = x.Label,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private Task<List<ProductVariantDeltaItemDto>> QueryProductVariantsForTenantAsync(
        int tenantId,
        DateTime? updatedAfter)
    {
        var query = _db.ProductVariants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (updatedAfter.HasValue)
            query = query.Where(x => x.UpdatedAt > updatedAfter.Value || (x.DeletedAt.HasValue && x.DeletedAt > updatedAfter.Value));

        return query.Select(x => new ProductVariantDeltaItemDto
        {
            Id = x.Id,
            OnlineId = x.Id,
            SyncId = x.SyncId,
            TenantId = x.TenantId,
            StoreProductId = x.ProductStoreAssignmentId,
            SourceDeviceId = x.SourceDeviceId,
            ClientCreatedAt = x.ClientCreatedAt,
            ClientUpdatedAt = x.ClientUpdatedAt,
            LastSyncedAt = x.LastSyncedAt,
            SKU = x.SKU,
            Barcode = x.Barcode,
            Description = x.Description,
            Price = x.Price,
            IsActive = x.IsActive,
            Label = x.Label,
            UpdatedAt = x.UpdatedAt,
            DeletedAt = x.DeletedAt,
        }).ToListAsync();
    }

    private static SaleReadDto MapSale(Sale sale)
    {
        return new SaleReadDto
        {
            Id = sale.Id,
            OnlineId = sale.Id,
            SyncId = sale.SyncId,
            TenantId = sale.TenantId ?? 0,
            StoreId = sale.StoreId,
            SourceDeviceId = sale.SourceDeviceId,
            ClientCreatedAt = sale.ClientCreatedAt,
            ClientUpdatedAt = sale.ClientUpdatedAt,
            LastSyncedAt = sale.LastSyncedAt,
            CreatedAt = sale.CreatedAt,
            UpdatedAt = sale.UpdatedAt,
            SaleDate = sale.SaleDate,
            TotalAmount = sale.TotalAmount,
            Discount = sale.Discount,
            TotalPaid = sale.Payments.Sum(x => x.Amount),
            PaymentSummary = SalesService.BuildPaymentSummary(sale.Payments),
            PaymentStatus = sale.PaymentStatus,
            ChangeGiven = sale.ChangeGiven,
            ReceiptNumber = sale.ReceiptNumber,
            Status = sale.Status,
            IsRefunded = sale.IsRefunded,
            IsVoided = sale.IsVoided,
            RefundReason = sale.RefundReason,
            VoidedAt = sale.VoidedAt,
            VoidedByUserId = sale.VoidedByUserId,
            VoidReason = sale.VoidReason,
            CashRegisterSessionId = sale.CashRegisterSessionId,
            CashRegisterTrackingMode = sale.CashRegisterTrackingMode,
            HasUntrackedCashPayment = sale.HasUntrackedCashPayment,
            SaleItems = sale.SaleItems.Select(item => new SaleItemReadDto
            {
                Id = item.Id,
                OnlineId = item.Id,
                SyncId = item.SyncId,
                TenantId = item.TenantId ?? 0,
                StoreId = item.StoreId,
                SourceDeviceId = item.SourceDeviceId,
                ClientCreatedAt = item.ClientCreatedAt,
                ClientUpdatedAt = item.ClientUpdatedAt,
                LastSyncedAt = item.LastSyncedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                ProductId = item.Variant?.ProductId ?? 0,
                VariantId = item.VariantId,
                ProductName = item.Variant?.Product?.Name ?? item.ProductNameFallback(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Subtotal = item.UnitPrice * item.Quantity,
                ProductCategory = item.Variant?.Product?.CategoryNavigation?.Name ?? string.Empty,
                IsRefunded = item.IsRefunded,
                RefundedQuantity = item.RefundedQuantity,
                RefundableQuantity = Math.Max(item.Quantity - item.RefundedQuantity, 0),
                RefundReason = item.RefundReason,
            }).ToList(),
            SalePayments = sale.Payments.Select(payment => new SalePaymentReadDto
            {
                Id = payment.Id,
                OnlineId = payment.Id,
                SyncId = payment.SyncId,
                SourceDeviceId = payment.SourceDeviceId,
                ClientCreatedAt = payment.ClientCreatedAt,
                ClientUpdatedAt = payment.ClientUpdatedAt,
                LastSyncedAt = payment.LastSyncedAt,
                CreatedAt = payment.CreatedAt,
                PaymentMethod = payment.PaymentMethod,
                Amount = payment.Amount,
                CashRegisterSessionId = payment.CashRegisterSessionId,
                ReferenceNumber = payment.ReferenceNumber,
                Notes = payment.Notes,
            }).ToList(),
        };
    }

    private sealed class SaleItemDeltaRow
    {
        public int SaleId { get; init; }
        public SaleItemReadDto Item { get; init; } = new();
    }

    private bool ShouldIncludeCatalog(bool includeCatalog) =>
        includeCatalog || _tenantAccess.IsPlatformAdmin || _tenantAccess.IsSupport;

    private string ResolveScopeRole()
    {
        if (_tenantAccess.IsPlatformAdmin)
            return AccessRoles.SystemAdmin;
        if (_tenantAccess.IsSupport)
            return AccessRoles.Support;
        return _tenantAccess.ActingRole ?? AccessRoles.User;
    }

    private void EnsureStoreAccess(int storeId)
    {
        if (!_tenantAccess.CanAccessStore(storeId))
            throw new UnauthorizedAccessException("Unauthorized store.");
    }

    private void EnsureTenantAccess(int tenantId)
    {
        if (!_tenantAccess.CanAccessTenant(tenantId))
            throw new UnauthorizedAccessException("Unauthorized tenant.");
    }

    private static DateTime? MaxDate(params IEnumerable<DateTime?>[] values)
    {
        return values
            .SelectMany(x => x)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty()
            .Max();
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            // Sync cursors are emitted as UTC. If the client sends them without a
            // timezone suffix, ASP.NET binds them as Unspecified; keep the instant.
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };
    }

    private static string ResolveUploadConflictType(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("Insufficient stock", StringComparison.OrdinalIgnoreCase))
            return SyncConflictTypes.InsufficientRemoteStock;
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) || message.Contains("not active", StringComparison.OrdinalIgnoreCase))
            return SyncConflictTypes.InactiveVariantSoldOffline;
        if (message.Contains("offline", StringComparison.OrdinalIgnoreCase))
            return SyncConflictTypes.OfflinePolicyConflict;
        if (message.Contains("receipt", StringComparison.OrdinalIgnoreCase))
            return SyncConflictTypes.DuplicateReceiptNumber;
        return SyncConflictTypes.UnknownUploadConflict;
    }
}

internal static class SaleItemSyncExtensions
{
    public static string ProductNameFallback(this SaleItem item) =>
        item.Variant?.Product?.Name ?? "Unknown Product";
}
