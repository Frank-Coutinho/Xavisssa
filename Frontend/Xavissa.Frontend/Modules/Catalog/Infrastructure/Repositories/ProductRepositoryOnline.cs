using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xavissa.Frontend.Mappers;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Data.Repositories
{
    public class ProductRepositoryOnline : IProductRepositoryOnline
    {
        private readonly HttpClient _client;

        public ProductRepositoryOnline(IHttpClientFactory factory)
        {
            _client = factory.CreateClient("backend");
        }

        public async Task<List<Product>> GetAllAsync()
        {
            var dtos = await SafeGetAsync<List<ProductReadDto>>("api/Product") ?? new List<ProductReadDto>();
            return dtos.ConvertAll(ProductMapper.FromReadDto);
        }

        public async Task<List<Product>> GetSellableProductsAsync(int storeId)
        {
            var dtos = await SafeGetAsync<List<ProductReadDto>>($"api/Product/sellable?storeId={storeId}") ?? new List<ProductReadDto>();
            return dtos.ConvertAll(ProductMapper.FromReadDto);
        }

        public async Task<List<Product>> GetCatalogAsync()
        {
            var dtos = await SafeGetAsync<List<ProductReadDto>>("api/Product/catalog") ?? new List<ProductReadDto>();
            return dtos.ConvertAll(ProductMapper.FromReadDto);
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            var dto = await SafeGetAsync<ProductReadDto>($"api/Product/{id}");
            return dto == null ? null : ProductMapper.FromReadDto(dto);
        }

        public async Task<Product?> GetByBarcodeAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
                return null;
            var dto = await SafeGetAsync<ProductReadDto>($"api/Product/barcode/{barcode}");
            return dto == null ? null : ProductMapper.FromReadDto(dto);
        }

        public async Task<Product?> CreateAsync(Product product)
        {
            var response = await _client.PostAsJsonAsync("api/Product", new
            {
                tenantId = product.TenantId,
                storeId = product.StoreId,
                categoryId = product.CategoryId,
                category = product.Category,
                name = product.Name,
                description = product.Description,
                brand = product.Brand,
                isActive = product.IsActive,
                attributesJson = product.AttributesJson,
            });
            var dto = await ReadIfSuccess<ProductReadDto>(response);
            return dto == null ? null : ProductMapper.FromReadDto(dto);
        }

        public async Task<Product?> UpdateAsync(Product product)
        {
            var response = await _client.PutAsJsonAsync($"api/Product/{product.OnlineId}", new
            {
                tenantId = product.TenantId,
                storeId = product.StoreId,
                categoryId = product.CategoryId,
                category = product.Category,
                name = product.Name,
                description = product.Description,
                brand = product.Brand,
                isActive = product.IsActive,
                attributesJson = product.AttributesJson,
            });
            var dto = await ReadIfSuccess<ProductReadDto>(response);
            return dto == null ? null : ProductMapper.FromReadDto(dto);
        }

        public async Task DeleteAsync(int id)
        {
            var response = await _client.DeleteAsync($"api/Product/{id}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    string.IsNullOrWhiteSpace(body)
                        ? $"Failed to delete product ({(int)response.StatusCode})."
                        : body,
                    null,
                    response.StatusCode
                );
            }
        }

        public async Task<List<CatalogCategory>> GetCategoriesAsync()
        {
            return await SafeGetAsync<List<CatalogCategory>>("api/Product/categories") ?? new List<CatalogCategory>();
        }

        public async Task<CatalogCategory?> SaveCategoryAsync(CatalogCategory category)
        {
            var response = category.Id > 0
                ? await _client.PutAsJsonAsync($"api/Product/categories/{category.Id}", new
                {
                    tenantId = category.TenantId,
                    name = category.Name,
                    isActive = category.IsActive,
                })
                : await _client.PostAsJsonAsync("api/Product/categories", new
                {
                    tenantId = category.TenantId,
                    name = category.Name,
                    isActive = category.IsActive,
                });

            return await ReadIfSuccess<CatalogCategory>(response);
        }

        public async Task DeleteCategoryAsync(int categoryId)
        {
            var response = await _client.DeleteAsync($"api/Product/categories/{categoryId}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<ProductStoreAssignment>> GetStoreAssignmentsAsync(int productId)
        {
            try
            {
                return await SafeGetAsync<List<ProductStoreAssignment>>($"api/Product/{productId}/stores")
                    ?? new List<ProductStoreAssignment>();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"[Products] Store assignments unavailable for product {productId}: {ex.Message}");
                return new List<ProductStoreAssignment>();
            }
        }

        public async Task SaveStoreAssignmentAsync(int productId, ProductStoreAssignment assignment)
        {
            var response = await _client.PostAsJsonAsync($"api/Product/{productId}/stores", new
            {
                tenantId = assignment.TenantId,
                storeId = assignment.StoreId,
                isActive = assignment.IsActive,
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(message)
                        ? "The server could not save the product-store assignment."
                        : message
                );
            }
        }

        public async Task RemoveStoreAssignmentAsync(int productId, int storeId)
        {
            var response = await _client.DeleteAsync($"api/Product/{productId}/stores/{storeId}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<ProductVariantRecord>> GetVariantsAsync(int productId, int storeId)
        {
            var dtos = await SafeGetAsync<List<ProductVariantReadDto>>($"api/Product/{productId}/variants?storeId={storeId}")
                ?? new List<ProductVariantReadDto>();

            return dtos.Select(dto => new ProductVariantRecord
            {
                Id = dto.Id,
                ProductId = dto.ProductId,
                AssignmentId = dto.AssignmentId,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                StoreName = dto.StoreName,
                Name = dto.Name,
                Label = dto.Label ?? string.Empty,
                SKU = dto.SKU ?? string.Empty,
                Barcode = dto.Barcode,
                Price = dto.Price,
                CostPrice = dto.CostPrice,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive,
            }).ToList();
        }

        public async Task<ProductVariantRecord?> SaveVariantAsync(int productId, ProductVariantRecord variant)
        {
            var response = variant.Id > 0
                ? await _client.PutAsJsonAsync($"api/Product/variants/{variant.Id}", new
                {
                    assignmentId = variant.AssignmentId > 0 ? (int?)variant.AssignmentId : null,
                    storeId = variant.StoreId,
                    label = variant.Label,
                    sku = variant.SKU,
                    barcode = variant.Barcode,
                    price = variant.Price,
                    costPrice = variant.CostPrice,
                    stockQuantity = variant.StockQuantity,
                    isActive = variant.IsActive,
                    generateBarcode = string.IsNullOrWhiteSpace(variant.Barcode),
                })
                : await _client.PostAsJsonAsync($"api/Product/{productId}/variants", new
                {
                    assignmentId = variant.AssignmentId > 0 ? (int?)variant.AssignmentId : null,
                    storeId = variant.StoreId,
                    label = variant.Label,
                    sku = variant.SKU,
                    barcode = variant.Barcode,
                    price = variant.Price,
                    costPrice = variant.CostPrice,
                    stockQuantity = variant.StockQuantity,
                    isActive = variant.IsActive,
                    generateBarcode = string.IsNullOrWhiteSpace(variant.Barcode),
                });

            var dto = await ReadIfSuccess<ProductVariantReadDto>(response);
            if (dto == null)
                return null;

            return new ProductVariantRecord
            {
                Id = dto.Id,
                ProductId = dto.ProductId,
                AssignmentId = dto.AssignmentId,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                StoreName = dto.StoreName,
                Name = dto.Name,
                Label = dto.Label ?? string.Empty,
                SKU = dto.SKU ?? string.Empty,
                Barcode = dto.Barcode,
                Price = dto.Price,
                CostPrice = dto.CostPrice,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive,
            };
        }

        public async Task DeleteVariantAsync(int variantId)
        {
            var response = await _client.DeleteAsync($"api/Product/variants/{variantId}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
        }

        public async Task<ProductVariantRecord?> GenerateVariantBarcodeAsync(int variantId)
        {
            var response = await _client.PostAsync($"api/Product/variants/{variantId}/generate-barcode", null);
            var dto = await ReadIfSuccess<ProductVariantReadDto>(response);
            if (dto == null)
                return null;

            return new ProductVariantRecord
            {
                Id = dto.Id,
                ProductId = dto.ProductId,
                AssignmentId = dto.AssignmentId,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                StoreName = dto.StoreName,
                Name = dto.Name,
                Label = dto.Label ?? string.Empty,
                SKU = dto.SKU ?? string.Empty,
                Barcode = dto.Barcode,
                Price = dto.Price,
                CostPrice = dto.CostPrice,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive,
            };
        }

        public async Task<byte[]?> GetVariantBarcodeImageAsync(int variantId)
        {
            var response = await _client.GetAsync($"api/Product/variants/{variantId}/barcode-image");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsByteArrayAsync();
        }

        private async Task<T?> SafeGetAsync<T>(string url)
        {
            try { return await _client.GetFromJsonAsync<T>(url); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw new UnauthorizedAccessException(); }
        }

        private static async Task<T?> ReadIfSuccess<T>(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException();
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>();
        }
    }
}
