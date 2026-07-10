using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;
using ZXing;
using ZXing.Common;

namespace Xavissa.Backend.Services
{
    public class ProductService
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;
        private bool? _productStoreAssignmentsAvailable;

        public ProductService(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        public List<Product> GetAllProducts(int? tenantId = null, int? storeId = null)
        {
            var query = BuildCatalogQuery(tenantId, storeId);

            if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId.Value);

            if (storeId.HasValue)
            {
                query = query.Where(p =>
                    p.StoreAssignments.Any(a => a.StoreId == storeId.Value && a.IsActive)
                );
            }

            return query.OrderBy(p => p.Name).ToList();
        }

        public List<ProductReadDto> GetProductDtos(int? tenantId = null, int? storeId = null)
        {
            var productsQuery = _db.Products
                .AsNoTracking()
                .Select(product => new
                {
                    product.Id,
                    product.SyncId,
                    product.SourceDeviceId,
                    product.ClientCreatedAt,
                    product.ClientUpdatedAt,
                    product.LastSyncedAt,
                    TenantId = product.TenantId ?? 0,
                    product.CategoryId,
                    product.Code,
                    product.Name,
                    product.Description,
                    Category = product.CategoryNavigation != null ? product.CategoryNavigation.Name : string.Empty,
                    product.Brand,
                    product.IsActive,
                    product.CreatedAt,
                    product.UpdatedAt,
                    product.DeletedAt,
                });

            if (tenantId.HasValue)
                productsQuery = productsQuery.Where(product => product.TenantId == tenantId.Value);

            var products = productsQuery.OrderBy(product => product.Name).ToList();
            if (products.Count == 0)
                return new List<ProductReadDto>();

            var productIds = products.Select(product => product.Id).ToList();
            var assignmentsQuery = _db.ProductStoreAssignments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(assignment => productIds.Contains(assignment.ProductId));

            if (storeId.HasValue)
                assignmentsQuery = assignmentsQuery.Where(assignment => assignment.StoreId == storeId.Value && assignment.IsActive);

            var assignments = assignmentsQuery
                .Select(assignment => new
                {
                    assignment.Id,
                    assignment.ProductId,
                    TenantId = assignment.TenantId ?? 0,
                    assignment.StoreId,
                    assignment.IsActive,
                })
                .ToList();

            var assignmentIds = assignments.Select(assignment => assignment.Id).ToList();
            var variants = assignmentIds.Count == 0
                ? new List<ProductVariantListRow>()
                : _db.ProductVariants
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(variant => assignmentIds.Contains(variant.ProductStoreAssignmentId))
                    .Select(variant => new ProductVariantListRow
                    {
                        Id = variant.Id,
                        AssignmentId = variant.ProductStoreAssignmentId,
                        TenantId = variant.TenantId ?? 0,
                        SKU = variant.SKU,
                        Barcode = variant.Barcode,
                        Label = variant.Label,
                        Price = variant.Price,
                        IsActive = variant.IsActive,
                    })
                    .ToList();

            var variantIds = variants.Select(variant => variant.Id).ToList();
            var stockByVariantAndStore = variantIds.Count == 0
                ? new Dictionary<(int VariantId, int StoreId), int>()
                : _db.StockLevels
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(stock => variantIds.Contains(stock.VariantId))
                    .Select(stock => new { stock.VariantId, stock.StoreId, stock.QuantityOnHand })
                    .ToList()
                    .GroupBy(stock => (stock.VariantId, stock.StoreId))
                    .ToDictionary(group => group.Key, group => group.Sum(stock => stock.QuantityOnHand));

            var assignmentsByProduct = assignments.GroupBy(assignment => assignment.ProductId).ToDictionary(group => group.Key, group => group.ToList());
            var variantsByAssignment = variants.GroupBy(variant => variant.AssignmentId).ToDictionary(group => group.Key, group => group.ToList());

            return products.Select(product =>
            {
                assignmentsByProduct.TryGetValue(product.Id, out var productAssignments);
                var assignment = storeId.HasValue
                    ? productAssignments?.FirstOrDefault(a => a.StoreId == storeId.Value)
                    : productAssignments?.OrderBy(a => a.StoreId).FirstOrDefault();

                var assignmentVariants = assignment != null && variantsByAssignment.TryGetValue(assignment.Id, out var foundVariants)
                    ? foundVariants
                    : new List<ProductVariantListRow>();
                var variant = assignmentVariants.FirstOrDefault(v => v.IsActive) ?? assignmentVariants.FirstOrDefault();
                var resolvedStoreId = storeId ?? assignment?.StoreId ?? 0;

                return new ProductReadDto
                {
                    Id = product.Id,
                    OnlineId = product.Id,
                    SyncId = product.SyncId,
                    SourceDeviceId = product.SourceDeviceId,
                    ClientCreatedAt = product.ClientCreatedAt,
                    ClientUpdatedAt = product.ClientUpdatedAt,
                    LastSyncedAt = product.LastSyncedAt,
                    TenantId = product.TenantId,
                    StoreId = resolvedStoreId,
                    AssignmentId = assignment?.Id ?? 0,
                    VariantId = variant?.Id ?? 0,
                    CategoryId = product.CategoryId,
                    Code = product.Code,
                    Barcode = variant?.Barcode ?? string.Empty,
                    Name = product.Name,
                    SKU = variant?.SKU ?? string.Empty,
                    Label = variant?.Label,
                    Description = product.Description,
                    Category = product.Category,
                    Brand = product.Brand,
                    Price = variant?.Price ?? 0,
                    StockQuantity = variant == null ? 0 : stockByVariantAndStore.GetValueOrDefault((variant.Id, resolvedStoreId)),
                    IsActive = product.IsActive && (assignment?.IsActive ?? true) && (variant?.IsActive ?? true),
                    VariantCount = assignmentVariants.Count(v => v.IsActive),
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt,
                    DeletedAt = product.DeletedAt,
                };
            }).ToList();
        }

        public List<ProductReadDto> GetSellableProductDtos(int storeId, int? tenantId = null)
        {
            return GetProductDtos(tenantId, storeId)
                .Where(product => product.IsActive && product.AssignmentId > 0 && product.VariantId > 0)
                .ToList();
        }

        public List<string> GetAllCategories(int? tenantId = null)
        {
            var query = _db.Categories.AsNoTracking().Where(c => c.IsActive);
            if (tenantId.HasValue)
                query = query.Where(c => c.TenantId == tenantId.Value);

            return query
                .Select(c => c.Name)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public List<CategoryReadDto> GetCategories(int tenantId)
        {
            EnsureTenantAccess(tenantId);

            var categories = _db.Categories
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    Id = c.Id,
                    TenantId = c.TenantId ?? tenantId,
                    Name = c.Name,
                    IsActive = c.IsActive,
                })
                .ToList();

            var categoryIds = categories.Select(c => c.Id).ToList();
            var productCounts = _db.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p =>
                    p.TenantId == tenantId
                    && p.IsActive
                    && p.CategoryId.HasValue
                    && categoryIds.Contains(p.CategoryId.Value)
                )
                .GroupBy(p => p.CategoryId!.Value)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    Count = group.Count(),
                })
                .ToDictionary(x => x.CategoryId, x => x.Count);

            return categories.Select(category => new CategoryReadDto
            {
                Id = category.Id,
                TenantId = category.TenantId,
                Name = category.Name,
                IsActive = category.IsActive,
                ProductCount = productCounts.GetValueOrDefault(category.Id),
            }).ToList();
        }

        public Category SaveCategory(int tenantId, int? categoryId, SaveCategoryDto dto)
        {
            EnsureTenantManagement(tenantId);

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Category name is required.");

            var normalizedName = dto.Name.Trim();
            var existingByName = _db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefault(c =>
                    c.TenantId == tenantId
                    && c.Name.ToLower() == normalizedName.ToLower()
                    && (!categoryId.HasValue || c.Id != categoryId.Value));

            if (existingByName != null)
                throw new InvalidOperationException("A category with the same name already exists.");

            Category category;
            if (categoryId.HasValue)
            {
                category = _db.Categories
                    .IgnoreQueryFilters()
                    .FirstOrDefault(c => c.Id == categoryId.Value && c.TenantId == tenantId)
                    ?? throw new ArgumentException("Category not found.");
            }
            else
            {
                category = new Category
                {
                    TenantId = tenantId,
                };
                _db.Categories.Add(category);
            }

            category.Name = normalizedName;
            category.IsActive = dto.IsActive;
            _db.SaveChanges();
            return category;
        }

        public void DeactivateCategory(int tenantId, int categoryId)
        {
            EnsureTenantManagement(tenantId);

            var category = _db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefault(c => c.Id == categoryId && c.TenantId == tenantId)
                ?? throw new ArgumentException("Category not found.");

            category.IsActive = false;
            _db.SaveChanges();
        }

        public Product? GetProductById(int id, int? tenantId = null, int? storeId = null)
        {
            var query = BuildCatalogQuery(tenantId, storeId).Where(p => p.Id == id);
            if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId.Value);

            return query.FirstOrDefault();
        }

        public Product? GetProductByBarcode(string barcode, int? tenantId = null, int? storeId = null)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;

            var normalizedBarcode = barcode.Trim();
            var variantQuery = _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .ThenInclude(p => p.CategoryNavigation)
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .ThenInclude(p => p.StoreAssignments)
                .ThenInclude(a => a.Variants)
                .ThenInclude(child => child.StockLevels)
                .Where(v =>
                    v.Barcode == normalizedBarcode
                    && v.IsActive == true
                    && v.ProductStoreAssignment != null
                    && v.ProductStoreAssignment.IsActive
                    && v.ProductStoreAssignment.Product.IsActive
                    && (!tenantId.HasValue || v.TenantId == tenantId.Value)
                );

            if (storeId.HasValue)
                variantQuery = variantQuery.Where(v => v.ProductStoreAssignment!.StoreId == storeId.Value);

            return variantQuery.Select(v => v.ProductStoreAssignment!.Product).FirstOrDefault();
        }

        public void AddProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Product name is required.");
            if (!product.TenantId.HasValue)
                throw new ArgumentException("Tenant is required.");

            EnsureTenantManagement(product.TenantId.Value);

            if (product.CategoryId.HasValue)
            {
                ValidateCategory(product.TenantId.Value, product.CategoryId.Value, true);
            }

            product.Name = product.Name.Trim();
            product.Description = product.Description?.Trim() ?? string.Empty;
            product.Code = string.IsNullOrWhiteSpace(product.Code) ? GenerateProductCode(product.Name) : product.Code;

            _db.Products.Add(product);
            _db.SaveChanges();
        }

        public void UpdateProduct(Product product)
        {
            var existingProduct = _db.Products
                .IgnoreQueryFilters()
                .Include(p => p.CategoryNavigation)
                .FirstOrDefault(p => p.Id == product.Id)
                ?? throw new ArgumentException($"Product with ID {product.Id} not found.");

            if (!existingProduct.TenantId.HasValue)
                throw new InvalidOperationException("Product tenant is required.");

            EnsureTenantManagement(existingProduct.TenantId.Value);

            if (product.CategoryId.HasValue)
            {
                ValidateCategory(existingProduct.TenantId.Value, product.CategoryId.Value, true);
            }

            existingProduct.Name = string.IsNullOrWhiteSpace(product.Name)
                ? existingProduct.Name
                : product.Name.Trim();
            existingProduct.Description = product.Description?.Trim() ?? string.Empty;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.Brand = product.Brand?.Trim();
            existingProduct.IsActive = product.IsActive;
            existingProduct.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
        }

        public List<ProductStoreAssignmentDto> GetProductStoreAssignments(int productId)
        {
            var product = _db.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == productId)
                ?? throw new ArgumentException($"Product with ID {productId} not found.");

            if (product.TenantId.HasValue)
                EnsureTenantAccess(product.TenantId.Value);

            var assignments = _db.ProductStoreAssignments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Store)
                .Where(x => x.ProductId == productId)
                .OrderBy(x => x.Store.Name)
                .ToList();

            if (assignments.Count == 0)
                return new List<ProductStoreAssignmentDto>();

            var assignmentIds = assignments.Select(x => x.Id).ToList();
            var variants = _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(v => assignmentIds.Contains(v.ProductStoreAssignmentId) && v.IsActive == true)
                .Select(v => new
                {
                    v.Id,
                    v.ProductStoreAssignmentId,
                    Price = v.Price ?? 0,
                })
                .ToList();

            var variantIds = variants.Select(v => v.Id).ToList();
            var stockByVariantAndStore = _db.StockLevels
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(sl => variantIds.Contains(sl.VariantId))
                .Select(sl => new { sl.VariantId, sl.StoreId, sl.QuantityOnHand })
                .ToList()
                .GroupBy(sl => (sl.VariantId, sl.StoreId))
                .ToDictionary(group => group.Key, group => group.Sum(x => x.QuantityOnHand));

            return assignments.Select(assignment =>
            {
                var assignmentVariants = variants
                    .Where(v => v.ProductStoreAssignmentId == assignment.Id)
                    .ToList();

                return new ProductStoreAssignmentDto
                {
                    Id = assignment.Id,
                    ProductId = assignment.ProductId,
                    TenantId = assignment.TenantId ?? 0,
                    StoreId = assignment.StoreId,
                    StoreName = assignment.Store.Name,
                    Price = assignmentVariants
                        .OrderBy(v => v.Price)
                        .Select(v => v.Price)
                        .FirstOrDefault(),
                    StockQuantity = assignmentVariants
                        .Select(v => stockByVariantAndStore.GetValueOrDefault((v.Id, assignment.StoreId)))
                        .Sum(),
                    IsActive = assignment.IsActive,
                    VariantCount = assignmentVariants.Count,
                };
            }).ToList();
        }

        public ProductStoreAssignmentDto SaveProductStoreAssignment(
            int productId,
            int? tenantId,
            int storeId,
            bool isActive
        )
        {
            if (tenantId.HasValue)
                EnsureTenantAccess(tenantId.Value);

            var product = _db.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == productId)
                ?? throw new ArgumentException($"Product with ID {productId} not found.");

            var resolvedTenantId = product.TenantId ?? tenantId
                ?? throw new InvalidOperationException("Tenant is required.");

            EnsureTenantAccess(resolvedTenantId);
            EnsureAssignmentManagement(storeId, resolvedTenantId);

            var assignment = _db.ProductStoreAssignments
                .IgnoreQueryFilters()
                .FirstOrDefault(x => x.ProductId == productId && x.StoreId == storeId);
            if (assignment == null)
            {
                assignment = new ProductStoreAssignment
                {
                    ProductId = productId,
                    TenantId = resolvedTenantId,
                    StoreId = storeId,
                    IsActive = isActive,
                    CreatedBy = _tenantAccess.CurrentUserId,
                    UpdatedBy = _tenantAccess.CurrentUserId,
                };
                _db.ProductStoreAssignments.Add(assignment);
            }
            else
            {
                assignment.IsActive = isActive;
                assignment.TenantId = resolvedTenantId;
                assignment.UpdatedAt = DateTime.UtcNow;
                assignment.UpdatedBy = _tenantAccess.CurrentUserId;
            }

            _db.SaveChanges();

            return BuildStoreAssignmentDto(assignment);
        }

        public void RemoveProductStoreAssignment(int productId, int storeId)
        {
            var assignment = _db.ProductStoreAssignments
                .IgnoreQueryFilters()
                .FirstOrDefault(x => x.ProductId == productId && x.StoreId == storeId)
                ?? throw new ArgumentException("Store assignment not found.");

            EnsureAssignmentManagement(storeId, assignment.TenantId);

            assignment.IsActive = false;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = _tenantAccess.CurrentUserId;

            var variants = _db.ProductVariants
                .IgnoreQueryFilters()
                .Where(v => v.ProductStoreAssignmentId == assignment.Id)
                .ToList();

            foreach (var variant in variants)
            {
                variant.IsActive = false;
                variant.UpdatedAt = DateTime.UtcNow;
                variant.UpdatedBy = _tenantAccess.CurrentUserId;
            }

            _db.SaveChanges();
        }

        public List<ProductVariantReadDto> GetVariants(int productId, int storeId)
        {
            EnsureStoreAccess(storeId);

            return _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Store)
                .Include(v => v.StockLevels)
                .Where(v =>
                    v.ProductStoreAssignment != null
                    && v.ProductStoreAssignment.ProductId == productId
                    && v.ProductStoreAssignment.StoreId == storeId
                    && v.ProductStoreAssignment.IsActive
                )
                .OrderBy(v => v.Label ?? v.SKU ?? v.Id.ToString())
                .AsEnumerable()
                .Select(MapVariant)
                .ToList();
        }

        public ProductVariantReadDto SaveVariant(int productId, SaveProductVariantDto dto)
        {
            var product = _db.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == productId)
                ?? throw new ArgumentException($"Product with ID {productId} not found.");

            var assignment = ResolveAssignment(productId, dto.AssignmentId, dto.StoreId);

            var resolvedStoreId = assignment.StoreId;
            var resolvedTenantId = assignment.TenantId
                ?? product.TenantId
                ?? _tenantAccess.SelectedTenantId
                ?? throw new InvalidOperationException("Tenant is required.");

            EnsureTenantAccess(resolvedTenantId);
            EnsureAssignmentManagement(resolvedStoreId, resolvedTenantId);

            var barcode = NormalizeBarcode(dto.Barcode, dto.GenerateBarcode);
            if (!string.IsNullOrWhiteSpace(barcode))
                EnsureBarcodeIsAvailable(barcode);

            if (dto.Price <= 0)
                throw new ArgumentException("Variant price must be greater than zero.");
            if (dto.StockQuantity < 0)
                throw new ArgumentException("Stock quantity cannot be negative.");

            var variant = new ProductVariant
            {
                ProductStoreAssignmentId = assignment.Id,
                TenantId = resolvedTenantId,
                SKU = ResolveVariantSku(null, assignment, dto.SKU, dto.Label),
                Label = dto.Label?.Trim(),
                Barcode = barcode,
                Price = dto.Price,
                CostPrice = dto.CostPrice,
                IsActive = dto.IsActive,
            };

            _db.ProductVariants.Add(variant);
            _db.SaveChanges();

            SyncStockLevel(variant.Id, resolvedTenantId, resolvedStoreId, dto.StockQuantity, _tenantAccess.CurrentUserId);
            _db.SaveChanges();

            return GetVariantDto(variant.Id);
        }

        public ProductVariantReadDto UpdateVariant(int variantId, SaveProductVariantDto dto)
        {
            var variant = _db.ProductVariants
                .Include(v => v.ProductStoreAssignment)
                .FirstOrDefault(v => v.Id == variantId)
                ?? throw new ArgumentException("Variant not found.");

            EnsureAssignmentManagement(
                variant.ProductStoreAssignment?.StoreId ?? 0,
                variant.TenantId
            );

            var barcode = NormalizeBarcode(dto.Barcode, dto.GenerateBarcode);
            if (!string.IsNullOrWhiteSpace(barcode))
                EnsureBarcodeIsAvailable(barcode, variantId);

            if (dto.Price <= 0)
                throw new ArgumentException("Variant price must be greater than zero.");
            if (dto.StockQuantity < 0)
                throw new ArgumentException("Stock quantity cannot be negative.");

            variant.SKU = ResolveVariantSku(variant, variant.ProductStoreAssignment, dto.SKU, dto.Label);
            variant.Label = dto.Label?.Trim();
            variant.Barcode = barcode;
            variant.Price = dto.Price;
            variant.CostPrice = dto.CostPrice;
            variant.IsActive = dto.IsActive;
            variant.UpdatedAt = DateTime.UtcNow;
            variant.UpdatedBy = _tenantAccess.CurrentUserId;

            SyncStockLevel(
                variant.Id,
                variant.TenantId,
                variant.ProductStoreAssignment?.StoreId ?? 0,
                dto.StockQuantity,
                _tenantAccess.CurrentUserId
            );
            _db.SaveChanges();

            return GetVariantDto(variant.Id);
        }

        public void DeleteVariant(int variantId)
        {
            var variant = _db.ProductVariants.FirstOrDefault(v => v.Id == variantId)
                ?? throw new ArgumentException("Variant not found.");

            EnsureAssignmentManagement(GetStoreIdForVariant(variantId), variant.TenantId);

            variant.IsActive = false;
            variant.UpdatedAt = DateTime.UtcNow;
            variant.UpdatedBy = _tenantAccess.CurrentUserId;

            _db.SaveChanges();
        }

        public ProductVariantReadDto GenerateBarcodeForVariant(int variantId)
        {
            var variant = _db.ProductVariants.FirstOrDefault(v => v.Id == variantId)
                ?? throw new ArgumentException("Variant not found.");

            EnsureAssignmentManagement(GetStoreIdForVariant(variantId), variant.TenantId);

            variant.Barcode = GenerateUniqueBarcode();
            variant.UpdatedAt = DateTime.UtcNow;
            variant.UpdatedBy = _tenantAccess.CurrentUserId;
            _db.SaveChanges();

            return GetVariantDto(variant.Id);
        }

        public ProductVariantReadDto GetVariant(int variantId)
        {
            var variant = _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.ProductStoreAssignment)
                .FirstOrDefault(v => v.Id == variantId)
                ?? throw new ArgumentException("Variant not found.");

            EnsureStoreAccess(variant.ProductStoreAssignment?.StoreId ?? 0);
            return GetVariantDto(variantId);
        }

        public byte[] GetVariantBarcodeImage(int variantId)
        {
            var variant = _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.ProductStoreAssignment)
                .FirstOrDefault(v => v.Id == variantId)
                ?? throw new ArgumentException("Variant not found.");

            EnsureStoreAccess(variant.ProductStoreAssignment?.StoreId ?? 0);

            if (string.IsNullOrWhiteSpace(variant.Barcode))
                throw new InvalidOperationException("Variant barcode has not been assigned.");

            return GenerateBarcodeImage(variant.Barcode);
        }

        public void DeleteProduct(int id, int? tenantId = null, int? storeId = null)
        {
            var product = _db.Products
                .IgnoreQueryFilters()
                .Include(p => p.StoreAssignments)
                .ThenInclude(a => a.Variants)
                .FirstOrDefault(p => p.Id == id)
                ?? throw new ArgumentException("Product not found.");

            if (!product.TenantId.HasValue)
                throw new InvalidOperationException("Product tenant is required.");

            EnsureTenantManagement(product.TenantId.Value);

            var assignmentIds = product.StoreAssignments.Select(a => a.Id).ToList();
            var variantIds = product.StoreAssignments
                .SelectMany(a => a.Variants)
                .Select(v => v.Id)
                .Distinct()
                .ToList();

            if (
                variantIds.Count > 0
                && _db.SaleItems.IgnoreQueryFilters().Any(item =>
                    variantIds.Contains(item.VariantId)
                )
            )
            {
                throw new InvalidOperationException(
                    "This product has sales history and cannot be permanently deleted. Deactivate it instead."
                );
            }

            if (variantIds.Count > 0)
            {
                var stockLevels = _db.StockLevels
                    .IgnoreQueryFilters()
                    .Where(level => variantIds.Contains(level.VariantId))
                    .ToList();
                if (stockLevels.Count > 0)
                    _db.StockLevels.RemoveRange(stockLevels);

                var stockMovements = _db.StockMovements
                    .IgnoreQueryFilters()
                    .Where(movement => variantIds.Contains(movement.VariantId))
                    .ToList();
                if (stockMovements.Count > 0)
                    _db.StockMovements.RemoveRange(stockMovements);

                var variants = _db.ProductVariants
                    .IgnoreQueryFilters()
                    .Where(variant => variantIds.Contains(variant.Id))
                    .ToList();
                if (variants.Count > 0)
                    _db.ProductVariants.RemoveRange(variants);
            }

            if (assignmentIds.Count > 0)
            {
                var assignments = _db.ProductStoreAssignments
                    .IgnoreQueryFilters()
                    .Where(assignment => assignmentIds.Contains(assignment.Id))
                    .ToList();
                if (assignments.Count > 0)
                    _db.ProductStoreAssignments.RemoveRange(assignments);
            }

            _db.Products.Remove(product);
            _db.SaveChanges();
        }

        public byte[] GenerateBarcodeImage(string barcode)
        {
            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height = 100,
                    Width = 300,
                    Margin = 2,
                },
            };

            var pixelData = writer.Write(barcode);
            using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, pixelData.Width, pixelData.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb
            );
            System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
            bitmap.UnlockBits(bitmapData);

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        private IQueryable<Product> BuildCatalogQuery(int? tenantId, int? storeId)
        {
            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.CategoryNavigation)
                .Include(p => p.StoreAssignments)
                .ThenInclude(x => x.Store)
                .Include(p => p.StoreAssignments)
                .ThenInclude(x => x.Variants)
                .ThenInclude(v => v.StockLevels)
                .AsQueryable();

            if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId.Value);

            return query;
        }

        private sealed class ProductVariantListRow
        {
            public int Id { get; init; }
            public int AssignmentId { get; init; }
            public int TenantId { get; init; }
            public string? SKU { get; init; }
            public string? Barcode { get; init; }
            public string? Label { get; init; }
            public decimal? Price { get; init; }
            public bool IsActive { get; init; }
        }

        private ProductStoreAssignment ResolveAssignment(int productId, int? assignmentId, int? storeId)
        {
            var query = _db.ProductStoreAssignments
                .Include(a => a.Product)
                .Where(a => a.ProductId == productId);

            if (assignmentId.HasValue)
            {
                return query.FirstOrDefault(a => a.Id == assignmentId.Value)
                    ?? throw new ArgumentException("Store assignment not found.");
            }

            if (storeId.HasValue)
            {
                return query.FirstOrDefault(a => a.StoreId == storeId.Value && a.IsActive)
                    ?? throw new ArgumentException("Assign the product to the store before creating variants.");
            }

            throw new ArgumentException("Store assignment is required.");
        }

        private ProductVariantReadDto GetVariantDto(int variantId)
        {
            return _db.ProductVariants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .Include(v => v.StockLevels)
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Store)
                .Where(v => v.Id == variantId)
                .AsEnumerable()
                .Select(MapVariant)
                .First();
        }

        private ProductVariantReadDto MapVariant(ProductVariant variant)
        {
            var storeName = variant.ProductStoreAssignment?.Store?.Name ?? GetStoreName(variant.StoreId);
            return new ProductVariantReadDto
            {
                Id = variant.Id,
                ProductId = variant.ProductId,
                AssignmentId = variant.ProductStoreAssignmentId,
                TenantId = variant.TenantId ?? 0,
                StoreId = variant.StoreId,
                StoreName = storeName,
                Name = variant.ProductStoreAssignment?.Product?.Name ?? string.Empty,
                Label = variant.Label,
                SKU = variant.SKU,
                Barcode = variant.Barcode ?? string.Empty,
                Price = variant.Price ?? 0,
                CostPrice = variant.CostPrice,
                StockQuantity = variant.StockLevels
                    .Where(sl => sl.StoreId == variant.StoreId)
                    .Select(sl => sl.QuantityOnHand)
                    .FirstOrDefault(),
                IsActive = variant.IsActive,
            };
        }

        private ProductStoreAssignmentDto BuildStoreAssignmentDto(ProductStoreAssignment assignment)
        {
            var storeName = GetStoreName(assignment.StoreId);
            var variantIds = _db.ProductVariants
                .IgnoreQueryFilters()
                .Where(v => v.ProductStoreAssignmentId == assignment.Id && v.IsActive == true)
                .Select(v => v.Id)
                .ToList();

            return new ProductStoreAssignmentDto
            {
                Id = assignment.Id,
                ProductId = assignment.ProductId,
                TenantId = assignment.TenantId ?? 0,
                StoreId = assignment.StoreId,
                StoreName = storeName,
                Price = _db.ProductVariants
                    .Where(v => variantIds.Contains(v.Id))
                    .OrderBy(v => v.Price)
                    .Select(v => v.Price ?? 0)
                    .FirstOrDefault(),
                StockQuantity = _db.StockLevels
                    .Where(sl => sl.StoreId == assignment.StoreId && variantIds.Contains(sl.VariantId))
                    .Select(sl => sl.QuantityOnHand)
                    .Sum(),
                IsActive = assignment.IsActive,
                VariantCount = variantIds.Count,
            };
        }

        private void SyncStockLevel(int variantId, int? tenantId, int storeId, int quantityOnHand, int? updatedBy)
        {
            var stockLevel = _db.StockLevels.FirstOrDefault(x => x.VariantId == variantId && x.StoreId == storeId);
            if (stockLevel == null)
            {
                stockLevel = new StockLevel
                {
                    TenantId = tenantId,
                    StoreId = storeId,
                    VariantId = variantId,
                    QuantityOnHand = quantityOnHand,
                    UpdatedBy = updatedBy,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.StockLevels.Add(stockLevel);
            }
            else
            {
                stockLevel.QuantityOnHand = quantityOnHand;
                stockLevel.UpdatedBy = updatedBy;
                stockLevel.UpdatedAt = DateTime.UtcNow;
            }
        }

        private static string GenerateBarcode(string prefix = "560")
        {
            var random = new Random();
            var code = prefix;
            for (var i = 0; i < 9; i++)
                code += random.Next(0, 10).ToString();

            var sum = 0;
            for (var i = 0; i < code.Length; i++)
            {
                var digit = int.Parse(code[i].ToString());
                sum += (i % 2 == 0) ? digit : digit * 3;
            }

            var checkDigit = (10 - (sum % 10)) % 10;
            return code + checkDigit;
        }

        private string GenerateUniqueBarcode()
        {
            var barcode = GenerateBarcode();
            while (_db.ProductVariants.IgnoreQueryFilters().Any(p => p.Barcode == barcode))
                barcode = GenerateBarcode();

            return barcode;
        }

        private string NormalizeBarcode(string? barcode, bool generateBarcode)
        {
            var normalizedBarcode = barcode?.Trim();
            if (generateBarcode || string.IsNullOrWhiteSpace(normalizedBarcode))
                return GenerateUniqueBarcode();

            return normalizedBarcode;
        }

        private string ResolveVariantSku(ProductVariant? existingVariant, ProductStoreAssignment? assignment, string? requestedSku, string? label)
        {
            var normalizedSku = requestedSku?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSku))
                return normalizedSku;

            if (existingVariant != null && !string.IsNullOrWhiteSpace(existingVariant.SKU))
                return existingVariant.SKU!;

            return GenerateUniqueVariantSku(assignment, assignment?.Product?.Name, label);
        }

        private string GenerateUniqueVariantSku(ProductStoreAssignment? assignment, string? productName, string? label)
        {
            var storeCode = assignment?.Store?.Code;
            if (string.IsNullOrWhiteSpace(storeCode) && assignment != null)
            {
                storeCode = _db.Stores
                    .Where(store => store.Id == assignment.StoreId)
                    .Select(store => store.Code)
                    .FirstOrDefault();
            }

            var storePart = SlugSkuPart(storeCode, 6, "STORE");
            var productPart = SlugSkuPart(productName, 8, "PRODUCT");
            var attributePart = SlugSkuPart(label, 8, "STD");

            var baseSku = $"{storePart}-{productPart}-{attributePart}";
            var sku = baseSku;
            var suffix = 2;

            while (_db.ProductVariants.IgnoreQueryFilters().Any(variant => variant.SKU == sku))
            {
                sku = $"{baseSku}-{suffix}";
                suffix++;
            }

            return sku;
        }

        private static string SlugSkuPart(string? value, int maxLength, string fallback)
        {
            var source = string.IsNullOrWhiteSpace(value) ? fallback : value;

            var words = new string(source
                    .ToUpperInvariant()
                    .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                    .ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length <= 3 ? word : word.Substring(0, 3))
                .ToArray();

            if (words.Length == 0)
                words = [fallback.Length <= 3 ? fallback.ToUpperInvariant() : fallback[..3].ToUpperInvariant()];

            var compact = string.Concat(words);
            return compact.Length <= maxLength
                ? compact
                : compact.Substring(0, maxLength);
        }

        private void EnsureBarcodeIsAvailable(string barcode, int? excludingVariantId = null)
        {
            var exists = _db.ProductVariants
                .IgnoreQueryFilters()
                .Any(v => v.Barcode == barcode && (!excludingVariantId.HasValue || v.Id != excludingVariantId.Value));

            if (exists)
                throw new InvalidOperationException("Barcode must resolve to one exact variant.");
        }

        private void ValidateCategory(int tenantId, int categoryId, bool requireActive)
        {
            var category = _db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefault(c => c.Id == categoryId && c.TenantId == tenantId)
                ?? throw new InvalidOperationException("Category does not belong to the selected tenant.");

            if (requireActive && !category.IsActive)
                throw new InvalidOperationException("Inactive categories cannot be used for new base products.");
        }

        private Category EnsureCategory(int tenantId, string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                throw new InvalidOperationException("Category is required.");

            var normalizedName = categoryName.Trim();
            var category = _db.Categories
                .IgnoreQueryFilters()
                .FirstOrDefault(c => c.TenantId == tenantId && c.Name.ToLower() == normalizedName.ToLower());

            if (category == null)
            {
                category = new Category
                {
                    TenantId = tenantId,
                    Name = normalizedName,
                    IsActive = true,
                };
                _db.Categories.Add(category);
                _db.SaveChanges();
            }
            else if (!category.IsActive)
            {
                throw new InvalidOperationException("Inactive categories cannot be used for new base products.");
            }

            return category;
        }

        private void EnsureTenantAccess(int tenantId)
        {
            if (!_tenantAccess.CanAccessTenant(tenantId))
                throw new UnauthorizedAccessException("Unauthorized tenant.");
        }

        private void EnsureTenantManagement(int tenantId)
        {
            if (!_tenantAccess.CanManageTenant(tenantId))
                throw new UnauthorizedAccessException("Unauthorized tenant.");
        }

        private void EnsureStoreAccess(int storeId)
        {
            if (!_tenantAccess.CanAccessStore(storeId))
                throw new UnauthorizedAccessException("Unauthorized store.");
        }

        private void EnsureStoreManagement(int storeId)
        {
            if (!_tenantAccess.CanManageStore(storeId))
                throw new UnauthorizedAccessException("Unauthorized store.");
        }

        private void EnsureAssignmentManagement(int storeId, int? tenantId)
        {
            var store = _db.Stores
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == storeId)
                ?? throw new ArgumentException("Store not found.");

            if (tenantId.HasValue && store.TenantId != tenantId.Value)
                throw new InvalidOperationException(
                    "The selected store does not belong to the same tenant as the product."
                );

            if (_tenantAccess.CanManageStore(storeId))
                return;

            if (tenantId.HasValue && _tenantAccess.CanManageTenant(tenantId.Value))
                return;

            throw new UnauthorizedAccessException("Unauthorized store.");
        }

        private static string GenerateProductCode(string name)
        {
            var compact = new string(name
                .Where(char.IsLetterOrDigit)
                .Take(6)
                .ToArray())
                .ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(compact))
                compact = "PROD";

            return $"{compact}-{DateTime.UtcNow:HHmmss}";
        }

        public Product PrepareProductForSave(Product product, string? categoryName)
        {
            if (!product.TenantId.HasValue)
                throw new InvalidOperationException("Tenant is required.");

            if (!product.CategoryId.HasValue)
                product.CategoryId = EnsureCategory(product.TenantId.Value, categoryName).Id;

            return product;
        }

        private string GetStoreName(int storeId)
        {
            if (storeId <= 0)
                return string.Empty;

            return _db.Stores
                    .IgnoreQueryFilters()
                    .Where(store => store.Id == storeId)
                    .Select(store => store.Name)
                    .FirstOrDefault()
                ?? $"Store {storeId}";
        }

        private int GetStoreIdForVariant(int variantId)
        {
            return _db.ProductVariants
                .IgnoreQueryFilters()
                .Where(v => v.Id == variantId)
                .Select(v => v.ProductStoreAssignment!.StoreId)
                .First();
        }

        private bool HasProductStoreAssignmentsTable()
        {
            if (_productStoreAssignmentsAvailable.HasValue)
                return _productStoreAssignmentsAvailable.Value;

            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                    connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = current_schema()
                          AND lower(table_name) = lower('StoreProducts')
                    );
                    """;

                var result = command.ExecuteScalar();
                _productStoreAssignmentsAvailable = result is bool boolResult && boolResult;
            }
            catch
            {
                _productStoreAssignmentsAvailable = false;
            }
            finally
            {
                if (shouldClose && connection.State == ConnectionState.Open)
                    connection.Close();
            }

            return _productStoreAssignmentsAvailable.Value;
        }
    }
}
