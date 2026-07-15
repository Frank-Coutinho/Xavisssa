using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Backend.Services;
using Xavissa.Backend.Utilities;
using Xavissa.Database.Models;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/v1/products")]
    [Authorize]
    public class ProductController : ControllerBase
    {
        private readonly ProductService _productService;
        private readonly TenantAccessService _tenantAccess;

        public ProductController(ProductService productService, TenantAccessService tenantAccess)
        {
            _productService = productService;
            _tenantAccess = tenantAccess;
        }

        [HttpGet]
        public IActionResult GetAllProducts()
        {
            return Ok(_productService.GetProductDtos(_tenantAccess.SelectedTenantId, _tenantAccess.SelectedStoreId));
        }

        [HttpGet("sellable")]
        public IActionResult GetSellableProducts([FromQuery] int? storeId = null)
        {
            var resolvedStoreId = storeId ?? _tenantAccess.SelectedStoreId;
            if (!resolvedStoreId.HasValue)
                return BadRequest("A selected store is required.");

            return Ok(_productService.GetSellableProductDtos(resolvedStoreId.Value, _tenantAccess.SelectedTenantId));
        }

        [HttpGet("catalog")]
        public IActionResult GetCatalogProducts()
        {
            var tenantId = _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("A selected tenant is required.");

            return Ok(_productService.GetProductDtos(tenantId, _tenantAccess.SelectedStoreId));
        }

        [HttpGet("{id:int}")]
        public IActionResult GetProduct(int id)
        {
            var product = _productService.GetProductById(id, _tenantAccess.SelectedTenantId, _tenantAccess.SelectedStoreId);
            return product == null ? NotFound() : Ok(MapProduct(product, _tenantAccess.SelectedStoreId));
        }

        [HttpGet("barcode/{barcode}")]
        public IActionResult GetProductByBarcode(string barcode)
        {
            var product = _productService.GetProductByBarcode(barcode, _tenantAccess.SelectedTenantId, _tenantAccess.SelectedStoreId);
            return product == null ? NotFound() : Ok(MapProduct(product, _tenantAccess.SelectedStoreId));
        }

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            var tenantId = _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("A selected tenant is required.");

            return Ok(_productService.GetCategories(tenantId.Value));
        }

        [HttpPost("categories")]
        public IActionResult CreateCategory([FromBody] SaveCategoryDto dto)
        {
            var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("Tenant is required.");

            try
            {
                var category = _productService.SaveCategory(tenantId.Value, null, dto);
                return Ok(new CategoryReadDto
                {
                    Id = category.Id,
                    TenantId = category.TenantId ?? tenantId.Value,
                    Name = category.Name,
                    IsActive = category.IsActive,
                    ProductCount = 0,
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("categories/{id:int}")]
        public IActionResult UpdateCategory(int id, [FromBody] SaveCategoryDto dto)
        {
            var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("Tenant is required.");

            try
            {
                var category = _productService.SaveCategory(tenantId.Value, id, dto);
                return Ok(new CategoryReadDto
                {
                    Id = category.Id,
                    TenantId = category.TenantId ?? tenantId.Value,
                    Name = category.Name,
                    IsActive = category.IsActive,
                    ProductCount = 0,
                });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("categories/{id:int}")]
        public IActionResult DeleteCategory(int id)
        {
            var tenantId = _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("A selected tenant is required.");

            try
            {
                _productService.DeactivateCategory(tenantId.Value, id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        public IActionResult CreateProduct([FromBody] ProductCreateDto dto)
        {
            var tenantId = dto.TenantId ?? _tenantAccess.SelectedTenantId;
            if (!tenantId.HasValue)
                return BadRequest("Tenant is required.");

            try
            {
                var product = new Product
                {
                    TenantId = tenantId,
                    Name = dto.Name,
                    Code = IdGenerator.GenerateId("PROD"),
                    Description = dto.Description,
                    CategoryId = dto.CategoryId,
                    Brand = dto.Brand,
                    IsActive = dto.IsActive,
                };

                _productService.PrepareProductForSave(product, dto.Category);
                _productService.AddProduct(product);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, MapProduct(product, _tenantAccess.SelectedStoreId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public IActionResult UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            try
            {
                var product = _productService.GetProductById(id, _tenantAccess.SelectedTenantId, null);
                if (product == null)
                    return NotFound();

                product.Name = dto.Name ?? product.Name;
                product.Description = dto.Description ?? product.Description;
                product.CategoryId = dto.CategoryId ?? product.CategoryId;
                product.Brand = dto.Brand ?? product.Brand;
                product.IsActive = dto.IsActive ?? product.IsActive;

                _productService.PrepareProductForSave(product, dto.Category);
                _productService.UpdateProduct(product);
                return Ok(MapProduct(product, _tenantAccess.SelectedStoreId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:int}")]
        public IActionResult DeleteProduct(int id)
        {
            try
            {
                _productService.DeleteProduct(id, _tenantAccess.SelectedTenantId, _tenantAccess.SelectedStoreId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}/stores")]
        public IActionResult GetProductStoreAssignments(int id)
        {
            try
            {
                var product = _productService.GetProductById(id, _tenantAccess.SelectedTenantId, null);
                if (product == null)
                    return NotFound();

                return Ok(_productService.GetProductStoreAssignments(id));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                return Ok(Array.Empty<ProductStoreAssignmentDto>());
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/stores")]
        public IActionResult SaveProductStoreAssignment(int id, [FromBody] SaveProductStoreAssignmentDto dto)
        {
            try
            {
                var assignment = _productService.SaveProductStoreAssignment(
                    id,
                    dto.TenantId,
                    dto.StoreId,
                    dto.IsActive
                );
                return Ok(assignment);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id:int}/stores/{storeId:int}")]
        public IActionResult RemoveProductStoreAssignment(int id, int storeId)
        {
            try
            {
                _productService.RemoveProductStoreAssignment(id, storeId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}/variants")]
        public IActionResult GetProductVariants(int id, [FromQuery] int? storeId = null)
        {
            var resolvedStoreId = storeId ?? _tenantAccess.SelectedStoreId;
            if (!resolvedStoreId.HasValue)
                return BadRequest("A selected store is required.");

            try
            {
                return Ok(_productService.GetVariants(id, resolvedStoreId.Value));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id:int}/variants")]
        public IActionResult CreateProductVariant(int id, [FromBody] SaveProductVariantDto dto)
        {
            try
            {
                var variant = _productService.SaveVariant(id, dto);
                return Ok(variant);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("variants/{variantId:int}")]
        public IActionResult UpdateProductVariant(int variantId, [FromBody] SaveProductVariantDto dto)
        {
            try
            {
                return Ok(_productService.UpdateVariant(variantId, dto));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("variants/{variantId:int}")]
        public IActionResult DeleteProductVariant(int variantId)
        {
            try
            {
                _productService.DeleteVariant(variantId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("variants/{variantId:int}/generate-barcode")]
        public IActionResult GenerateVariantBarcode(int variantId)
        {
            try
            {
                return Ok(_productService.GenerateBarcodeForVariant(variantId));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("variants/{variantId:int}/barcode-image")]
        public IActionResult GetVariantBarcodeImage(int variantId)
        {
            try
            {
                var image = _productService.GetVariantBarcodeImage(variantId);
                return File(image, "image/png");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static ProductReadDto MapProduct(Product product, int? selectedStoreId)
        {
            var assignment = selectedStoreId.HasValue
                ? product.StoreAssignments.FirstOrDefault(a => a.StoreId == selectedStoreId.Value)
                : product.StoreAssignments.OrderBy(a => a.StoreId).FirstOrDefault();

            var variant = selectedStoreId.HasValue
                ? product.Variants.FirstOrDefault(v => v.StoreId == selectedStoreId.Value && v.IsActive == true)
                    ?? assignment?.Variants.FirstOrDefault(v => v.IsActive == true)
                : product.Variants.OrderBy(v => v.StoreId).FirstOrDefault(v => v.IsActive == true);

            var resolvedStoreId = selectedStoreId ?? assignment?.StoreId ?? variant?.StoreId ?? 0;
            var stockQuantity = variant?.StockLevels.FirstOrDefault(sl => sl.StoreId == resolvedStoreId)?.QuantityOnHand ?? 0;

            var variantCount = selectedStoreId.HasValue
                ? product.Variants.Count(v => v.StoreId == selectedStoreId.Value && v.IsActive == true)
                : product.Variants.Count(v => v.IsActive == true);

            return new ProductReadDto
            {
                Id = product.Id,
                TenantId = product.TenantId ?? 0,
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
                Category = product.CategoryNavigation?.Name ?? string.Empty,
                Brand = product.Brand,
                Price = variant?.Price ?? 0,
                StockQuantity = stockQuantity,
                IsActive = product.IsActive,
                VariantCount = variantCount,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
            };
        }

        private static IEnumerable<ProductReadDto> MapSellableProducts(IEnumerable<Product> products, int storeId)
        {
            return products.SelectMany(product =>
            {
                var assignment = product.StoreAssignments.FirstOrDefault(a => a.StoreId == storeId && a.IsActive);
                if (assignment == null)
                    return Enumerable.Empty<ProductReadDto>();

                var variants = assignment.Variants
                    .Where(variant => variant.IsActive == true)
                    .OrderBy(variant => variant.Label ?? variant.SKU ?? variant.Id.ToString())
                    .Select(variant => new ProductReadDto
                    {
                        Id = product.Id,
                        TenantId = product.TenantId ?? 0,
                        StoreId = storeId,
                        AssignmentId = assignment.Id,
                        VariantId = variant.Id,
                        CategoryId = product.CategoryId,
                        Code = product.Code,
                        Barcode = variant.Barcode ?? string.Empty,
                        Name = product.Name,
                        SKU = variant.SKU ?? string.Empty,
                        Label = variant.Label,
                        Description = product.Description,
                        Category = product.CategoryNavigation?.Name ?? string.Empty,
                        Brand = product.Brand,
                        Price = variant.Price ?? 0,
                        StockQuantity = variant.StockLevels
                            .Where(stockLevel => stockLevel.StoreId == storeId)
                            .Select(stockLevel => stockLevel.QuantityOnHand)
                            .FirstOrDefault(),
                        IsActive = product.IsActive && assignment.IsActive && variant.IsActive == true,
                        VariantCount = 1,
                        CreatedAt = product.CreatedAt,
                        UpdatedAt = product.UpdatedAt,
                    });

                return variants;
            });
        }
    }
}
