using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IProductRepositoryOffline
    {
        Task<List<Product>> GetAllAsync();

        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids);

        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<SellableVariantSnapshot?> GetSellableVariantByBarcodeAsync(int storeId, string barcode);
        Task<List<SellableVariantSnapshot>> GetSellableVariantsAsync(int storeId);

        Task DecreaseStockAsync(int productId, int quantity);
        Task DecreaseStockRangeAsync(IEnumerable<(int ProductId, int Quantity)> items);
        Task DecreaseSellableVariantStockRangeAsync(IEnumerable<(int VariantId, int Quantity)> items);
        Task SaveAsync();

        Task<List<Product>> GetUnsyncedAsync();
        Task UpdateOnlineIdAsync(int localId, int onlineId, int variantId);
        Task<List<CatalogCategory>> GetCategoriesAsync();
        Task ReplaceCategoriesAsync(IEnumerable<CatalogCategory> categories);
        Task<List<ProductStoreAssignment>> GetStoreAssignmentsAsync(int productId);
        Task ReplaceStoreAssignmentsAsync(int productId, IEnumerable<ProductStoreAssignment> assignments);
        Task<List<ProductVariantRecord>> GetVariantsAsync(int productId, int storeId);
        Task ReplaceVariantsAsync(int productId, int storeId, IEnumerable<ProductVariantRecord> variants);
        Task UpsertSellableProductsSnapshotAsync(int storeId, IEnumerable<Product> products);
        Task UpsertSellableVariantsAsync(int storeId, IEnumerable<SellableVariantSyncItemDto> variants, IEnumerable<int>? removedVariantIds = null);
        Task ApplyStockLevelDeltaAsync(int storeId, IEnumerable<StockLevelDeltaItemDto> items);
        Task UpsertCatalogDeltaAsync(
            IEnumerable<CategoryDeltaItemDto> categories,
            IEnumerable<ProductCatalogDeltaItemDto> products,
            IEnumerable<StoreProductDeltaItemDto> storeProducts,
            IEnumerable<ProductVariantDeltaItemDto> productVariants);
        Task<DateTime?> GetCursorAsync(string key);
        Task SetCursorAsync(string key, DateTime? value);

        Task AddOrUpdateAsync(Product product);

        Task AddOrUpdateRangeAsync(IEnumerable<Product> products);

        Task DeleteAsync(int id);

        Task ClearAsync();
    }
}
