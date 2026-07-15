using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly IProductRepositoryOnline _online;
        private readonly IProductRepositoryOffline _offline;
        private readonly IConnectivityService _net;
        private readonly IApiTokenProvider _tokens;
        private readonly IAuthService _auth;

        public ProductRepository(
            IProductRepositoryOnline online,
            IProductRepositoryOffline offline,
            IConnectivityService net,
            IApiTokenProvider tokens,
            IAuthService auth
        )
        {
            _online = online;
            _offline = offline;
            _net = net;
            _tokens = tokens;
            _auth = auth;
        }

        private bool CanUseAuthenticatedOnline() =>
            _net.IsOnline() && !string.IsNullOrWhiteSpace(_tokens.Token);

        private void EnsureAuthenticatedOnline(string operation)
        {
            if (!_net.IsOnline())
                throw new InvalidOperationException($"{operation} requires an internet connection.");

            if (string.IsNullOrWhiteSpace(_tokens.Token))
                throw new InvalidOperationException($"{operation} requires an authenticated online session. Please sign in again.");
        }

        // -------------------------
        // READS
        // -------------------------
        public async Task<List<Product>> GetAllAsync()
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var serverProducts = await _online.GetAllAsync();

                    // Merge into local store
                    await _offline.AddOrUpdateRangeAsync(serverProducts);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Online fetch failed: {ex.Message}");
                }
            }

            // Always return local data (single source of truth)
            return await _offline.GetAllAsync();
        }

        public async Task<List<Product>> GetSellableProductsAsync(int storeId)
        {
            var sellableVariants = await _offline.GetSellableVariantsAsync(storeId);
            if (sellableVariants.Count > 0)
            {
                var baseProductsByOnlineId = (await _offline.GetAllAsync())
                    .Where(product => product.OnlineId > 0 && product.VariantId == 0)
                    .GroupBy(product => product.OnlineId)
                    .ToDictionary(group => group.Key, group => group.First());

                return sellableVariants
                    .Where(variant => variant.IsSellable)
                    .OrderBy(variant => variant.ProductName)
                    .ThenBy(variant => variant.VariantLabel)
                    .Select(variant =>
                    {
                        baseProductsByOnlineId.TryGetValue(variant.ProductId, out var baseProduct);
                        return new Product
                        {
                            Id = baseProduct?.Id ?? 0,
                            OnlineId = variant.ProductId,
                            TenantId = variant.TenantId,
                            VariantId = variant.VariantId,
                            AssignmentId = variant.StoreProductId,
                            StoreId = variant.StoreId,
                            CategoryId = baseProduct?.CategoryId,
                            Code = baseProduct?.Code ?? string.Empty,
                            Barcode = variant.Barcode,
                            Name = variant.ProductName,
                            SKU = variant.SKU,
                            Label = variant.VariantLabel,
                            Description = baseProduct?.Description ?? string.Empty,
                            Category = baseProduct?.Category ?? string.Empty,
                            Brand = baseProduct?.Brand ?? string.Empty,
                            Price = variant.Price,
                            StockQuantity = variant.QuantityOnHand,
                            IsActive = variant.IsSellable,
                            VariantCount = 1,
                            CreatedAt = baseProduct?.CreatedAt ?? DateTime.UtcNow,
                            UpdatedAt = variant.UpdatedAt,
                        };
                    })
                    .ToList();
            }

            var baseProducts = (await _offline.GetAllAsync())
                .Where(product => product.IsActive && product.OnlineId > 0)
                .ToDictionary(product => product.OnlineId, product => product);

            var assignments = (await _offline.GetAllAsync())
                .Where(product => product.StoreId == storeId && product.VariantId > 0)
                .ToList();

            if (assignments.Count > 0)
            {
                return assignments
                    .Where(product => product.IsActive)
                    .OrderBy(product => product.Name)
                    .ThenBy(product => product.Label)
                    .ToList();
            }

            var variants = new List<Product>();
            foreach (var baseProduct in baseProducts.Values)
            {
                var productVariants = await _offline.GetVariantsAsync(baseProduct.OnlineId, storeId);
                variants.AddRange(productVariants
                    .Where(variant => variant.IsActive)
                    .Select(variant => new Product
                    {
                        Id = baseProduct.Id,
                        OnlineId = baseProduct.OnlineId,
                        TenantId = baseProduct.TenantId,
                        VariantId = variant.Id,
                        AssignmentId = variant.AssignmentId,
                        StoreId = storeId,
                        CategoryId = baseProduct.CategoryId,
                        Code = baseProduct.Code,
                        Barcode = variant.Barcode,
                        Name = baseProduct.Name,
                        SKU = variant.SKU,
                        Label = variant.Label,
                        Description = baseProduct.Description,
                        Category = baseProduct.Category,
                        Brand = baseProduct.Brand,
                        Price = variant.Price,
                        StockQuantity = variant.StockQuantity,
                        IsActive = variant.IsActive,
                        VariantCount = baseProduct.VariantCount,
                        CreatedAt = baseProduct.CreatedAt,
                        UpdatedAt = baseProduct.UpdatedAt,
                    }));
            }

            return variants
                .Where(product => product.IsActive && product.StoreId == storeId && product.VariantId > 0)
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Label)
                .ToList();
        }

        public async Task<List<Product>> GetCatalogAsync()
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var catalog = await _online.GetCatalogAsync();
                    await _offline.AddOrUpdateRangeAsync(catalog);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Catalog fetch failed: {ex.Message}");
                }
            }

            return (await _offline.GetAllAsync())
                .Where(product => product.VariantId == 0)
                .OrderBy(product => product.Name)
                .ToList();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            // `id` here is the local product Id used by cart/sale items.
            // Resolve locally first to avoid accidental localId -> onlineId collisions.
            var local = await _offline.GetByIdAsync(id);

            if (!CanUseAuthenticatedOnline() || local == null)
                return local;

            if (local.OnlineId <= 0)
                return local;

            try
            {
                var online = await _online.GetByIdAsync(local.OnlineId);
                if (online != null)
                    await _offline.AddOrUpdateAsync(online);
            }
            catch
            {
                // Ignore online refresh failures and keep the local mapping.
            }

            return await _offline.GetByIdAsync(id) ?? local;
        }

        public Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
        {
            return _offline.GetByIdsAsync(ids);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            if (_auth.SelectedStoreId.HasValue)
            {
                var baseProductsByOnlineId = (await _offline.GetAllAsync())
                    .Where(product => product.OnlineId > 0 && product.VariantId == 0)
                    .GroupBy(product => product.OnlineId)
                    .ToDictionary(group => group.Key, group => group.First());
                var localSellable = await _offline.GetSellableVariantByBarcodeAsync(
                    _auth.SelectedStoreId.Value,
                    barcode);
                if (localSellable != null)
                {
                    baseProductsByOnlineId.TryGetValue(localSellable.ProductId, out var baseProduct);
                    return new Product
                    {
                        Id = baseProduct?.Id ?? 0,
                        OnlineId = localSellable.ProductId,
                        TenantId = localSellable.TenantId,
                        VariantId = localSellable.VariantId,
                        AssignmentId = localSellable.StoreProductId,
                        StoreId = localSellable.StoreId,
                        CategoryId = baseProduct?.CategoryId,
                        Code = baseProduct?.Code ?? string.Empty,
                        Barcode = localSellable.Barcode,
                        Name = localSellable.ProductName,
                        SKU = localSellable.SKU,
                        Label = localSellable.VariantLabel,
                        Description = baseProduct?.Description ?? string.Empty,
                        Category = baseProduct?.Category ?? string.Empty,
                        Brand = baseProduct?.Brand ?? string.Empty,
                        Price = localSellable.Price,
                        StockQuantity = localSellable.QuantityOnHand,
                        IsActive = localSellable.IsSellable,
                        VariantCount = 1,
                        CreatedAt = baseProduct?.CreatedAt ?? DateTime.UtcNow,
                        UpdatedAt = localSellable.UpdatedAt
                    };
                }
            }

            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var product = await _online.GetByBarcodeAsync(barcode);
                    if (product != null)
                        await _offline.AddOrUpdateAsync(product);

                    return product;
                }
                catch
                {
                }
            }

            return await _offline.GetByBarcodeAsync(barcode);
        }

        // -------------------------
        // WRITES
        // -------------------------
        public async Task<Product?> AddOrUpdateAsync(Product product)
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    Product? serverProduct;

                    if (product.OnlineId == 0)
                        serverProduct = await _online.CreateAsync(product);
                    else
                        serverProduct = await _online.UpdateAsync(product);

                    if (serverProduct != null)
                        await _offline.AddOrUpdateAsync(serverProduct);

                    return serverProduct;
                }
                catch
                {
                    // online failed -> store locally
                }
            }

            await _offline.AddOrUpdateAsync(product);
            return product;
        }

        public async Task DeleteAsync(int id)
        {
            var localProduct = await _offline.GetByIdAsync(id);
            if (localProduct == null && id > 0)
            {
                localProduct = (await _offline.GetAllAsync())
                    .FirstOrDefault(product => product.OnlineId == id);
            }

            if (CanUseAuthenticatedOnline())
            {
                var remoteId = localProduct?.OnlineId ?? id;
                if (remoteId > 0)
                    await _online.DeleteAsync(remoteId);
            }

            if (localProduct != null)
                await _offline.DeleteAsync(localProduct.Id);
        }

        public async Task<List<CatalogCategory>> GetCategoriesAsync()
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var categories = await _online.GetCategoriesAsync();
                    await _offline.ReplaceCategoriesAsync(categories);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Category fetch failed: {ex.Message}");
                }
            }

            return await _offline.GetCategoriesAsync();
        }

        public async Task<CatalogCategory?> SaveCategoryAsync(CatalogCategory category)
        {
            EnsureAuthenticatedOnline("Saving categories");

            return await _online.SaveCategoryAsync(category);
        }

        public async Task DeleteCategoryAsync(int categoryId)
        {
            EnsureAuthenticatedOnline("Deleting categories");

            await _online.DeleteCategoryAsync(categoryId);
        }

        public async Task<List<ProductStoreAssignment>> GetStoreAssignmentsAsync(int productId)
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var assignments = await _online.GetStoreAssignmentsAsync(productId);
                    await _offline.ReplaceStoreAssignmentsAsync(productId, assignments);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Store assignment fetch failed: {ex.Message}");
                }
            }

            return await _offline.GetStoreAssignmentsAsync(productId);
        }

        public async Task SaveStoreAssignmentAsync(int productId, ProductStoreAssignment assignment)
        {
            EnsureAuthenticatedOnline("Assigning products to a store");

            await _online.SaveStoreAssignmentAsync(productId, assignment);
        }

        public async Task RemoveStoreAssignmentAsync(int productId, int storeId)
        {
            EnsureAuthenticatedOnline("Removing product store assignments");

            await _online.RemoveStoreAssignmentAsync(productId, storeId);
        }

        public async Task<List<ProductVariantRecord>> GetVariantsAsync(int productId, int storeId)
        {
            if (CanUseAuthenticatedOnline())
            {
                try
                {
                    var variants = await _online.GetVariantsAsync(productId, storeId);
                    await _offline.ReplaceVariantsAsync(productId, storeId, variants);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Variant fetch failed: {ex.Message}");
                }
            }

            return await _offline.GetVariantsAsync(productId, storeId);
        }

        public async Task<ProductVariantRecord?> SaveVariantAsync(int productId, ProductVariantRecord variant)
        {
            EnsureAuthenticatedOnline("Saving store variants");

            return await _online.SaveVariantAsync(productId, variant);
        }

        public async Task DeleteVariantAsync(int variantId)
        {
            EnsureAuthenticatedOnline("Deleting store variants");

            await _online.DeleteVariantAsync(variantId);
        }

        public async Task<ProductVariantRecord?> GenerateVariantBarcodeAsync(int variantId)
        {
            EnsureAuthenticatedOnline("Generating barcodes");

            return await _online.GenerateVariantBarcodeAsync(variantId);
        }

        public async Task<byte[]?> GetVariantBarcodeImageAsync(int variantId)
        {
            EnsureAuthenticatedOnline("Loading barcode images");

            return await _online.GetVariantBarcodeImageAsync(variantId);
        }

        public async Task DecreaseStockAsync(int productId, int quantity)
        {
            await _offline.DecreaseStockAsync(productId, quantity);
        }

        public async Task DecreaseStockRangeAsync(IEnumerable<(int ProductId, int Quantity)> items)
        {
            await _offline.DecreaseStockRangeAsync(items);
        }

        public async Task DecreaseSellableVariantStockRangeAsync(IEnumerable<(int VariantId, int Quantity)> items)
        {
            await _offline.DecreaseSellableVariantStockRangeAsync(items);
        }

        public async Task SaveAsync()
        {
            await _offline.SaveAsync();
        }

        public async Task ClearAsync()
        {
            await _offline.ClearAsync();
        }

        // -------------------------
        // SYNC ENTRY POINT
        // -------------------------
        public async Task SyncAsync(bool replaceLocalData = false)
        {
            if (!CanUseAuthenticatedOnline())
                return;

            // 1?? Upload local unsynced products
            var unsynced = await _offline.GetUnsyncedAsync();

            foreach (var product in unsynced)
            {
                try
                {
                    var serverProduct = await _online.CreateAsync(product);
                    await _offline.UpdateOnlineIdAsync(
                        product.Id,
                        serverProduct.OnlineId,
                        serverProduct.VariantId
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"?? Failed to upload product {product.Name}: {ex.Message}");
                }
            }

            // 2?? Pull server products
            var serverProducts = await _online.GetAllAsync();

            if (replaceLocalData)
                await _offline.ClearAsync();

            // 3?? Merge by OnlineId
            await _offline.AddOrUpdateRangeAsync(serverProducts);
        }
    }
}
