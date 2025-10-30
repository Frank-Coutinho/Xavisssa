using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Database;
using Xavissa.Database.Models;
using Xavissa.Database.ViewModels;
using Xavissa.Backend.Utilities;
using Xavissa.Backend.DTOs;

namespace Xavissa.Backend.Services
{
    public class SalesService
    {
        private readonly XavissaDbContext _db;

        public SalesService(XavissaDbContext db)
        {
            _db = db;
        }

        #region 🔹 CREATE SALE

        public async Task<Sale> CreateSaleAsync(List<SaleItemDto> itemsDto, int userId, string paymentMethod = "Cash", decimal discount = 0, decimal amountPaid = 0)
        {
            if (itemsDto == null || itemsDto.Count == 0)
                throw new ArgumentException("A sale must contain at least one item.");

            // Load products from DB
            var productIds = itemsDto.Select(i => i.ProductId).ToList();
            var products = await _db.Products
                                    .Where(p => productIds.Contains(p.Id))
                                    .ToDictionaryAsync(p => p.Id, p => p);

            var saleItems = new List<SaleItem>();

            foreach (var itemDto in itemsDto)
            {
                if (!products.TryGetValue(itemDto.ProductId, out var product))
                    throw new ArgumentException($"Product with ID {itemDto.ProductId} not found.");

                //Validate stock
                if (product.StockQuantity < itemDto.Quantity)
                    throw new InvalidOperationException($"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {itemDto.Quantity}");

                //Deduct stock
                product.StockQuantity -= itemDto.Quantity;

                var saleItem = new SaleItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = product.Price,
                    ProductCategory = product.Category,
                    LastModified = DateTime.UtcNow
                };
                saleItems.Add(saleItem);
            }

            // Calculate totals
            var total = saleItems.Sum(i => i.UnitPrice * i.Quantity);
            var finalTotal = total - discount;

            var sale = new Sale
            {
                SaleDate = DateTime.UtcNow,
                TotalAmount = total,
                Discount = discount,
                AmountPaid = amountPaid == 0 ? finalTotal : amountPaid,
                PaymentMethod = Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedMethod)
                    ? parsedMethod
                    : PaymentMethod.Cash,
                ReceiptNumber = $"RC-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
                Code = IdGenerator.GenerateId("SALE"),
                UserId = userId,
                SaleItems = saleItems,
                LastModified = DateTime.UtcNow
            };

            //Save all changes in one go
            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            return sale;
        }




        #endregion

        #region 🔹 READ SALES

        public async Task<List<Sale>> GetAllSalesAsync()
        {
            return await _db.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.User)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<Sale?> GetSaleByIdAsync(int id)
        {
            return await _db.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<Sale>> GetSalesByDateAsync(DateTime date)
        {
            var start = date.Date;
            var end = date.Date.AddDays(1);
            return await _db.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.User)
                .Where(s => s.SaleDate >= start && s.SaleDate < end)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<List<Sale>> GetSalesByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _db.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.User)
                .Where(s => s.SaleDate >= start && s.SaleDate <= end)
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();
        }

        public async Task<Sale?> GetSaleByReceiptNumberAsync(string receiptNumber)
        {
            return await _db.Sales
                .Include(s => s.SaleItems).ThenInclude(si => si.Product)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ReceiptNumber == receiptNumber);
        }

        public async Task<decimal> GetTotalSalesAmountAsync(DateTime date)
        {
            return await _db.Sales
                .Where(s => s.SaleDate.Date == date.Date)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;
        }

        #endregion

        #region 🔹 ANALYTICS / REPORTS

        public async Task<SalesReportViewModel> GetSalesSummaryAsync(DateTime? start = null, DateTime? end = null)
        {
            var query = _db.Sales.AsQueryable();

            if (start.HasValue) query = query.Where(s => s.SaleDate >= start.Value);
            if (end.HasValue) query = query.Where(s => s.SaleDate <= end.Value);

            var salesList = await query.ToListAsync();

            if (!salesList.Any())
            {
                return new SalesReportViewModel
                {
                    StartDate = start ?? DateTime.UtcNow,
                    EndDate = end ?? DateTime.UtcNow,
                    TotalSalesCount = 0,
                    TotalRevenue = 0,
                    AverageSaleValue = 0
                };
            }

            return new SalesReportViewModel
            {
                StartDate = start ?? salesList.Min(s => s.SaleDate),
                EndDate = end ?? salesList.Max(s => s.SaleDate),
                TotalSalesCount = salesList.Count,
                TotalRevenue = salesList.Sum(s => s.TotalAmount),
                AverageSaleValue = salesList.Average(s => s.TotalAmount)
            };
        }

        public async Task<Dictionary<string, decimal>> GetDailySalesSummaryAsync(DateTime start, DateTime end)
        {
            return await _db.Sales
                .Where(s => s.SaleDate >= start && s.SaleDate <= end)
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new { Date = g.Key.ToString("dd/MM/yyyy"), Total = g.Sum(s => s.TotalAmount) })
                .ToDictionaryAsync(x => x.Date, x => x.Total);
        }

        #endregion

        #region 🔹 DELETE SALES

        public async Task<bool> DeleteSaleAsync(int id)
        {
            var sale = await _db.Sales.FindAsync(id);
            if (sale == null) return false;

            _db.Sales.Remove(sale);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<int> DeleteSalesByDateRangeAsync(DateTime start, DateTime end)
        {
            var salesToDelete = await _db.Sales
                .Where(s => s.SaleDate >= start && s.SaleDate <= end)
                .ToListAsync();

            if (!salesToDelete.Any()) return 0;

            _db.Sales.RemoveRange(salesToDelete);
            await _db.SaveChangesAsync();

            return salesToDelete.Count;
        }

        #endregion

        #region 🔹 RECEIPT GENERATION

        public string GenerateReceiptContent(Sale sale)
        {
            if (sale == null)
                throw new ArgumentNullException(nameof(sale));

            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("             XAVISSA SYSTEM              ");
            sb.AppendLine("========================================");
            sb.AppendLine($"Receipt Nº: {sale.ReceiptNumber ?? sale.Id.ToString()}");
            sb.AppendLine($"Date: {sale.SaleDate:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Cashier: {sale.User?.Username ?? "System User"}");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("ITEMS:");

            foreach (var item in sale.SaleItems)
            {
                sb.AppendLine($"{item.Product?.Name ?? "Unknown"}");
                sb.AppendLine($"{item.Quantity} x {item.UnitPrice:C2} = {(item.UnitPrice * item.Quantity):C2}");
            }

            sb.AppendLine("----------------------------------------");
            sb.AppendLine($"Subtotal: {sale.TotalAmount:C2}");

            if (sale.Discount > 0)
                sb.AppendLine($"Discount: -{sale.Discount:C2}");

            var final = sale.TotalAmount - (sale.Discount ?? 0);
            sb.AppendLine($"TOTAL: {final:C2}");
            sb.AppendLine($"Payment: {sale.PaymentMethod}");
            sb.AppendLine($"Amount Paid: {sale.AmountPaid:C2}");

            if (sale.PaymentMethod == PaymentMethod.Cash && sale.AmountPaid > final)
                sb.AppendLine($"Change: {sale.AmountPaid - final:C2}");

            sb.AppendLine("----------------------------------------");
            sb.AppendLine("     Thank you for your purchase!       ");
            sb.AppendLine("========================================");

            return sb.ToString();
        }

        #endregion

        #region 🔹 REFUND / CANCELLATION

        public async Task<bool> RefundSaleAsync(int saleId, string reason)
        {
            var sale = await _db.Sales.Include(s => s.SaleItems).FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null) return false;

            sale.IsRefunded = true;
            sale.RefundReason = reason;
            sale.LastModified = DateTime.UtcNow;

            _db.Sales.Update(sale);
            await _db.SaveChangesAsync();
            return true;
        }

        internal static async Task<Sale> CreateSaleAsync(List<SaleItemDto> saleItems, int userId, object paymentMethod, object discount, object amountPaid)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
