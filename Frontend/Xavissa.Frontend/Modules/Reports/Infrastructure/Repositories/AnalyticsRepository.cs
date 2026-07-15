using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class AnalyticsRepository : IAnalyticsRepository
    {
        private readonly HttpClient _client;
        private readonly IDbContextFactory<LocalDbContext> _factory;
        private readonly IConnectivityService _net;
        private readonly IAuthService _auth;

        public AnalyticsRepository(
            IHttpClientFactory factory,
            IDbContextFactory<LocalDbContext> dbFactory,
            IConnectivityService net,
            IAuthService auth)
        {
            _client = factory.CreateClient("backend");
            _factory = dbFactory;
            _net = net;
            _auth = auth;
        }

        public async Task<TenantAnalyticsSummary> GetTenantAnalyticsAsync(int tenantId)
        {
            if (_net.IsOnline() && _auth.IsOnlineSession)
            {
                try
                {
                    return await GetRequiredAsync<TenantAnalyticsSummary>($"api/Analytics/tenant/{tenantId}");
                }
                catch
                {
                }
            }

            return await BuildTenantAnalyticsAsync(tenantId);
        }

        public async Task<StoreAnalyticsResponse> GetStoreAnalyticsAsync()
        {
            if (_net.IsOnline() && _auth.IsOnlineSession)
            {
                try
                {
                    return await GetRequiredAsync<StoreAnalyticsResponse>("api/Analytics/store");
                }
                catch
                {
                }
            }

            return await BuildStoreAnalyticsAsync(_auth.SelectedStoreId ?? 0);
        }

        private async Task<T> GetRequiredAsync<T>(string url)
        {
            var response = await _client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"API Error {(int)response.StatusCode}: {error}");
            }

            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new InvalidOperationException("Server returned an empty analytics response.");
        }

        private async Task<TenantAnalyticsSummary> BuildTenantAnalyticsAsync(int tenantId)
        {
            await using var db = _factory.CreateDbContext();

            var storesTask = db.Stores
                .AsNoTracking()
                .Where(store => store.TenantId == tenantId)
                .ToListAsync();
            var productCountTask = db.Products
                .AsNoTracking()
                .Where(product => product.TenantId == tenantId && product.VariantId == 0)
                .CountAsync();
            var salesByStoreTask = db.Sales
                .AsNoTracking()
                .Where(sale => sale.TenantId == tenantId)
                .GroupBy(sale => sale.StoreId)
                .Select(group => new
                {
                    StoreId = group.Key,
                    TotalSalesCount = group.Count(),
                    TotalRevenue = group.Sum(sale => sale.TotalPaid > 0 ? sale.TotalPaid : sale.TotalAmount),
                    LastSaleDate = group.Max(sale => (DateTime?)sale.Timestamp),
                })
                .ToListAsync();

            await Task.WhenAll(storesTask, productCountTask, salesByStoreTask);

            var stores = storesTask.Result;
            var productCount = productCountTask.Result;
            var salesByStore = salesByStoreTask.Result.ToDictionary(item => item.StoreId);
            var storeAnalytics = stores.Select(store =>
            {
                salesByStore.TryGetValue(store.Id, out var sales);
                var totalSalesCount = sales?.TotalSalesCount ?? 0;
                var totalRevenue = sales?.TotalRevenue ?? 0;
                return new StoreAnalyticsSummary
                {
                    StoreId = store.Id,
                    StoreName = store.Name,
                    StoreCode = store.Code,
                    IsActive = store.IsActive,
                    TotalSalesCount = totalSalesCount,
                    TotalRevenue = totalRevenue,
                    AverageSaleValue = totalSalesCount == 0 ? 0 : totalRevenue / totalSalesCount,
                    LastSaleDate = sales?.LastSaleDate,
                };
            }).ToList();

            var totalSalesAll = salesByStore.Values.Sum(item => item.TotalSalesCount);
            var totalRevenueAll = salesByStore.Values.Sum(item => item.TotalRevenue);

            return new TenantAnalyticsSummary
            {
                TenantId = tenantId,
                StoreCount = stores.Count,
                ProductCount = productCount,
                TotalSalesCount = totalSalesAll,
                TotalRevenue = totalRevenueAll,
                AverageSaleValue = totalSalesAll == 0 ? 0 : totalRevenueAll / totalSalesAll,
                Stores = storeAnalytics.OrderByDescending(store => store.TotalRevenue).ToList(),
            };
        }

        private async Task<StoreAnalyticsResponse> BuildStoreAnalyticsAsync(int storeId)
        {
            await using var db = _factory.CreateDbContext();

            var summary = await db.Sales
                .AsNoTracking()
                .Where(sale => sale.StoreId == storeId)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    TotalSalesCount = group.Count(),
                    TotalRevenue = group.Sum(sale => sale.TotalPaid > 0 ? sale.TotalPaid : sale.TotalAmount),
                })
                .FirstOrDefaultAsync();

            var totalSalesCount = summary?.TotalSalesCount ?? 0;
            var totalRevenue = summary?.TotalRevenue ?? 0;

            return new StoreAnalyticsResponse
            {
                StoreId = storeId,
                TotalSalesCount = totalSalesCount,
                TotalRevenue = totalRevenue,
                AverageSaleValue = totalSalesCount == 0 ? 0 : totalRevenue / totalSalesCount,
            };
        }
    }
}
