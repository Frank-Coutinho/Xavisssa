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
    public class SaleRepositoryOnline : ISaleOnlineRepository
    {
        private readonly IHttpClientFactory _factory;
        private readonly IProductRepository _productRepo;
        private HttpClient Client => _factory.CreateClient("backend");

        public SaleRepositoryOnline(IHttpClientFactory factory, IProductRepository productRepo)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _productRepo = productRepo ?? throw new ArgumentNullException(nameof(productRepo));
        }

        public async Task<List<Sale>> GetAllAsync()
        {
            var response = await Client.GetAsync("api/Sales");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            response.EnsureSuccessStatusCode();
            var dtos = await response.Content.ReadFromJsonAsync<List<SaleReadDto>>() ?? new List<SaleReadDto>();
            return dtos.Select(SaleMapper.FromReadDto).ToList();
        }

        private async Task<SaleSyncDto> MapToSaleSyncDtoAsync(Sale sale)
        {
            if (sale.Items == null || sale.Items.Count == 0)
                throw new Exception($"Sale {sale.Id} has no items");

            var localProductIds = sale.Items.Select(item => item.ProductId).Where(id => id > 0).Distinct().ToList();
            var localProducts = await _productRepo.GetByIdsAsync(localProductIds);
            var productsById = localProducts.ToDictionary(product => product.Id);

            var items = new List<SaleItemSyncDto>();
            foreach (var item in sale.Items)
            {
                if (item.Quantity <= 0)
                    throw new Exception($"Invalid quantity for product {item.ProductId}");

                if (!productsById.TryGetValue(item.ProductId, out var product))
                    throw new Exception($"Local product {item.ProductId} not found");

                if (product == null)
                    throw new Exception($"Local product {item.ProductId} not found");
                if (product.OnlineId <= 0)
                    throw new Exception($"Product {product.Name} (LocalId={product.Id}) not synced online");
                if (product.VariantId <= 0)
                    throw new Exception($"Product {product.Name} (LocalId={product.Id}) has no synced variant");

                items.Add(new SaleItemSyncDto
                {
                    OnlineId = item.OnlineId > 0 ? item.OnlineId : null,
                    SyncId = item.SyncId,
                    TenantId = sale.TenantId,
                    StoreId = sale.StoreId,
                    SourceDeviceId = item.SourceDeviceId ?? sale.SourceDeviceId,
                    ClientCreatedAt = item.ClientCreatedAt,
                    ClientUpdatedAt = item.ClientUpdatedAt,
                    LastSyncedAt = item.LastSyncedAt,
                    VariantId = product.VariantId,
                    ProductId = product.OnlineId,
                    Quantity = item.Quantity,
                    IsRefunded = false,
                });
            }

            return new SaleSyncDto
            {
                OnlineId = sale.OnlineId > 0 ? sale.OnlineId : null,
                SyncId = sale.SyncId,
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                SourceDeviceId = sale.SourceDeviceId,
                ClientCreatedAt = sale.ClientCreatedAt,
                ClientUpdatedAt = sale.ClientUpdatedAt,
                LastSyncedAt = sale.LastSyncedAt,
                SaleItems = items,
                SalePayments = sale.Payments.Select(payment => new SalePaymentSyncDto
                {
                    OnlineId = payment.OnlineId > 0 ? payment.OnlineId : null,
                    SyncId = payment.SyncId,
                    TenantId = payment.TenantId,
                    StoreId = payment.StoreId,
                    SourceDeviceId = payment.SourceDeviceId ?? sale.SourceDeviceId,
                    ClientCreatedAt = payment.ClientCreatedAt,
                    ClientUpdatedAt = payment.ClientUpdatedAt,
                    LastSyncedAt = payment.LastSyncedAt,
                    PaymentMethod = payment.PaymentMethod,
                    Amount = payment.Amount,
                    ReferenceNumber = payment.ReferenceNumber,
                    Notes = payment.Notes,
                }).ToList(),
                Discount = sale.Discount,
            };
        }

        public async Task<Sale> CreateAsync(Sale sale)
        {
            var syncDto = await MapToSaleSyncDtoAsync(sale);
            var response = await Client.PostAsJsonAsync("api/Sales", syncDto);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"API Error {(int)response.StatusCode}: {error}");
            }

            var dto = await response.Content.ReadFromJsonAsync<SaleReadDto>() ?? throw new InvalidOperationException("Server returned null sale.");
            return SaleMapper.FromReadDto(dto);
        }

        public async Task SoftDeleteSaleAsync(int saleId, string reason)
        {
            var response = await Client.PostAsJsonAsync($"api/Sales/{saleId}/soft-delete", new { reason });
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"API Error {(int)response.StatusCode}: {error}");
            }
        }

        public async Task RefundSaleAsync(int saleId, string reason)
        {
            var response = await Client.PostAsJsonAsync($"api/Sales/{saleId}/refund", new { reason });
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"API Error {(int)response.StatusCode}: {error}");
            }
        }

        public async Task RefundSaleItemAsync(int saleId, int saleItemId, int quantity, string reason)
        {
            var response = await Client.PostAsJsonAsync(
                $"api/Sales/{saleId}/items/{saleItemId}/refund",
                new { quantity, reason });
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException();

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"API Error {(int)response.StatusCode}: {error}");
            }
        }

        public async Task DebugPrintServerSales()
        {
            var sales = await GetAllAsync();
            foreach (var sale in sales)
            {
                Console.WriteLine($"Sale Id={sale.Id}, Total={sale.TotalAmount}, Timestamp={sale.Timestamp}");
                foreach (var item in sale.Items)
                    Console.WriteLine($"  Item LocalId={item.Id}, ProductId={item.ProductId}, Qty={item.Quantity}, Price={item.UnitPrice}, Name={item.ProductName}");
            }
        }
    }
}
