using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();
        Task<List<Product>> GetSellableProductsAsync(int storeId);
        Task<List<Product>> GetCatalogAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids);
        Task DecreaseStockAsync(int productId, int quantity);
        Task DecreaseStockRangeAsync(IEnumerable<(int ProductId, int Quantity)> items);
        Task DecreaseSellableVariantStockRangeAsync(IEnumerable<(int VariantId, int Quantity)> items);
        Task SaveAsync();
        Task ClearAsync();
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<Product?> AddOrUpdateAsync(Product product);
        Task DeleteAsync(int id);
        Task<List<CatalogCategory>> GetCategoriesAsync();
        Task<CatalogCategory?> SaveCategoryAsync(CatalogCategory category);
        Task DeleteCategoryAsync(int categoryId);
        Task<List<ProductStoreAssignment>> GetStoreAssignmentsAsync(int productId);
        Task SaveStoreAssignmentAsync(int productId, ProductStoreAssignment assignment);
        Task RemoveStoreAssignmentAsync(int productId, int storeId);
        Task<List<ProductVariantRecord>> GetVariantsAsync(int productId, int storeId);
        Task<ProductVariantRecord?> SaveVariantAsync(int productId, ProductVariantRecord variant);
        Task DeleteVariantAsync(int variantId);
        Task<ProductVariantRecord?> GenerateVariantBarcodeAsync(int variantId);
        Task<byte[]?> GetVariantBarcodeImageAsync(int variantId);

        // Task DebugPrintServerProducts();
        Task SyncAsync(bool replaceLocalData = false);
    }
}
