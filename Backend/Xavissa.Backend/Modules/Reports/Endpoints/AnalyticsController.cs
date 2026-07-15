using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.Security;
using Xavissa.Database;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public AnalyticsController(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        [HttpGet("platform")]
        [Authorize(Roles = AccessRoles.SystemAdmin)]
        public async Task<IActionResult> GetPlatformAnalytics()
        {
            return Ok(new
            {
                TotalProducts = await _db.Products.CountAsync(),
                TotalStores = await _db.Stores.CountAsync(),
                TotalSalesRevenue = await _db.SalePayments.SumAsync(p => (decimal?)p.Amount) ?? 0,
            });
        }

        [HttpGet("tenant/{tenantId:int}")]
        public async Task<IActionResult> GetTenantAnalytics(int tenantId)
        {
            if (_tenantAccess.ActingRole.IsStoreManager())
                return Forbid();

            var permission = _tenantAccess.EnsureTenantAccess(tenantId);
            if (permission != null)
                return permission;

            var stores = await _db.Stores
                .Where(s => s.TenantId == tenantId)
                .OrderBy(s => s.Name)
                .Select(store => new
                {
                    StoreId = store.Id,
                    StoreName = store.Name,
                    StoreCode = store.Code,
                    store.IsActive,
                    TotalSalesCount = _db.Sales.Count(sale => sale.TenantId == tenantId && sale.StoreId == store.Id),
                    TotalRevenue = _db.Sales
                        .Where(sale => sale.TenantId == tenantId && sale.StoreId == store.Id)
                        .SelectMany(sale => sale.Payments)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0,
                    AverageSaleValue = _db.Sales
                        .Where(sale => sale.TenantId == tenantId && sale.StoreId == store.Id)
                        .Average(sale => (decimal?)sale.TotalAmount) ?? 0,
                    LastSaleDate = _db.Sales
                        .Where(sale => sale.TenantId == tenantId && sale.StoreId == store.Id)
                        .Max(sale => (DateTime?)sale.SaleDate),
                })
                .ToListAsync();

            var salesQuery = _db.Sales.Where(s => s.TenantId == tenantId);
            var totalSalesCount = await salesQuery.CountAsync();
            var totalRevenue = await _db.SalePayments.Where(p => p.TenantId == tenantId).SumAsync(p => (decimal?)p.Amount) ?? 0;

            return Ok(new
            {
                TenantId = tenantId,
                StoreCount = stores.Count,
                ProductCount = await _db.Products.CountAsync(p => p.TenantId == tenantId),
                TotalSalesCount = totalSalesCount,
                TotalRevenue = totalRevenue,
                AverageSaleValue = totalSalesCount == 0 ? 0 : totalRevenue / totalSalesCount,
                Stores = stores,
            });
        }

        [HttpGet("store")]
        public async Task<IActionResult> GetStoreAnalytics(DateTime? start = null, DateTime? end = null)
        {
            var storeRequirement = _tenantAccess.RequireSelectedStore();
            if (storeRequirement != null)
                return storeRequirement.Result!;
            var storeId = _tenantAccess.SelectedStoreId!.Value;

            var salesQuery = _db.Sales.AsQueryable().Where(s => s.StoreId == storeId);
            if (start.HasValue)
                salesQuery = salesQuery.Where(s => s.SaleDate >= start.Value);
            if (end.HasValue)
                salesQuery = salesQuery.Where(s => s.SaleDate <= end.Value);

            var sales = await salesQuery.ToListAsync();
            var dailySales = (await salesQuery
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(s => s.TotalAmount),
                })
                .OrderBy(x => x.Date)
                .ToListAsync()
            )
                .Select(x => new
                {
                    Date = x.Date.ToString("dd/MM/yyyy"),
                    x.Total,
                })
                .ToList();

            return Ok(new
            {
                StoreId = storeId,
                TotalSalesCount = sales.Count,
                TotalRevenue = await _db.SalePayments.Where(p => p.StoreId == storeId).SumAsync(p => (decimal?)p.Amount) ?? 0,
                AverageSaleValue = sales.Any() ? sales.Average(s => s.TotalAmount) : 0,
                DailySales = dailySales,
            });
        }
    }
}
