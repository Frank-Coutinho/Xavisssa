using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class ProductRepositoryOffline : IProductRepositoryOffline
    {
        private readonly IDbContextFactory<LocalDbContext> _factory;
        private readonly IDemoStateService _demoState;

        public ProductRepositoryOffline(IDbContextFactory<LocalDbContext> factory, IDemoStateService demoState)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _demoState = demoState ?? throw new ArgumentNullException(nameof(demoState));
        }

        // -------------------------
        // GET ALL
        // -------------------------
        public async Task<List<Product>> GetAllAsync()
        {
            await using var db = _factory.CreateDbContext();

            return await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        }

        // -------------------------
        // GET BY ID
        // -------------------------
        public async Task<Product?> GetByIdAsync(int id)
        {
            await using var db = _factory.CreateDbContext();

            return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Product>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var idList = ids.Where(id => id > 0).Distinct().ToList();
            if (idList.Count == 0)
                return new List<Product>();

            await using var db = _factory.CreateDbContext();

            return await db
                .Products.AsNoTracking()
                .Where(p => idList.Contains(p.Id))
                .ToListAsync();
        }

        // -------------------------
        // GET BY BARCODE
        // -------------------------
        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            await using var db = _factory.CreateDbContext();

            return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Barcode == barcode);
        }

        public async Task<SellableVariantSnapshot?> GetSellableVariantByBarcodeAsync(int storeId, string barcode)
        {
            if (storeId <= 0 || string.IsNullOrWhiteSpace(barcode))
                return null;

            await using var db = _factory.CreateDbContext();

            return await db.SellableVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(variant =>
                    variant.StoreId == storeId
                    && variant.IsSellable
                    && variant.Barcode == barcode);
        }

        public async Task<List<SellableVariantSnapshot>> GetSellableVariantsAsync(int storeId)
        {
            await using var db = _factory.CreateDbContext();
            return await db.SellableVariants
                .AsNoTracking()
                .Where(variant => variant.StoreId == storeId && variant.IsSellable)
                .OrderBy(variant => variant.ProductName)
                .ThenBy(variant => variant.VariantLabel)
                .ToListAsync();
        }

        // -------------------------
        // GET UNSYNCED
        // -------------------------
        public async Task<List<Product>> GetUnsyncedAsync()
        {
            await using var db = _factory.CreateDbContext();

            return await db
                .Products.Where(p => p.OnlineId == 0)
                .OrderBy(p => p.Id)
                .AsNoTracking()
                .ToListAsync();
        }

        // -------------------------
        // UPDATE ONLINE ID
        // -------------------------
        public async Task UpdateOnlineIdAsync(int localId, int onlineId, int variantId)
        {
            await using var db = _factory.CreateDbContext();

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == localId);
            if (product == null)
                throw new ArgumentException($"Local product {localId} not found");

            product.OnlineId = onlineId;
            product.VariantId = variantId;
            await db.SaveChangesAsync();
        }

        public async Task<List<CatalogCategory>> GetCategoriesAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync();
        }

        public async Task ReplaceCategoriesAsync(IEnumerable<CatalogCategory> categories)
        {
            var categoryList = categories?.ToList() ?? new List<CatalogCategory>();

            await using var db = _factory.CreateDbContext();
            await using var tx = await db.Database.BeginTransactionAsync();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM Categories;");
            if (categoryList.Count > 0)
                await db.Categories.AddRangeAsync(categoryList);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task<List<ProductStoreAssignment>> GetStoreAssignmentsAsync(int productId)
        {
            await using var db = _factory.CreateDbContext();
            return await db.ProductStoreAssignments
                .AsNoTracking()
                .Where(assignment => assignment.ProductId == productId)
                .OrderBy(assignment => assignment.StoreName)
                .ToListAsync();
        }

        public async Task ReplaceStoreAssignmentsAsync(int productId, IEnumerable<ProductStoreAssignment> assignments)
        {
            var assignmentList = assignments?
                .Where(assignment => assignment != null)
                .GroupBy(assignment => assignment.Id > 0
                    ? $"id:{assignment.Id}"
                    : $"product:{assignment.ProductId}|store:{assignment.StoreId}|tenant:{assignment.TenantId}")
                .Select(group => group.First())
                .ToList()
                ?? new List<ProductStoreAssignment>();

            await using var db = _factory.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProductStoreAssignments WHERE ProductId = {0};", productId);

            if (assignmentList.Count > 0)
                await db.ProductStoreAssignments.AddRangeAsync(assignmentList);

            await db.SaveChangesAsync();
        }

        public async Task<List<ProductVariantRecord>> GetVariantsAsync(int productId, int storeId)
        {
            await using var db = _factory.CreateDbContext();
            return await db.ProductVariants
                .AsNoTracking()
                .Where(variant => variant.ProductId == productId && variant.StoreId == storeId)
                .OrderBy(variant => variant.Name)
                .ThenBy(variant => variant.Label)
                .ToListAsync();
        }

        public async Task ReplaceVariantsAsync(int productId, int storeId, IEnumerable<ProductVariantRecord> variants)
        {
            var variantList = variants?
                .Where(variant => variant != null)
                .GroupBy(variant => variant.Id > 0
                    ? $"id:{variant.Id}"
                    : $"product:{variant.ProductId}|store:{variant.StoreId}|label:{variant.Label}|sku:{variant.SKU}")
                .Select(group => group.First())
                .ToList()
                ?? new List<ProductVariantRecord>();

            await using var db = _factory.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ProductVariants WHERE ProductId = {0} AND StoreId = {1};",
                productId,
                storeId);

            if (variantList.Count > 0)
                await db.ProductVariants.AddRangeAsync(variantList);

            await db.SaveChangesAsync();
        }

        public async Task UpsertSellableProductsSnapshotAsync(int storeId, IEnumerable<Product> products)
        {
            var productList = products?.ToList() ?? new List<Product>();
            if (storeId <= 0)
                return;

            await using var db = _factory.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProductVariants WHERE StoreId = {0};", storeId);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProductStoreAssignments WHERE StoreId = {0};", storeId);

            var distinctAssignments = productList
                .Where(product => product.AssignmentId > 0)
                .GroupBy(product => product.AssignmentId)
                .Select(group => group.First())
                .ToList();

            var distinctVariants = productList
                .Where(product => product.VariantId > 0)
                .GroupBy(product => product.VariantId)
                .Select(group => group.First())
                .ToList();

            foreach (var product in productList)
            {
                if (product.OnlineId > 0)
                {
                    var existingProduct = await db.Products.FirstOrDefaultAsync(candidate => candidate.OnlineId == product.OnlineId);
                    if (existingProduct == null)
                    {
                        db.Products.Add(new Product
                        {
                            OnlineId = product.OnlineId,
                            TenantId = product.TenantId,
                            StoreId = 0,
                            CategoryId = product.CategoryId,
                            Name = product.Name,
                            Barcode = string.Empty,
                            Category = product.Category,
                            Brand = product.Brand,
                            Code = product.Code,
                            Color = product.Color,
                            Size = product.Size,
                            SKU = string.Empty,
                            Label = string.Empty,
                            AttributesJson = product.AttributesJson,
                            Description = product.Description,
                            ImageUrl = product.ImageUrl,
                            CreatedAt = product.CreatedAt,
                            Price = product.Price,
                            StockQuantity = product.StockQuantity,
                            IsActive = product.IsActive,
                            VariantCount = product.VariantCount,
                            UpdatedAt = product.UpdatedAt,
                        });
                    }
                    else
                    {
                        existingProduct.TenantId = product.TenantId;
                        existingProduct.CategoryId = product.CategoryId;
                        existingProduct.Name = product.Name;
                        existingProduct.Category = product.Category;
                        existingProduct.Brand = product.Brand;
                        existingProduct.Code = product.Code;
                        existingProduct.Color = product.Color;
                        existingProduct.Size = product.Size;
                        existingProduct.AttributesJson = product.AttributesJson;
                        existingProduct.Description = product.Description;
                        existingProduct.ImageUrl = product.ImageUrl;
                        existingProduct.Price = product.Price;
                        existingProduct.StockQuantity = product.StockQuantity;
                        existingProduct.IsActive = product.IsActive;
                        existingProduct.VariantCount = Math.Max(existingProduct.VariantCount, product.VariantCount);
                        existingProduct.UpdatedAt = product.UpdatedAt;
                    }
                }

            }

            foreach (var product in distinctAssignments)
            {
                db.ProductStoreAssignments.Add(new ProductStoreAssignment
                {
                    Id = product.AssignmentId,
                    ProductId = product.OnlineId,
                    TenantId = product.TenantId,
                    StoreId = product.StoreId,
                    StoreName = string.Empty,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    IsActive = product.IsActive,
                    VariantCount = product.VariantCount,
                });
            }

            foreach (var product in distinctVariants)
            {
                db.ProductVariants.Add(new ProductVariantRecord
                {
                    Id = product.VariantId,
                    ProductId = product.OnlineId,
                    AssignmentId = product.AssignmentId,
                    TenantId = product.TenantId,
                    StoreId = product.StoreId,
                    StoreName = string.Empty,
                    Name = product.Name,
                    Label = product.Label,
                    SKU = product.SKU,
                    Barcode = product.Barcode,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    IsActive = product.IsActive,
                });
            }

            await db.SaveChangesAsync();
        }

        public async Task UpsertSellableVariantsAsync(
            int storeId,
            IEnumerable<SellableVariantSyncItemDto> variants,
            IEnumerable<int>? removedVariantIds = null)
        {
            await using var db = _factory.CreateDbContext();

            var variantList = variants?.ToList() ?? new List<SellableVariantSyncItemDto>();
            var removedIds = (removedVariantIds ?? Enumerable.Empty<int>()).Distinct().ToList();

            if (removedIds.Count > 0)
            {
                var removedSnapshots = await db.SellableVariants
                    .Where(variant => variant.StoreId == storeId && removedIds.Contains(variant.VariantId))
                    .ToListAsync();
                if (removedSnapshots.Count > 0)
                    db.SellableVariants.RemoveRange(removedSnapshots);
            }

            var variantIds = variantList.Select(variant => variant.VariantId).Distinct().ToList();
            var existing = await db.SellableVariants
                .Where(variant => variant.StoreId == storeId && variantIds.Contains(variant.VariantId))
                .ToDictionaryAsync(variant => variant.VariantId);

            foreach (var variant in variantList)
            {
                if (existing.TryGetValue(variant.VariantId, out var snapshot))
                {
                    snapshot.StoreProductId = variant.StoreProductId;
                    snapshot.ProductId = variant.ProductId;
                    snapshot.TenantId = variant.TenantId;
                    snapshot.StoreId = variant.StoreId;
                    snapshot.ProductName = variant.ProductName;
                    snapshot.VariantLabel = variant.VariantLabel ?? string.Empty;
                    snapshot.Barcode = variant.Barcode ?? string.Empty;
                    snapshot.SKU = variant.SKU ?? string.Empty;
                    snapshot.Price = variant.Price;
                    snapshot.QuantityOnHand = variant.QuantityOnHand;
                    snapshot.IsSellable = variant.IsSellable;
                    snapshot.UpdatedAt = variant.UpdatedAt;
                }
                else
                {
                    db.SellableVariants.Add(new SellableVariantSnapshot
                    {
                        VariantId = variant.VariantId,
                        StoreProductId = variant.StoreProductId,
                        ProductId = variant.ProductId,
                        TenantId = variant.TenantId,
                        StoreId = variant.StoreId,
                        ProductName = variant.ProductName,
                        VariantLabel = variant.VariantLabel ?? string.Empty,
                        Barcode = variant.Barcode ?? string.Empty,
                        SKU = variant.SKU ?? string.Empty,
                        Price = variant.Price,
                        QuantityOnHand = variant.QuantityOnHand,
                        IsSellable = variant.IsSellable,
                        UpdatedAt = variant.UpdatedAt,
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        public async Task ApplyStockLevelDeltaAsync(int storeId, IEnumerable<StockLevelDeltaItemDto> items)
        {
            var itemList = items?.Where(item => item.StoreId == storeId).ToList()
                ?? new List<StockLevelDeltaItemDto>();
            if (itemList.Count == 0)
                return;

            await using var db = _factory.CreateDbContext();

            var variantIds = itemList.Select(item => item.VariantId).Distinct().ToList();
            var snapshots = await db.SellableVariants
                .Where(variant => variant.StoreId == storeId && variantIds.Contains(variant.VariantId))
                .ToListAsync();
            var snapshotsById = snapshots.ToDictionary(snapshot => snapshot.VariantId);

            var variants = await db.ProductVariants
                .Where(variant => variant.StoreId == storeId && variantIds.Contains(variant.Id))
                .ToListAsync();
            var variantsById = variants.ToDictionary(variant => variant.Id);

            foreach (var item in itemList)
            {
                if (snapshotsById.TryGetValue(item.VariantId, out var snapshot))
                {
                    snapshot.QuantityOnHand = item.QuantityOnHand;
                    snapshot.UpdatedAt = item.UpdatedAt;
                }

                if (variantsById.TryGetValue(item.VariantId, out var variant))
                    variant.StockQuantity = item.QuantityOnHand;
            }

            await db.SaveChangesAsync();
        }

        public async Task UpsertCatalogDeltaAsync(
            IEnumerable<CategoryDeltaItemDto> categories,
            IEnumerable<ProductCatalogDeltaItemDto> products,
            IEnumerable<StoreProductDeltaItemDto> storeProducts,
            IEnumerable<ProductVariantDeltaItemDto> productVariants)
        {
            await using var db = _factory.CreateDbContext();

            var storeProductList = storeProducts?.ToList() ?? new List<StoreProductDeltaItemDto>();
            var variantList = productVariants?.ToList() ?? new List<ProductVariantDeltaItemDto>();

            foreach (var category in categories ?? Enumerable.Empty<CategoryDeltaItemDto>())
            {
                var existing = category.SyncId != Guid.Empty
                    ? await db.Categories.FirstOrDefaultAsync(x => x.SyncId == category.SyncId)
                    : await db.Categories.FindAsync(category.Id);
                if (category.DeletedAt.HasValue || !category.IsActive)
                {
                    if (existing != null)
                        db.Categories.Remove(existing);
                    continue;
                }

                if (existing == null)
                {
                    db.Categories.Add(new CatalogCategory
                    {
                        Id = category.Id,
                        OnlineId = category.OnlineId > 0 ? category.OnlineId : category.Id,
                        SyncId = category.SyncId == Guid.Empty ? Guid.NewGuid() : category.SyncId,
                        SourceDeviceId = category.SourceDeviceId,
                        ClientCreatedAt = category.ClientCreatedAt,
                        ClientUpdatedAt = category.ClientUpdatedAt,
                        LastSyncedAt = category.LastSyncedAt ?? DateTimeOffset.UtcNow,
                        TenantId = category.TenantId,
                        Name = category.Name,
                        IsActive = category.IsActive,
                    });
                }
                else
                {
                    existing.TenantId = category.TenantId;
                    existing.OnlineId = category.OnlineId > 0 ? category.OnlineId : category.Id;
                    existing.SyncId = category.SyncId == Guid.Empty ? existing.SyncId : category.SyncId;
                    existing.SourceDeviceId = category.SourceDeviceId;
                    existing.ClientCreatedAt = category.ClientCreatedAt;
                    existing.ClientUpdatedAt = category.ClientUpdatedAt;
                    existing.LastSyncedAt = category.LastSyncedAt ?? DateTimeOffset.UtcNow;
                    existing.Name = category.Name;
                    existing.IsActive = category.IsActive;
                }
            }

            foreach (var product in products ?? Enumerable.Empty<ProductCatalogDeltaItemDto>())
            {
                var existing = product.SyncId != Guid.Empty
                    ? await db.Products.FirstOrDefaultAsync(x => x.SyncId == product.SyncId)
                    : await db.Products.FirstOrDefaultAsync(x => x.OnlineId == product.Id || x.Id == product.Id);
                if (product.DeletedAt.HasValue || !product.IsActive)
                {
                    if (existing != null)
                        db.Products.Remove(existing);
                    continue;
                }

                if (existing == null)
                {
                    db.Products.Add(new Product
                    {
                        OnlineId = product.Id,
                        SyncId = product.SyncId == Guid.Empty ? Guid.NewGuid() : product.SyncId,
                        SourceDeviceId = product.SourceDeviceId,
                        ClientCreatedAt = product.ClientCreatedAt,
                        ClientUpdatedAt = product.ClientUpdatedAt,
                        LastSyncedAt = product.LastSyncedAt ?? DateTimeOffset.UtcNow,
                        TenantId = product.TenantId,
                        CategoryId = product.CategoryId,
                        Name = product.Name,
                        Description = product.Description,
                        Code = product.Code,
                        Brand = product.Brand ?? string.Empty,
                        IsActive = product.IsActive,
                        UpdatedAt = product.UpdatedAt ?? DateTime.UtcNow,
                    });
                }
                else
                {
                    existing.TenantId = product.TenantId;
                    existing.OnlineId = product.OnlineId > 0 ? product.OnlineId : product.Id;
                    existing.SyncId = product.SyncId == Guid.Empty ? existing.SyncId : product.SyncId;
                    existing.SourceDeviceId = product.SourceDeviceId;
                    existing.ClientCreatedAt = product.ClientCreatedAt;
                    existing.ClientUpdatedAt = product.ClientUpdatedAt;
                    existing.LastSyncedAt = product.LastSyncedAt ?? DateTimeOffset.UtcNow;
                    existing.CategoryId = product.CategoryId;
                    existing.Name = product.Name;
                    existing.Description = product.Description;
                    existing.Code = product.Code;
                    existing.Brand = product.Brand ?? string.Empty;
                    existing.IsActive = product.IsActive;
                    existing.UpdatedAt = product.UpdatedAt ?? existing.UpdatedAt;
                }
            }

            foreach (var assignment in storeProductList)
            {
                var existing = assignment.SyncId != Guid.Empty
                    ? await db.ProductStoreAssignments.FirstOrDefaultAsync(x => x.SyncId == assignment.SyncId)
                    : await db.ProductStoreAssignments.FindAsync(assignment.Id);
                if (assignment.DeletedAt.HasValue || !assignment.IsActive)
                {
                    if (existing != null)
                        db.ProductStoreAssignments.Remove(existing);
                    continue;
                }

                if (existing == null)
                {
                    db.ProductStoreAssignments.Add(new ProductStoreAssignment
                    {
                        Id = assignment.Id,
                        OnlineId = assignment.OnlineId > 0 ? assignment.OnlineId : assignment.Id,
                        SyncId = assignment.SyncId == Guid.Empty ? Guid.NewGuid() : assignment.SyncId,
                        SourceDeviceId = assignment.SourceDeviceId,
                        ClientCreatedAt = assignment.ClientCreatedAt,
                        ClientUpdatedAt = assignment.ClientUpdatedAt,
                        LastSyncedAt = assignment.LastSyncedAt ?? DateTimeOffset.UtcNow,
                        ProductId = assignment.ProductId,
                        TenantId = assignment.TenantId ?? 0,
                        StoreId = assignment.StoreId,
                        IsActive = assignment.IsActive,
                    });
                }
                else
                {
                    existing.ProductId = assignment.ProductId;
                    existing.OnlineId = assignment.OnlineId > 0 ? assignment.OnlineId : assignment.Id;
                    existing.SyncId = assignment.SyncId == Guid.Empty ? existing.SyncId : assignment.SyncId;
                    existing.SourceDeviceId = assignment.SourceDeviceId;
                    existing.ClientCreatedAt = assignment.ClientCreatedAt;
                    existing.ClientUpdatedAt = assignment.ClientUpdatedAt;
                    existing.LastSyncedAt = assignment.LastSyncedAt ?? DateTimeOffset.UtcNow;
                    existing.TenantId = assignment.TenantId ?? 0;
                    existing.StoreId = assignment.StoreId;
                    existing.IsActive = assignment.IsActive;
                }
            }

            var assignmentIds = variantList.Select(variant => variant.StoreProductId).Distinct().ToList();
            var assignmentsById = await db.ProductStoreAssignments
                .Where(assignment => assignmentIds.Contains(assignment.Id))
                .ToDictionaryAsync(assignment => assignment.Id);

            foreach (var variant in variantList)
            {
                var existing = variant.SyncId != Guid.Empty
                    ? await db.ProductVariants.FirstOrDefaultAsync(x => x.SyncId == variant.SyncId)
                    : await db.ProductVariants.FindAsync(variant.Id);
                if (variant.DeletedAt.HasValue || !variant.IsActive)
                {
                    if (existing != null)
                        db.ProductVariants.Remove(existing);
                    continue;
                }

                assignmentsById.TryGetValue(variant.StoreProductId, out var assignment);

                if (existing == null)
                {
                    db.ProductVariants.Add(new ProductVariantRecord
                    {
                        Id = variant.Id,
                        OnlineId = variant.OnlineId > 0 ? variant.OnlineId : variant.Id,
                        SyncId = variant.SyncId == Guid.Empty ? Guid.NewGuid() : variant.SyncId,
                        SourceDeviceId = variant.SourceDeviceId,
                        ClientCreatedAt = variant.ClientCreatedAt,
                        ClientUpdatedAt = variant.ClientUpdatedAt,
                        LastSyncedAt = variant.LastSyncedAt ?? DateTimeOffset.UtcNow,
                        ProductId = assignment?.ProductId ?? 0,
                        AssignmentId = variant.StoreProductId,
                        TenantId = variant.TenantId ?? 0,
                        StoreId = assignment?.StoreId ?? 0,
                        SKU = variant.SKU ?? string.Empty,
                        Barcode = variant.Barcode ?? string.Empty,
                        Description = variant.Description,
                        Price = variant.Price ?? 0,
                        IsActive = variant.IsActive,
                        Label = variant.Label ?? string.Empty,
                    });
                }
                else
                {
                    existing.ProductId = assignment?.ProductId ?? existing.ProductId;
                    existing.OnlineId = variant.OnlineId > 0 ? variant.OnlineId : variant.Id;
                    existing.SyncId = variant.SyncId == Guid.Empty ? existing.SyncId : variant.SyncId;
                    existing.SourceDeviceId = variant.SourceDeviceId;
                    existing.ClientCreatedAt = variant.ClientCreatedAt;
                    existing.ClientUpdatedAt = variant.ClientUpdatedAt;
                    existing.LastSyncedAt = variant.LastSyncedAt ?? DateTimeOffset.UtcNow;
                    existing.AssignmentId = variant.StoreProductId;
                    existing.TenantId = variant.TenantId ?? 0;
                    existing.StoreId = assignment?.StoreId ?? existing.StoreId;
                    existing.SKU = variant.SKU ?? string.Empty;
                    existing.Barcode = variant.Barcode ?? string.Empty;
                    existing.Description = variant.Description;
                    existing.Price = variant.Price ?? 0;
                    existing.IsActive = variant.IsActive;
                    existing.Label = variant.Label ?? string.Empty;
                }
            }

            await db.SaveChangesAsync();
        }

        public async Task<DateTime?> GetCursorAsync(string key)
        {
            await using var db = _factory.CreateDbContext();
            return await db.SyncCursors
                .Where(cursor => cursor.Key == key)
                .Select(cursor => cursor.Value)
                .FirstOrDefaultAsync();
        }

        public async Task SetCursorAsync(string key, DateTime? value)
        {
            await using var db = _factory.CreateDbContext();
            var cursor = await db.SyncCursors.FindAsync(key);
            if (cursor == null)
            {
                db.SyncCursors.Add(new SyncCursor
                {
                    Key = key,
                    Value = value,
                });
            }
            else
            {
                cursor.Value = value;
            }

            await db.SaveChangesAsync();
        }

        public async Task SaveAsync()
        {
            await using var db = _factory.CreateDbContext();
        }

        // -------------------------
        // UPSERT SINGLE
        // -------------------------
        public async Task AddOrUpdateAsync(Product product)
        {
            await EnsureDemoCanWriteAsync();
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            await using var db = _factory.CreateDbContext();

            var normalized = NormalizeBaseProduct(product);

            Product? existing = null;

            if (normalized.OnlineId > 0)
            {
                existing = await db.Products.FirstOrDefaultAsync(p =>
                    p.OnlineId == normalized.OnlineId
                );
            }

            if (existing == null)
            {
                db.Products.Add(normalized);
            }
            else
            {
                existing.TenantId = normalized.TenantId;
                existing.StoreId = normalized.StoreId;
                existing.CategoryId = normalized.CategoryId;
                existing.Name = normalized.Name;
                existing.AssignmentId = normalized.AssignmentId;
                existing.VariantId = normalized.VariantId;
                existing.Barcode = normalized.Barcode;
                existing.Category = normalized.Category;
                existing.Brand = normalized.Brand;
                existing.Code = normalized.Code;
                existing.Color = normalized.Color;
                existing.Size = normalized.Size;
                existing.SKU = normalized.SKU;
                existing.Label = normalized.Label;
                existing.AttributesJson = normalized.AttributesJson;
                existing.Description = normalized.Description;
                existing.ImageUrl = normalized.ImageUrl;
                existing.CreatedAt = normalized.CreatedAt;
                existing.Price = normalized.Price;
                existing.StockQuantity = normalized.StockQuantity;
                existing.IsActive = normalized.IsActive;
                existing.VariantCount = normalized.VariantCount;
                existing.UpdatedAt = normalized.UpdatedAt;
            }

            await db.SaveChangesAsync();
        }

        // -------------------------
        // DECREASE STOCK
        // -------------------------
        public async Task DecreaseStockAsync(int productId, int quantity)
        {
            await EnsureDemoCanWriteAsync();
            await using var db = _factory.CreateDbContext();

            var product = await db.Products.FirstAsync(p => p.Id == productId);

            product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);

            await db.SaveChangesAsync();
        }

        public async Task DecreaseStockRangeAsync(IEnumerable<(int ProductId, int Quantity)> items)
        {
            await EnsureDemoCanWriteAsync();
            var itemList = items.Where(item => item.ProductId > 0 && item.Quantity > 0).ToList();
            if (itemList.Count == 0)
                return;

            await using var db = _factory.CreateDbContext();

            var productIds = itemList.Select(item => item.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
            var productsById = products.ToDictionary(p => p.Id);

            foreach (var item in itemList)
            {
                if (!productsById.TryGetValue(item.ProductId, out var product))
                    throw new InvalidOperationException($"Product {item.ProductId} was not found while updating stock.");

                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
            }

            await db.SaveChangesAsync();
        }

        public async Task DecreaseSellableVariantStockRangeAsync(IEnumerable<(int VariantId, int Quantity)> items)
        {
            await EnsureDemoCanWriteAsync();
            var itemList = items.Where(item => item.VariantId > 0 && item.Quantity > 0).ToList();
            if (itemList.Count == 0)
                return;

            await using var db = _factory.CreateDbContext();

            var variantIds = itemList.Select(item => item.VariantId).Distinct().ToList();
            var variants = await db.SellableVariants
                .Where(variant => variantIds.Contains(variant.VariantId))
                .ToListAsync();
            var variantsById = variants.ToDictionary(variant => variant.VariantId);

            foreach (var item in itemList)
            {
                if (!variantsById.TryGetValue(item.VariantId, out var variant))
                    continue;

                variant.QuantityOnHand = Math.Max(0, variant.QuantityOnHand - item.Quantity);
                variant.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }

        // -------------------------
        // UPSERT MANY (used by sync)
        // -------------------------
        public async Task AddOrUpdateRangeAsync(IEnumerable<Product> products)
        {
            var productList = products.ToList();
            if (!productList.Any())
                return;

            await using var db = _factory.CreateDbContext();

            var normalizedProducts = productList.Select(NormalizeBaseProduct).ToList();
            var onlineIds = normalizedProducts.Where(p => p.OnlineId > 0).Select(p => p.OnlineId).ToList();

            var existingProducts = await db
                .Products.Where(p => onlineIds.Contains(p.OnlineId))
                .ToDictionaryAsync(p => p.OnlineId);

            foreach (var product in normalizedProducts)
            {
                if (existingProducts.TryGetValue(product.OnlineId, out var local))
                {
                    local.TenantId = product.TenantId;
                    local.StoreId = product.StoreId;
                    local.CategoryId = product.CategoryId;
                    local.Name = product.Name;
                    local.AssignmentId = product.AssignmentId;
                    local.VariantId = product.VariantId;
                    local.Barcode = product.Barcode;
                    local.Category = product.Category;
                    local.Brand = product.Brand;
                    local.Code = product.Code;
                    local.Color = product.Color;
                    local.Size = product.Size;
                    local.SKU = product.SKU;
                    local.Label = product.Label;
                    local.AttributesJson = product.AttributesJson;
                    local.Description = product.Description;
                    local.ImageUrl = product.ImageUrl;
                    local.CreatedAt = product.CreatedAt;
                    local.Price = product.Price;
                    local.StockQuantity = product.StockQuantity;
                    local.IsActive = product.IsActive;
                    local.VariantCount = product.VariantCount;
                    local.UpdatedAt = product.UpdatedAt;
                }
                else
                {
                    db.Products.Add(
                        new Product
                        {
                            OnlineId = product.OnlineId,
                            TenantId = product.TenantId,
                            StoreId = product.StoreId,
                            AssignmentId = product.AssignmentId,
                            VariantId = product.VariantId,
                            CategoryId = product.CategoryId,
                            Name = product.Name,
                            Barcode = product.Barcode,
                            Category = product.Category,
                            Brand = product.Brand,
                            Code = product.Code,
                            Color = product.Color,
                            Size = product.Size,
                            SKU = product.SKU,
                            Label = product.Label,
                            AttributesJson = product.AttributesJson,
                            Description = product.Description,
                            ImageUrl = product.ImageUrl,
                            CreatedAt = product.CreatedAt,
                            Price = product.Price,
                            StockQuantity = product.StockQuantity,
                            IsActive = product.IsActive,
                            VariantCount = product.VariantCount,
                            UpdatedAt = product.UpdatedAt,
                        }
                    );
                }
            }

            await db.SaveChangesAsync();
        }

        private static Product NormalizeBaseProduct(Product product)
        {
            if (product.VariantId <= 0)
                return product;

            return new Product
            {
                Id = product.Id,
                OnlineId = product.OnlineId,
                TenantId = product.TenantId,
                StoreId = 0,
                AssignmentId = 0,
                VariantId = 0,
                CategoryId = product.CategoryId,
                Code = product.Code,
                Barcode = string.Empty,
                Name = product.Name,
                Color = product.Color,
                Size = product.Size,
                SKU = string.Empty,
                Label = string.Empty,
                AttributesJson = product.AttributesJson,
                ImageUrl = product.ImageUrl,
                Description = product.Description,
                Category = product.Category,
                Brand = product.Brand,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive,
                VariantCount = product.VariantCount,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
            };
        }

        // -------------------------
        // DELETE
        // -------------------------
        public async Task DeleteAsync(int id)
        {
            await EnsureDemoCanWriteAsync();
            await using var db = _factory.CreateDbContext();

            var entity = await db.Products.FindAsync(id);
            if (entity == null)
                return;

            db.Products.Remove(entity);
            await db.SaveChangesAsync();
        }

        // -------------------------
        // CLEAR ALL (server authoritative sync)
        // -------------------------
        public async Task ClearAsync()
        {
            await EnsureDemoCanWriteAsync();
            await using var db = _factory.CreateDbContext();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM Products;");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Categories;");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProductStoreAssignments;");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ProductVariants;");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM SellableVariants;");
        }

        private async Task EnsureDemoCanWriteAsync()
        {
            await _demoState.CheckExpirationAsync();
            if (_demoState.IsExpired)
                throw new InvalidOperationException("Demo session expired. Activate a license to use this in production.");
        }
    }
}
