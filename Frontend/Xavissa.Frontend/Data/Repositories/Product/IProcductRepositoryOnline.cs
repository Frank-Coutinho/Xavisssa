using System.Collections.Generic;
using System.Threading.Tasks;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public interface IProductRepositoryOnline
    {
        Task<List<Product>> GetAllAsync();
        Task<List<Product>> GetSellableProductsAsync(int storeId);
        Task<List<Product>> GetCatalogAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByBarcodeAsync(string barcode);
        Task<Product?> CreateAsync(Product product);
        Task<Product?> UpdateAsync(Product product);
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
    }
}
