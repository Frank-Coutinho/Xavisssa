using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Data.Repositories
{
    public class SaleRepositoryOffline : ISaleOfflineRepository
    {
        private readonly IDbContextFactory<LocalDbContext> _factory;
        private readonly IDemoStateService _demoState;

        public SaleRepositoryOffline(IDbContextFactory<LocalDbContext> factory, IDemoStateService demoState)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _demoState = demoState ?? throw new ArgumentNullException(nameof(demoState));
        }

        public async Task DebugPrintRawSaleItemsAsync(int saleId)
        {
            await using var db = _factory.CreateDbContext();
            var count = await db.SaleItems.CountAsync(i => i.SaleId == saleId);
            Console.WriteLine($"RAW DB | SaleId={saleId}, SaleItems rows={count}");
        }

        public async Task<List<Sale>> GetAllAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.Sales.Include(s => s.Items).Include(s => s.Payments).AsNoTracking().ToListAsync();
        }

        public async Task<List<Sale>> GetHistoryPageAsync(SaleHistoryQuery query)
        {
            query ??= new SaleHistoryQuery();
            var limit = Math.Clamp(query.Limit, 1, 250);
            var offset = Math.Max(0, query.Offset);

            await using var db = _factory.CreateDbContext();
            var salesQuery = db.Sales.AsNoTracking().AsQueryable();

            if (query.StoreId.HasValue && query.StoreId.Value > 0)
                salesQuery = salesQuery.Where(sale => sale.StoreId == query.StoreId.Value);

            if (query.FromUtc.HasValue)
                salesQuery = salesQuery.Where(sale => sale.Timestamp >= query.FromUtc.Value);

            if (query.ToUtc.HasValue)
                salesQuery = salesQuery.Where(sale => sale.Timestamp < query.ToUtc.Value);

            if (!string.IsNullOrWhiteSpace(query.SearchText))
            {
                var term = query.SearchText.Trim();
                salesQuery = salesQuery.Where(sale =>
                    sale.ReceiptNumber.Contains(term)
                    || sale.PaymentSummary.Contains(term)
                    || sale.Items.Any(item => item.ProductName.Contains(term)));
            }

            var pageIds = await salesQuery
                .OrderByDescending(sale => sale.Timestamp)
                .Skip(offset)
                .Take(limit)
                .Select(sale => sale.Id)
                .ToListAsync();

            if (pageIds.Count == 0)
                return new List<Sale>();

            var sales = await db.Sales
                .AsNoTracking()
                .Include(sale => sale.Items)
                .Include(sale => sale.Payments)
                .Where(sale => pageIds.Contains(sale.Id))
                .ToListAsync();
            var sortOrder = pageIds.Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            return sales
                .OrderBy(sale => sortOrder.TryGetValue(sale.Id, out var index) ? index : int.MaxValue)
                .ToList();
        }

        public async Task<List<Sale>> GetUnsyncedAsync()
        {
            await using var db = _factory.CreateDbContext();
            return await db.Sales
                .Include(s => s.Items)
                .Include(s => s.Payments)
                .Where(s => !s.Synced && s.Items.Count > 0)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(Sale sale)
        {
            await EnsureDemoCanWriteAsync();
            if (sale == null)
                throw new ArgumentNullException(nameof(sale));

            ClampSaleDiscount(sale);

            await using var db = _factory.CreateDbContext();
            await db.EnsureLocalSchemaAsync();

            db.ChangeTracker.Clear();
            db.Entry(sale).State = EntityState.Added;

            foreach (var item in sale.Items)
                db.Entry(item).State = EntityState.Added;

            foreach (var payment in sale.Payments)
                db.Entry(payment).State = EntityState.Added;

            await db.SaveChangesAsync();
        }

        public async Task UpdateLocalSaleIdAsync(int oldId, int newId)
        {
            if (oldId == newId)
                return;

            await using var db = _factory.CreateDbContext();
            await db.EnsureLocalSchemaAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var oldSale = await db.Sales
                .Include(sale => sale.Items)
                .Include(sale => sale.Payments)
                .FirstOrDefaultAsync(sale => sale.Id == oldId);
            if (oldSale == null)
                return;

            var targetSale = await db.Sales
                .Include(sale => sale.Items)
                .Include(sale => sale.Payments)
                .FirstOrDefaultAsync(sale => sale.Id == newId);

            if (targetSale == null)
            {
                targetSale = CloneSaleHeader(oldSale, newId);
                db.Sales.Add(targetSale);
                await db.SaveChangesAsync();
            }
            else
            {
                targetSale.OnlineId = oldSale.OnlineId;
                targetSale.SyncId = oldSale.SyncId;
                targetSale.SourceDeviceId = oldSale.SourceDeviceId;
                targetSale.ClientCreatedAt = oldSale.ClientCreatedAt;
                targetSale.ClientUpdatedAt = oldSale.ClientUpdatedAt;
                targetSale.LastSyncedAt = oldSale.LastSyncedAt;
                targetSale.TenantId = oldSale.TenantId;
                targetSale.StoreId = oldSale.StoreId;
                targetSale.Timestamp = oldSale.Timestamp;
                targetSale.TotalAmount = oldSale.TotalAmount;
                targetSale.Discount = oldSale.Discount;
                targetSale.TotalPaid = oldSale.TotalPaid;
                targetSale.PaymentSummary = oldSale.PaymentSummary;
                targetSale.PaymentStatus = oldSale.PaymentStatus;
                targetSale.ChangeGiven = oldSale.ChangeGiven;
                targetSale.ReceiptNumber = oldSale.ReceiptNumber;
                targetSale.IsRefunded = oldSale.IsRefunded;
                targetSale.RefundReason = oldSale.RefundReason;
                targetSale.UpdatedAt = oldSale.UpdatedAt;
                targetSale.SyncFailed = false;
            }

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE SaleItems SET SaleId = {0} WHERE SaleId = {1}",
                newId,
                oldId
            );
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE SalePayments SET SaleId = {0} WHERE SaleId = {1}",
                newId,
                oldId
            );

            db.Sales.Remove(oldSale);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        public async Task UpsertRangeAsync(IEnumerable<Sale> sales)
        {
            if (sales == null)
                throw new ArgumentNullException(nameof(sales));

            await using var db = _factory.CreateDbContext();
            await db.EnsureLocalSchemaAsync();

            var saleList = sales
                .Where(sale => sale != null)
                .GroupBy(sale => sale.SyncId != Guid.Empty ? $"sync:{sale.SyncId}" : $"id:{sale.Id}")
                .Select(group => group.First())
                .ToList();

            if (saleList.Count == 0)
                return;

            var saleIds = saleList.Select(sale => sale.Id).ToList();
            var syncIds = saleList.Where(sale => sale.SyncId != Guid.Empty).Select(sale => sale.SyncId).ToList();
            var existingSales = await db.Sales
                .Include(sale => sale.Items)
                .Include(sale => sale.Payments)
                .Where(sale => saleIds.Contains(sale.Id) || syncIds.Contains(sale.SyncId))
                .ToListAsync();
            var existingById = existingSales.ToDictionary(sale => sale.Id);
            var existingBySyncId = existingSales
                .Where(sale => sale.SyncId != Guid.Empty)
                .GroupBy(sale => sale.SyncId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var sale in saleList)
            {
                ClampSaleDiscount(sale);
                sale.Synced = true;
                sale.SyncFailed = false;

                Sale? localSale = null;
                if (sale.SyncId != Guid.Empty)
                    existingBySyncId.TryGetValue(sale.SyncId, out localSale);
                if (localSale == null && !existingById.TryGetValue(sale.Id, out localSale))
                {
                    var newSale = CloneSaleForInsert(sale);
                    db.Sales.Add(newSale);
                    continue;
                }

                localSale.TenantId = sale.TenantId;
                localSale.OnlineId = sale.OnlineId > 0 ? sale.OnlineId : sale.Id;
                localSale.SyncId = sale.SyncId == Guid.Empty ? localSale.SyncId : sale.SyncId;
                localSale.SourceDeviceId = sale.SourceDeviceId;
                localSale.ClientCreatedAt = sale.ClientCreatedAt;
                localSale.ClientUpdatedAt = sale.ClientUpdatedAt;
                localSale.LastSyncedAt = sale.LastSyncedAt ?? DateTimeOffset.UtcNow;
                localSale.StoreId = sale.StoreId;
                localSale.Timestamp = sale.Timestamp;
                localSale.TotalAmount = sale.TotalAmount;
                localSale.Discount = sale.Discount;
                localSale.TotalPaid = sale.TotalPaid;
                localSale.PaymentSummary = sale.PaymentSummary;
                localSale.PaymentStatus = sale.PaymentStatus;
                localSale.ChangeGiven = sale.ChangeGiven;
                localSale.ReceiptNumber = sale.ReceiptNumber;
                localSale.IsRefunded = sale.IsRefunded;
                localSale.RefundReason = sale.RefundReason;
                localSale.UpdatedAt = sale.UpdatedAt ?? sale.Timestamp;
                localSale.CreatedAt = sale.CreatedAt;
                localSale.DeletedAt = sale.DeletedAt;
                localSale.Synced = true;
                localSale.SyncFailed = false;

                var incomingItems = sale.Items?
                    .GroupBy(item => item.Id)
                    .Select(group => group.First())
                    .ToList()
                    ?? new List<SaleItem>();
                var incomingItemIds = incomingItems.Select(item => item.Id).ToHashSet();
                var existingItemsById = localSale.Items.ToDictionary(item => item.Id);

                var removedItems = localSale.Items
                    .Where(item => !incomingItemIds.Contains(item.Id))
                    .ToList();
                if (removedItems.Count > 0)
                    db.SaleItems.RemoveRange(removedItems);

                foreach (var item in incomingItems)
                {
                    if (!existingItemsById.TryGetValue(item.Id, out var localItem))
                    {
                        localSale.Items.Add(CloneItemForInsert(item, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp));
                        continue;
                    }

                    CopyItem(localItem, item, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp);
                }

                var incomingPayments = sale.Payments?
                    .GroupBy(payment => payment.Id)
                    .Select(group => group.First())
                    .ToList()
                    ?? new List<SalePayment>();
                var incomingPaymentIds = incomingPayments.Select(payment => payment.Id).ToHashSet();
                var existingPaymentsById = localSale.Payments.ToDictionary(payment => payment.Id);

                var removedPayments = localSale.Payments
                    .Where(payment => !incomingPaymentIds.Contains(payment.Id))
                    .ToList();
                if (removedPayments.Count > 0)
                    db.SalePayments.RemoveRange(removedPayments);

                foreach (var payment in incomingPayments)
                {
                    if (!existingPaymentsById.TryGetValue(payment.Id, out var localPayment))
                    {
                        localSale.Payments.Add(ClonePaymentForInsert(payment, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp));
                        continue;
                    }

                    CopyPayment(localPayment, payment, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp);
                }
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteRangeAsync(IEnumerable<int> saleIds)
        {
            await EnsureDemoCanWriteAsync();
            if (saleIds == null)
                throw new ArgumentNullException(nameof(saleIds));

            var ids = saleIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return;

            await using var db = _factory.CreateDbContext();
            var sales = await db.Sales.Where(sale => ids.Contains(sale.Id)).ToListAsync();
            if (sales.Count == 0)
                return;

            db.Sales.RemoveRange(sales);
            await db.SaveChangesAsync();
        }

        private static void ClampSaleDiscount(Sale sale)
        {
            if (sale == null)
                return;

            var discount = sale.Discount;
            if (discount.HasValue && discount.Value > sale.TotalAmount)
                sale.Discount = sale.TotalAmount;
        }

        public async Task MarkAsSyncedAsync(int id)
        {
            await MarkAsSyncedAsync(id, null, null);
        }

        public async Task MarkAsSyncedAsync(int id, int? onlineId, Guid? syncId)
        {
            await using var db = _factory.CreateDbContext();
            var sale = await db.Sales.FindAsync(id);
            if (sale != null)
            {
                if (onlineId.HasValue)
                    sale.OnlineId = onlineId.Value;
                if (syncId.HasValue && syncId.Value != Guid.Empty)
                    sale.SyncId = syncId.Value;
                sale.Synced = true;
                sale.SyncFailed = false;
                sale.LastSyncedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        public async Task MarkAsFailedAsync(int saleId)
        {
            await using var db = _factory.CreateDbContext();
            var sale = await db.Sales.FindAsync(saleId);
            if (sale == null)
                return;

            sale.SyncFailed = true;
            await db.SaveChangesAsync();
        }

        private async Task EnsureDemoCanWriteAsync()
        {
            await _demoState.CheckExpirationAsync();
            if (_demoState.IsExpired)
                throw new InvalidOperationException("Demo session expired. Activate a license to use this in production.");
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

        private static Sale CloneSaleForInsert(Sale sale)
        {
            var clone = new Sale
            {
                Id = sale.Id,
                OnlineId = sale.OnlineId > 0 ? sale.OnlineId : sale.Id,
                SyncId = sale.SyncId == Guid.Empty ? Guid.NewGuid() : sale.SyncId,
                SourceDeviceId = sale.SourceDeviceId,
                ClientCreatedAt = sale.ClientCreatedAt,
                ClientUpdatedAt = sale.ClientUpdatedAt,
                LastSyncedAt = sale.LastSyncedAt ?? DateTimeOffset.UtcNow,
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                Timestamp = sale.Timestamp,
                TotalAmount = sale.TotalAmount,
                Discount = sale.Discount,
                TotalPaid = sale.TotalPaid,
                PaymentSummary = sale.PaymentSummary,
                PaymentStatus = sale.PaymentStatus,
                ChangeGiven = sale.ChangeGiven,
                ReceiptNumber = sale.ReceiptNumber,
                IsRefunded = sale.IsRefunded,
                RefundReason = sale.RefundReason,
                CreatedAt = sale.CreatedAt,
                UpdatedAt = sale.UpdatedAt ?? sale.Timestamp,
                DeletedAt = sale.DeletedAt,
                Synced = true,
                SyncFailed = false,
            };

            foreach (var item in sale.Items ?? Enumerable.Empty<SaleItem>())
                clone.Items.Add(CloneItemForInsert(item, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp));

            foreach (var payment in sale.Payments ?? Enumerable.Empty<SalePayment>())
                clone.Payments.Add(ClonePaymentForInsert(payment, sale.Id, sale.TenantId, sale.StoreId, sale.Timestamp));

            return clone;
        }

        private static Sale CloneSaleHeader(Sale sale, int newId)
        {
            return new Sale
            {
                Id = newId,
                OnlineId = sale.OnlineId,
                SyncId = sale.SyncId == Guid.Empty ? Guid.NewGuid() : sale.SyncId,
                SourceDeviceId = sale.SourceDeviceId,
                ClientCreatedAt = sale.ClientCreatedAt,
                ClientUpdatedAt = sale.ClientUpdatedAt,
                LastSyncedAt = sale.LastSyncedAt,
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                Timestamp = sale.Timestamp,
                TotalAmount = sale.TotalAmount,
                Discount = sale.Discount,
                TotalPaid = sale.TotalPaid,
                PaymentSummary = sale.PaymentSummary,
                PaymentStatus = sale.PaymentStatus,
                ChangeGiven = sale.ChangeGiven,
                ReceiptNumber = sale.ReceiptNumber,
                IsRefunded = sale.IsRefunded,
                RefundReason = sale.RefundReason,
                CreatedAt = sale.CreatedAt,
                UpdatedAt = sale.UpdatedAt ?? sale.Timestamp,
                DeletedAt = sale.DeletedAt,
                Synced = sale.Synced,
                SyncFailed = false,
            };
        }

        private static SaleItem CloneItemForInsert(
            SaleItem item,
            int saleId,
            int tenantId,
            int storeId,
            DateTime saleTimestamp)
        {
            var clone = new SaleItem();
            CopyItem(clone, item, saleId, tenantId, storeId, saleTimestamp);
            return clone;
        }

        private static void CopyItem(
            SaleItem target,
            SaleItem source,
            int saleId,
            int tenantId,
            int storeId,
            DateTime saleTimestamp)
        {
            target.Id = source.Id;
            target.OnlineId = source.OnlineId > 0 ? source.OnlineId : source.Id;
            target.SyncId = source.SyncId == Guid.Empty ? target.SyncId == Guid.Empty ? Guid.NewGuid() : target.SyncId : source.SyncId;
            target.SourceDeviceId = source.SourceDeviceId;
            target.ClientCreatedAt = source.ClientCreatedAt;
            target.ClientUpdatedAt = source.ClientUpdatedAt;
            target.LastSyncedAt = source.LastSyncedAt ?? DateTimeOffset.UtcNow;
            target.SaleId = saleId;
            target.TenantId = source.TenantId != 0 ? source.TenantId : tenantId;
            target.StoreId = source.StoreId != 0 ? source.StoreId : storeId;
            target.ProductId = source.ProductId;
            target.VariantId = source.VariantId;
            target.ProductName = source.ProductName;
            target.ProductCategory = source.ProductCategory;
            target.Quantity = source.Quantity;
            target.UnitPrice = source.UnitPrice;
            target.Subtotal = source.Subtotal > 0
                ? source.Subtotal
                : source.UnitPrice * source.Quantity;
            target.IsRefunded = source.IsRefunded;
            target.RefundedQuantity = source.RefundedQuantity;
            target.RefundReason = source.RefundReason;
            target.RefundedAt = source.RefundedAt;
            target.RefundedByUserId = source.RefundedByUserId;
            target.UpdatedBy = source.UpdatedBy;
            target.CreatedAt = source.CreatedAt;
            target.UpdatedAt = source.UpdatedAt ?? saleTimestamp;
            target.DeletedAt = source.DeletedAt;
        }

        private static SalePayment ClonePaymentForInsert(
            SalePayment payment,
            int saleId,
            int tenantId,
            int storeId,
            DateTime saleTimestamp)
        {
            var clone = new SalePayment();
            CopyPayment(clone, payment, saleId, tenantId, storeId, saleTimestamp);
            return clone;
        }

        private static void CopyPayment(
            SalePayment target,
            SalePayment source,
            int saleId,
            int tenantId,
            int storeId,
            DateTime saleTimestamp)
        {
            target.Id = source.Id;
            target.OnlineId = source.OnlineId > 0 ? source.OnlineId : source.Id;
            target.SyncId = source.SyncId == Guid.Empty ? target.SyncId == Guid.Empty ? Guid.NewGuid() : target.SyncId : source.SyncId;
            target.SourceDeviceId = source.SourceDeviceId;
            target.ClientCreatedAt = source.ClientCreatedAt;
            target.ClientUpdatedAt = source.ClientUpdatedAt;
            target.LastSyncedAt = source.LastSyncedAt ?? DateTimeOffset.UtcNow;
            target.SaleId = saleId;
            target.TenantId = source.TenantId != 0 ? source.TenantId : tenantId;
            target.StoreId = source.StoreId != 0 ? source.StoreId : storeId;
            target.PaymentMethod = source.PaymentMethod;
            target.Amount = source.Amount;
            target.ReferenceNumber = source.ReferenceNumber;
            target.Notes = source.Notes;
            target.CreatedAt = source.CreatedAt == default ? saleTimestamp : source.CreatedAt;
            target.UpdatedAt = source.UpdatedAt;
            target.DeletedAt = source.DeletedAt;
        }
    }
}
