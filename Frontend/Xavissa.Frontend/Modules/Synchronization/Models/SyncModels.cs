using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xavissa.Frontend.Models
{
    public class StoreBootstrapSyncDto
    {
        public DateTime ServerUtcNow { get; set; }
        public string ScopeRole { get; set; } = string.Empty;
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
        public DateTime? CatalogCursor { get; set; }
        public DateTime? SellableVariantsCursor { get; set; }
        public DateTime? StockCursor { get; set; }
        public DateTime? SalesCursor { get; set; }
        public List<CategoryDeltaItemDto> Categories { get; set; } = new();
        public List<ProductCatalogDeltaItemDto> Products { get; set; } = new();
        public List<StoreProductDeltaItemDto> StoreProducts { get; set; } = new();
        public List<ProductVariantDeltaItemDto> ProductVariants { get; set; } = new();
        public List<SellableVariantSyncItemDto> SellableVariants { get; set; } = new();
        public List<StockLevelDeltaItemDto> StockLevels { get; set; } = new();
    }

    public class StoreSellableVariantsDeltaDto
    {
        public DateTime ServerUtcNow { get; set; }
        public DateTime Cursor { get; set; }
        public List<SellableVariantSyncItemDto> Items { get; set; } = new();
        public List<int> RemovedVariantIds { get; set; } = new();
    }

    public class StockLevelsDeltaDto
    {
        public DateTime ServerUtcNow { get; set; }
        public DateTime Cursor { get; set; }
        public List<StockLevelDeltaItemDto> Items { get; set; } = new();
    }

    public class LiveStockCheckRequestDto
    {
        public int StoreId { get; set; }
        public List<int> VariantIds { get; set; } = new();
    }

    public class LiveStockCheckResponseDto
    {
        public DateTime ServerUtcNow { get; set; }
        public List<StockLevelDeltaItemDto> Items { get; set; } = new();
    }

    public class CatalogDeltaDto
    {
        public DateTime ServerUtcNow { get; set; }
        public DateTime Cursor { get; set; }
        public List<CategoryDeltaItemDto> Categories { get; set; } = new();
        public List<ProductCatalogDeltaItemDto> Products { get; set; } = new();
        public List<StoreProductDeltaItemDto> StoreProducts { get; set; } = new();
        public List<ProductVariantDeltaItemDto> ProductVariants { get; set; } = new();
    }

    public class SalesDeltaDto
    {
        public DateTime ServerUtcNow { get; set; }
        public DateTime Cursor { get; set; }
        public List<SaleReadDto> Sales { get; set; } = new();
        public List<int> DeletedSaleIds { get; set; } = new();
    }

    public class SalesUploadBatchRequestDto
    {
        public List<PendingSaleUploadDto> Sales { get; set; } = new();
    }

    public class PendingSaleUploadDto
    {
        public int ClientSaleId { get; set; }
        public SaleSyncDto Sale { get; set; } = new();
    }

    public class SalesUploadBatchResultDto
    {
        public DateTime ServerUtcNow { get; set; }
        public List<SalesUploadResultItemDto> Results { get; set; } = new();
    }

    public class SalesUploadResultItemDto
    {
        public int ClientSaleId { get; set; }
        public int? ServerSaleId { get; set; }
        public Guid? SyncId { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ConflictId { get; set; }
        public string? Error { get; set; }
    }

    public class CategoryDeltaItemDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int TenantId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class ProductCatalogDeltaItemDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int TenantId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class StoreProductDeltaItemDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int? TenantId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class ProductVariantDeltaItemDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        [JsonPropertyName("storeProductId")]
        public int StoreProductId { get; set; }
        public int? TenantId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string? SKU { get; set; }
        public string? Barcode { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public bool IsActive { get; set; }
        public string? Label { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class SellableVariantSyncItemDto
    {
        public int VariantId { get; set; }
        public int StoreProductId { get; set; }
        public int ProductId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? VariantLabel { get; set; }
        public string? Barcode { get; set; }
        public string? SKU { get; set; }
        public decimal Price { get; set; }
        public int QuantityOnHand { get; set; }
        public bool IsSellable { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StockLevelDeltaItemDto
    {
        public int VariantId { get; set; }
        public int StoreId { get; set; }
        public int TenantId { get; set; }
        public int QuantityOnHand { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
