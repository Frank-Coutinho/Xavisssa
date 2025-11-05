using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Services;
using Xavissa.Backend.Utilities;
using Xavissa.Database;
using Xavissa.Database.Models;
using Xavissa.Database.ViewModels;

namespace Xavissa.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly XavissaDbContext _db;
        private readonly SalesService _salesService;

        public SalesController(XavissaDbContext db, SalesService salesService)
        {
            _db = db;
            _salesService = salesService;
        }

        #region 🔹 GET Endpoints

        [HttpGet]
        public async Task<ActionResult<List<Sale>>> GetAll()
        {
            var sales = await _db
                .Sales.Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Include(s => s.User)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            return Ok(sales);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Sale>> GetById(int id)
        {
            var sale = await _db
                .Sales.Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();
            return Ok(sale);
        }

        [HttpGet("by-date")]
        public async Task<ActionResult<List<Sale>>> GetByDateRange(DateTime start, DateTime end)
        {
            var sales = await _db
                .Sales.Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Include(s => s.User)
                .Where(s => s.SaleDate >= start && s.SaleDate <= end)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            return Ok(sales);
        }

        [HttpGet("summary")]
        public async Task<ActionResult<SalesReportViewModel>> GetSummary(
            DateTime? start = null,
            DateTime? end = null
        )
        {
            var query = _db.Sales.AsQueryable();

            if (start.HasValue)
                query = query.Where(s => s.SaleDate >= start.Value);
            if (end.HasValue)
                query = query.Where(s => s.SaleDate <= end.Value);

            var salesList = await query.ToListAsync();

            if (!salesList.Any())
            {
                return Ok(
                    new SalesReportViewModel
                    {
                        StartDate = start,
                        EndDate = end,
                        TotalSalesCount = 0,
                        TotalRevenue = 0,
                        AverageSaleValue = 0,
                    }
                );
            }

            var report = new SalesReportViewModel
            {
                StartDate = start ?? salesList.Min(s => s.SaleDate),
                EndDate = end ?? salesList.Max(s => s.SaleDate),
                TotalSalesCount = salesList.Count,
                TotalRevenue = salesList.Sum(s => s.TotalAmount),
                AverageSaleValue = salesList.Average(s => s.TotalAmount),
            };

            return Ok(report);
        }

        [HttpGet("by-category/{category}")]
        public async Task<ActionResult<List<Sale>>> GetByCategory(string category)
        {
            // Parse string to enum
            if (!Enum.TryParse<ProductCategory>(category, true, out var categoryEnum))
            {
                return BadRequest("Invalid category");
            }

            var sales = await _db
                .Sales.Include(s => s.SaleItems)
                .Where(s => s.SaleItems.Any(i => i.ProductCategory == categoryEnum))
                .ToListAsync();

            return Ok(sales);
        }

        [HttpGet("payment/{paymentMethod}")]
        public async Task<ActionResult<List<Sale>>> GetByPaymentMethod(string paymentMethod)
        {
            if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedMethod))
                return BadRequest("Invalid payment method.");

            var sales = await _db
                .Sales.Where(s => s.PaymentMethod == parsedMethod)
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .ToListAsync();

            return Ok(sales);
        }

        [HttpGet("top-products")]
        public async Task<ActionResult<List<ProductSalesReportViewModel>>> GetTopSellingProducts(
            int topCount = 10
        )
        {
            var topProducts = await _db
                .SaleItems.Include(si => si.Product)
                .GroupBy(si => si.Product.Name)
                .Select(g => new ProductSalesReportViewModel
                {
                    ProductName = g.Key,
                    TotalQuantitySold = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice),
                    TransactionCount = g.Count(),
                })
                .OrderByDescending(x => x.TotalQuantitySold)
                .Take(topCount)
                .ToListAsync();

            return Ok(topProducts);
        }

        #endregion

        #region  POST Endpoint

        [HttpPost]
        [HttpPost]
        [HttpPost]
        public async Task<ActionResult<Sale>> Create([FromBody] SaleCreateDto dto)
        {
            if (dto == null || dto.SaleItems == null || !dto.SaleItems.Any())
                return BadRequest("Invalid sale data");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return BadRequest("Invalid user claim");

            var userId = int.Parse(userIdClaim);

            // Pass the DTO to the service
            var sale = await _salesService.CreateSaleAsync(
                dto.SaleItems,
                userId,
                dto.SaleItems.First().PaymentMethod, // for per-sale payment method
                dto.Discount ?? 0,
                dto.AmountPaid ?? 0
            );

            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
        }

        #endregion

        #region 🔹 PUT Endpoint

        [HttpPut("{id:int}")]
        public async Task<ActionResult<Sale>> Update(int id, [FromBody] Sale updatedSale)
        {
            var existing = await _db
                .Sales.Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (existing == null)
                return NotFound();

            existing.TotalAmount = updatedSale.TotalAmount;
            existing.PaymentMethod = updatedSale.PaymentMethod;
            existing.LastModified = DateTime.UtcNow;

            _db.SaleItems.RemoveRange(existing.SaleItems);
            foreach (var item in updatedSale.SaleItems)
            {
                item.SaleId = existing.Id;
                item.LastModified = DateTime.UtcNow;
            }

            existing.SaleItems = updatedSale.SaleItems;

            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        #endregion

        #region 🔹 DELETE Endpoints

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var sale = await _db
                .Sales.Include(s => s.SaleItems)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound();

            // Archive before deleting
            var deletedSale = new DeletedSale
            {
                OriginalSaleId = sale.Id,
                SaleDate = sale.SaleDate,
                TotalAmount = sale.TotalAmount,
                Discount = sale.Discount ?? 0,
                AmountPaid = sale.AmountPaid ?? 0,
                PaymentMethod = sale.PaymentMethod.ToString(),
                ReceiptNumber = sale.ReceiptNumber,
                Code = sale.Code,
                UserId = sale.UserId ?? 0,
                DeletedAt = DateTime.UtcNow,
                SaleItems = sale
                    .SaleItems.Select(i => new DeletedSaleItem
                    {
                        ProductId = i.ProductId,
                        ProductCategory = i.ProductCategory.ToString(),
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                    })
                    .ToList(),
            };

            _db.DeletedSales.Add(deletedSale);
            _db.Sales.Remove(sale);

            await _db.SaveChangesAsync();

            return Ok("Sale archived and deleted successfully.");
        }

        [HttpDelete("multiple")]
        public async Task<ActionResult> DeleteMultiple([FromBody] List<int> saleIds)
        {
            if (saleIds == null || !saleIds.Any())
                return BadRequest("No sale IDs provided.");

            var salesToDelete = await _db.Sales.Where(s => saleIds.Contains(s.Id)).ToListAsync();

            if (!salesToDelete.Any())
                return NotFound("No sales found for provided IDs.");

            _db.Sales.RemoveRange(salesToDelete);
            await _db.SaveChangesAsync();

            return Ok(salesToDelete.Count);
        }

        [HttpDelete("by-date")]
        public async Task<ActionResult<int>> DeleteByDateRange(DateTime start, DateTime end)
        {
            var salesToDelete = await _db
                .Sales.Where(s => s.SaleDate >= start && s.SaleDate <= end)
                .ToListAsync();

            if (!salesToDelete.Any())
                return NotFound("No sales found in range.");

            _db.Sales.RemoveRange(salesToDelete);
            await _db.SaveChangesAsync();

            return Ok(salesToDelete.Count);
        }

        #endregion
    }

    #region 🔹 View Models for Reports

    public class ProductSalesReportViewModel
    {
        public string ProductName { get; set; }
        public int TotalQuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
    }

    public class SalesReportViewModel
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageSaleValue { get; set; }
    }

    #endregion
}
