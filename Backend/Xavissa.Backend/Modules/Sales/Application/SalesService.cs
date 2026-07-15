using Microsoft.EntityFrameworkCore;
using Xavissa.Backend.DTOs;
using Xavissa.Backend.Security;
using Xavissa.Database;
using Xavissa.Database.Models;
using Xavissa.Database.ViewModels;

namespace Xavissa.Backend.Services
{
    public class SalesService
    {
        private readonly XavissaDbContext _db;
        private readonly TenantAccessService _tenantAccess;

        public SalesService(XavissaDbContext db, TenantAccessService tenantAccess)
        {
            _db = db;
            _tenantAccess = tenantAccess;
        }

        public async Task<Sale> CreateSaleAsync(
            List<SaleItemDto> itemsDto,
            List<SalePaymentDto> paymentsDto,
            int userId,
            int tenantId,
            int storeId,
            decimal discount = 0,
            Guid syncId = default,
            string? sourceDeviceId = null,
            DateTimeOffset? clientCreatedAt = null,
            DateTimeOffset? clientUpdatedAt = null
        )
        {
            if (itemsDto == null || itemsDto.Count == 0)
                throw new ArgumentException("A sale must contain at least one item.");
            if (!_tenantAccess.CanAccessStore(storeId))
                throw new UnauthorizedAccessException("Unauthorized store.");

            var now = DateTime.UtcNow;
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var variantIds = itemsDto.Where(i => i.VariantId > 0).Select(i => i.VariantId).Distinct().ToList();
            if (variantIds.Count != itemsDto.Count)
                throw new ArgumentException("Every sale item must reference a valid variant.");

            var variants = await _db.ProductVariants
                .IgnoreQueryFilters()
                .Where(v =>
                    v.TenantId == tenantId
                    && variantIds.Contains(v.Id)
                    && v.ProductStoreAssignment != null
                    && v.ProductStoreAssignment.StoreId == storeId
                    && v.IsActive
                    && v.ProductStoreAssignment.Product.IsActive
                    && v.ProductStoreAssignment.IsActive
                )
                .Include(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .ToDictionaryAsync(v => v.Id);

            var saleItems = new List<SaleItem>();
            var stockMovements = new List<StockMovement>();

            foreach (var itemDto in itemsDto)
            {
                if (itemDto.Quantity <= 0)
                    throw new ArgumentException("Sale item quantity must be greater than zero.");

                if (!variants.TryGetValue(itemDto.VariantId, out var variant))
                    throw new ArgumentException($"Variant with ID {itemDto.VariantId} not found.");

                var productName = variant.ProductStoreAssignment?.Product?.Name ?? $"variant {variant.Id}";
                if (variant.Price == null)
                    throw new InvalidOperationException($"Variant '{productName}' cannot be sold because it has no price.");

                var updatedRows = await _db.StockLevels
                    .IgnoreQueryFilters()
                    .Where(sl =>
                        sl.TenantId == tenantId
                        && sl.StoreId == storeId
                        && sl.VariantId == itemDto.VariantId
                        && sl.QuantityOnHand >= itemDto.Quantity)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(sl => sl.QuantityOnHand, sl => sl.QuantityOnHand - itemDto.Quantity)
                        .SetProperty(sl => sl.UpdatedAt, now)
                        .SetProperty(sl => sl.UpdatedBy, userId));

                if (updatedRows == 0)
                    throw new InvalidOperationException($"Insufficient stock for product '{productName}'.");

                saleItems.Add(new SaleItem
                {
                    SyncId = itemDto.SyncId == Guid.Empty ? Guid.NewGuid() : itemDto.SyncId,
                    SourceDeviceId = itemDto.SourceDeviceId ?? sourceDeviceId,
                    ClientCreatedAt = itemDto.ClientCreatedAt ?? clientCreatedAt,
                    ClientUpdatedAt = itemDto.ClientUpdatedAt ?? clientUpdatedAt,
                    TenantId = tenantId,
                    StoreId = storeId,
                    VariantId = variant.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = variant.Price.Value,
                    IsRefunded = false,
                    RefundedQuantity = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });

                stockMovements.Add(new StockMovement
                {
                    TenantId = tenantId,
                    StoreId = storeId,
                    VariantId = variant.Id,
                    Quantity = -itemDto.Quantity,
                    MovementType = "Sale",
                    ReferenceType = "Sale",
                    Notes = $"Sale created by user {userId}",
                    CreatedAt = now,
                    CreatedBy = userId,
                });
            }

            var total = saleItems.Sum(i => i.UnitPrice * i.Quantity);
            var appliedDiscount = Math.Min(discount, total);
            var finalTotal = Math.Max(total - appliedDiscount, 0);
            var settings = await _db.StoreOperationalSettings
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.StoreId == storeId);
            var cashRegisterMode = NormalizeCashRegisterMode(settings?.CashRegisterMode);
            var payments = NormalizePayments(paymentsDto, finalTotal, tenantId, storeId, userId, now, sourceDeviceId);
            var hasCashPayment = payments.Any(IsCashPayment);
            var openSession = hasCashPayment
                ? await FindOpenRegisterSessionAsync(tenantId, storeId, userId, sourceDeviceId)
                : null;

            if (hasCashPayment && cashRegisterMode == CashRegisterModes.Required && openSession == null)
                throw new InvalidOperationException("An open cash register session is required before completing a cash sale.");

            if (hasCashPayment && openSession != null && cashRegisterMode != CashRegisterModes.Disabled)
            {
                foreach (var payment in payments.Where(IsCashPayment))
                    payment.CashRegisterSessionId = openSession.Id;
            }

            var untrackedCash = payments.Any(payment => IsCashPayment(payment) && payment.CashRegisterSessionId == null);
            var totalPaid = payments.Sum(x => x.Amount);

            var sale = new Sale
            {
                SyncId = syncId == Guid.Empty ? Guid.NewGuid() : syncId,
                SourceDeviceId = sourceDeviceId,
                ClientCreatedAt = clientCreatedAt,
                ClientUpdatedAt = clientUpdatedAt,
                TenantId = tenantId,
                StoreId = storeId,
                SaleDate = now,
                TotalAmount = finalTotal,
                Discount = appliedDiscount,
                PaymentStatus = totalPaid >= finalTotal ? "Paid" : "Partial",
                ChangeGiven = totalPaid > finalTotal ? totalPaid - finalTotal : 0,
                ReceiptNumber = $"RC-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..5]}",
                Status = "Completed",
                CashRegisterSessionId = payments.Any(x => x.CashRegisterSessionId.HasValue)
                    ? openSession?.Id
                    : null,
                CashRegisterTrackingMode = cashRegisterMode,
                HasUntrackedCashPayment = untrackedCash,
                CreatedBy = userId,
                UpdatedBy = userId,
                SaleItems = saleItems,
                Payments = payments,
            };

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            foreach (var movement in stockMovements)
                movement.ReferenceId = sale.Id;

            _db.StockMovements.AddRange(stockMovements);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return sale;
        }

        public async Task<List<Sale>> GetAllSalesAsync()
        {
            var query = BaseSaleQuery().OrderByDescending(s => s.SaleDate);
            if (_tenantAccess.SelectedStoreId.HasValue)
                query = query.Where(s => s.StoreId == _tenantAccess.SelectedStoreId.Value).OrderByDescending(s => s.SaleDate);
            return await query.ToListAsync();
        }

        public async Task<Sale?> GetSaleByIdAsync(int id)
        {
            return await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<SalesReportViewModel> GetSalesSummaryAsync(DateTime? start = null, DateTime? end = null)
        {
            var query = _db.Sales.AsQueryable();
            if (_tenantAccess.SelectedStoreId.HasValue)
                query = query.Where(s => s.StoreId == _tenantAccess.SelectedStoreId.Value);
            if (start.HasValue)
                query = query.Where(s => s.SaleDate >= start.Value);
            if (end.HasValue)
                query = query.Where(s => s.SaleDate <= end.Value);

            var salesList = await query.ToListAsync();
            if (!salesList.Any())
            {
                return new SalesReportViewModel
                {
                    StartDate = start ?? DateTime.UtcNow,
                    EndDate = end ?? DateTime.UtcNow,
                    TotalSalesCount = 0,
                    TotalRevenue = 0,
                    AverageSaleValue = 0,
                };
            }

            return new SalesReportViewModel
            {
                StartDate = start ?? salesList.Min(s => s.SaleDate),
                EndDate = end ?? salesList.Max(s => s.SaleDate),
                TotalSalesCount = salesList.Count,
                TotalRevenue = salesList.Sum(s => s.TotalAmount),
                AverageSaleValue = salesList.Average(s => s.TotalAmount),
            };
        }

        public async Task<bool> DeleteSaleAsync(int id)
        {
            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == id);
            if (sale == null)
                return false;
            if (!_tenantAccess.CanAccessStore(sale.StoreId))
                throw new UnauthorizedAccessException("Unauthorized store.");

            _db.Sales.Remove(sale);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SoftDeleteSaleAsync(int saleId, string reason)
        {
            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return false;

            if (!CanManageSaleDeletion(sale))
                throw new UnauthorizedAccessException("Unauthorized store.");

            reason = NormalizeDeletionReason(reason, "Sale deleted from history.");

            foreach (var item in sale.SaleItems)
                RestoreStockForDeletedItem(sale, item, reason);

            AddDeletionAuditLog(
                sale.TenantId,
                sale.StoreId,
                "Sale",
                sale.Id.ToString(),
                $"Sale {sale.ReceiptNumber} soft-deleted. Reason: {reason}");

            sale.IsRefunded = true;
            sale.IsVoided = true;
            sale.Status = "Voided";
            sale.PaymentStatus = "Voided";
            sale.RefundReason = reason;
            sale.VoidReason = reason;
            sale.VoidedAt = DateTime.UtcNow;
            sale.VoidedByUserId = _tenantAccess.CurrentUserId;
            sale.UpdatedAt = DateTime.UtcNow;
            sale.UpdatedBy = _tenantAccess.CurrentUserId;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SoftDeleteSaleItemAsync(int saleId, int saleItemId, string reason)
        {
            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return false;

            if (!CanManageSaleDeletion(sale))
                throw new UnauthorizedAccessException("Unauthorized store.");

            var item = sale.SaleItems.FirstOrDefault(x => x.Id == saleItemId);
            if (item == null)
                return false;

            reason = NormalizeDeletionReason(reason, "Sale item deleted from history.");

            RestoreStockForDeletedItem(sale, item, reason);

            AddDeletionAuditLog(
                sale.TenantId,
                sale.StoreId,
                "SaleItem",
                item.Id.ToString(),
                $"Sale item removed from sale {sale.ReceiptNumber}. Reason: {reason}");

            item.IsRefunded = true;
            item.RefundedQuantity = item.Quantity;
            item.RefundReason = reason;
            item.RefundedAt = DateTime.UtcNow;
            item.RefundedByUserId = _tenantAccess.CurrentUserId;
            RecalculateSaleTotals(sale);

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RefundSaleAsync(int saleId, string reason)
        {
            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return false;
            if (!_tenantAccess.CanAccessStore(sale.StoreId))
                throw new UnauthorizedAccessException("Unauthorized store.");

            foreach (var item in sale.SaleItems)
            {
                var refundableQuantity = GetRefundableQuantity(item);
                if (refundableQuantity <= 0)
                    continue;

                await RefundSaleItemInternalAsync(sale, item, refundableQuantity, reason);
            }

            UpdateSaleRefundStatus(sale, reason);

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RefundSaleItemAsync(int saleId, int saleItemId, int quantity, string reason)
        {
            if (quantity <= 0)
                throw new ArgumentException("Refund quantity must be greater than zero.");

            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return false;
            if (!_tenantAccess.CanAccessStore(sale.StoreId))
                throw new UnauthorizedAccessException("Unauthorized store.");

            var item = sale.SaleItems.FirstOrDefault(x => x.Id == saleItemId);
            if (item == null)
                return false;

            var refundableQuantity = GetRefundableQuantity(item);
            if (refundableQuantity <= 0)
                throw new InvalidOperationException("This sale item has already been fully refunded.");
            if (quantity > refundableQuantity)
                throw new InvalidOperationException($"Only {refundableQuantity} item(s) can still be refunded.");

            await RefundSaleItemInternalAsync(sale, item, quantity, reason);
            UpdateSaleRefundStatus(sale, reason);

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> VoidSaleAsync(int saleId, string reason)
        {
            var sale = await BaseSaleQuery().FirstOrDefaultAsync(s => s.Id == saleId);
            if (sale == null)
                return false;
            if (!CanManageSaleDeletion(sale))
                throw new UnauthorizedAccessException("Unauthorized store.");
            if (sale.IsVoided || string.Equals(sale.Status, "Voided", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Sale is already voided.");

            reason = NormalizeDeletionReason(reason, "Sale voided.");
            await using var tx = await _db.Database.BeginTransactionAsync();
            foreach (var item in sale.SaleItems)
            {
                var quantity = GetRefundableQuantity(item);
                if (quantity <= 0)
                    continue;

                await RefundSaleItemInternalAsync(sale, item, quantity, reason, "Void");
            }

            sale.IsVoided = true;
            sale.Status = "Voided";
            sale.PaymentStatus = "Voided";
            sale.VoidedAt = DateTime.UtcNow;
            sale.VoidedByUserId = _tenantAccess.CurrentUserId;
            sale.VoidReason = reason;
            sale.UpdatedAt = DateTime.UtcNow;
            sale.UpdatedBy = _tenantAccess.CurrentUserId;

            _db.AuditLogs.Add(new AuditLog
            {
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                UserId = _tenantAccess.CurrentUserId,
                EntityName = "Sale",
                EntityId = sale.Id.ToString(),
                ActionType = "Void",
                Description = $"Sale {sale.ReceiptNumber} voided. Reason: {reason}",
                CreatedAt = DateTime.UtcNow,
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        public string GenerateReceiptContent(Sale sale)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("             XAVISSA SYSTEM              ");
            sb.AppendLine("========================================");
            sb.AppendLine($"Receipt No: {sale.ReceiptNumber}");
            sb.AppendLine($"Date: {sale.SaleDate:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Clerk: {sale.CreatedBy?.ToString() ?? "System User"}");
            sb.AppendLine("----------------------------------------");
            foreach (var item in sale.SaleItems)
            {
                var lineGross = item.UnitPrice * item.Quantity;
                var lineNet = lineGross;
                sb.AppendLine(item.Variant?.Product?.Name ?? "Unknown");
                sb.AppendLine($"{item.Quantity} x {item.UnitPrice:C2} = {lineNet:C2}");
            }
            sb.AppendLine("----------------------------------------");
            sb.AppendLine($"TOTAL: {sale.TotalAmount:C2}");
            sb.AppendLine($"Payment: {BuildPaymentSummary(sale.Payments)}");
            sb.AppendLine($"Amount Paid: {sale.Payments.Sum(x => x.Amount):C2}");
            return sb.ToString();
        }

        private IQueryable<Sale> BaseSaleQuery()
        {
            return _db.Sales.Include(s => s.SaleItems)
                .ThenInclude(si => si.Variant)
                .ThenInclude(v => v.ProductStoreAssignment!)
                .ThenInclude(a => a.Product)
                .ThenInclude(p => p.CategoryNavigation)
                .Include(s => s.Payments);
        }

        private void RestoreStockForDeletedItem(Sale sale, SaleItem item, string reason)
        {
            var quantityToRestore = GetRefundableQuantity(item);
            if (quantityToRestore <= 0)
                return;

            var stockLevel = _db.StockLevels.FirstOrDefault(sl =>
                sl.TenantId == sale.TenantId
                && sl.StoreId == sale.StoreId
                && sl.VariantId == item.VariantId);

            var nextQuantity = (stockLevel?.QuantityOnHand ?? 0) + quantityToRestore;
            UpsertStockLevel(sale.TenantId ?? 0, sale.StoreId, item.VariantId, nextQuantity, _tenantAccess.CurrentUserId);

            if (item.Variant != null)
                item.Variant.UpdatedAt = DateTime.UtcNow;

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                VariantId = item.VariantId,
                Quantity = quantityToRestore,
                MovementType = "DeleteSale",
                ReferenceType = "Sale",
                ReferenceId = sale.Id,
                Notes = reason,
                CreatedBy = _tenantAccess.CurrentUserId,
            });
        }

        private void RecalculateSaleTotals(Sale sale)
        {
            var remainingItems = sale.SaleItems.Where(x => x.RefundedQuantity < x.Quantity).ToList();
            var grossSubtotal = remainingItems.Sum(x => x.UnitPrice * x.Quantity);
            var saleDiscount = Math.Min(sale.Discount ?? 0, grossSubtotal);
            var newTotal = Math.Max(grossSubtotal - saleDiscount, 0);

            sale.Discount = saleDiscount;
            sale.TotalAmount = newTotal;
            var totalPaid = sale.Payments.Sum(x => x.Amount);
            sale.PaymentStatus = totalPaid >= newTotal ? "Paid" : "Partial";
            sale.ChangeGiven = totalPaid > newTotal ? totalPaid - newTotal : 0;
            sale.UpdatedAt = DateTime.UtcNow;
            sale.UpdatedBy = _tenantAccess.CurrentUserId;
        }

        private void AddDeletionAuditLog(int? tenantId, int storeId, string entityName, string entityId, string description)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = _tenantAccess.CurrentUserId,
                EntityName = entityName,
                EntityId = entityId,
                ActionType = "SoftDelete",
                Description = description,
                CreatedAt = DateTime.UtcNow,
            });
        }

        private bool CanManageSaleDeletion(Sale sale)
        {
            if (_tenantAccess.CanManageStore(sale.StoreId))
                return true;

            return sale.TenantId.HasValue && _tenantAccess.CanManageTenant(sale.TenantId.Value);
        }

        private static string NormalizeDeletionReason(string? reason, string fallback)
        {
            return string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();
        }

        private static int GetRefundableQuantity(SaleItem item)
        {
            return Math.Max(item.Quantity - item.RefundedQuantity, 0);
        }

        private async Task RefundSaleItemInternalAsync(
            Sale sale,
            SaleItem item,
            int quantity,
            string reason,
            string movementType = "Refund")
        {
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Refunded" : reason.Trim();
            var stockLevel = await _db.StockLevels.FirstOrDefaultAsync(sl =>
                sl.TenantId == sale.TenantId
                && sl.StoreId == sale.StoreId
                && sl.VariantId == item.VariantId);

            var nextQuantity = (stockLevel?.QuantityOnHand ?? 0) + quantity;
            UpsertStockLevel(sale.TenantId ?? 0, sale.StoreId, item.VariantId, nextQuantity, _tenantAccess.CurrentUserId);

            if (item.Variant != null)
                item.Variant.UpdatedAt = DateTime.UtcNow;

            item.RefundedQuantity += quantity;
            item.IsRefunded = item.RefundedQuantity >= item.Quantity;
            item.RefundReason = normalizedReason;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = _tenantAccess.CurrentUserId;

            _db.StockMovements.Add(new StockMovement
            {
                TenantId = sale.TenantId,
                StoreId = sale.StoreId,
                VariantId = item.VariantId,
                Quantity = quantity,
                MovementType = movementType,
                ReferenceType = "SaleItem",
                ReferenceId = item.Id,
                Notes = normalizedReason,
                CreatedBy = _tenantAccess.CurrentUserId,
            });
        }

        private void UpdateSaleRefundStatus(Sale sale, string reason)
        {
            var isFullyRefunded = sale.SaleItems.Count > 0 && sale.SaleItems.All(x => x.RefundedQuantity >= x.Quantity);
            sale.IsRefunded = isFullyRefunded;
            sale.Status = isFullyRefunded ? "Refunded" : "PartiallyRefunded";
            sale.RefundReason = isFullyRefunded
                ? (string.IsNullOrWhiteSpace(reason) ? "Refunded" : reason.Trim())
                : null;
            sale.UpdatedAt = DateTime.UtcNow;
            sale.UpdatedBy = _tenantAccess.CurrentUserId;
        }

        private void UpsertStockLevel(int tenantId, int storeId, int variantId, int quantityOnHand, int? updatedBy)
        {
            var stockLevel = _db.StockLevels.FirstOrDefault(x => x.TenantId == tenantId && x.StoreId == storeId && x.VariantId == variantId);
            if (stockLevel == null)
            {
                _db.StockLevels.Add(new StockLevel
                {
                    TenantId = tenantId,
                    StoreId = storeId,
                    VariantId = variantId,
                    QuantityOnHand = quantityOnHand,
                    UpdatedBy = updatedBy,
                    UpdatedAt = DateTime.UtcNow,
                });
                return;
            }

            stockLevel.QuantityOnHand = quantityOnHand;
            stockLevel.UpdatedBy = updatedBy;
            stockLevel.UpdatedAt = DateTime.UtcNow;
        }

        private static List<SalePayment> NormalizePayments(
            List<SalePaymentDto>? paymentsDto,
            decimal finalTotal,
            int tenantId,
            int storeId,
            int userId,
            DateTime now,
            string? saleSourceDeviceId)
        {
            var source = paymentsDto?.Where(x => x.Amount > 0).ToList() ?? new List<SalePaymentDto>();
            if (source.Count == 0)
                source.Add(new SalePaymentDto { PaymentMethod = PaymentMethods.Cash, Amount = finalTotal });

            return source.Select(payment => new SalePayment
            {
                SyncId = payment.SyncId == Guid.Empty ? Guid.NewGuid() : payment.SyncId,
                SourceDeviceId = payment.SourceDeviceId ?? saleSourceDeviceId,
                ClientCreatedAt = payment.ClientCreatedAt,
                ClientUpdatedAt = payment.ClientUpdatedAt,
                TenantId = tenantId,
                StoreId = storeId,
                PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ? PaymentMethods.Cash : payment.PaymentMethod.Trim(),
                Amount = payment.Amount,
                ReferenceNumber = payment.ReferenceNumber,
                Notes = payment.Notes,
                CreatedAt = now,
                CreatedBy = userId,
            }).ToList();
        }

        private Task<CashRegisterSession?> FindOpenRegisterSessionAsync(
            int tenantId,
            int storeId,
            int userId,
            string? sourceDeviceId)
        {
            var query = _db.CashRegisterSessions
                .IgnoreQueryFilters()
                .Where(x =>
                    x.TenantId == tenantId
                    && x.StoreId == storeId
                    && x.OpenedByUserId == userId
                    && x.Status == "Open");

            if (!string.IsNullOrWhiteSpace(sourceDeviceId))
                query = query.Where(x => x.SourceDeviceId == sourceDeviceId);

            return query.OrderByDescending(x => x.OpenedAt).FirstOrDefaultAsync();
        }

        private static string NormalizeCashRegisterMode(string? mode)
        {
            if (string.Equals(mode, CashRegisterModes.Required, StringComparison.OrdinalIgnoreCase))
                return CashRegisterModes.Required;
            if (string.Equals(mode, CashRegisterModes.Optional, StringComparison.OrdinalIgnoreCase))
                return CashRegisterModes.Optional;
            return CashRegisterModes.Disabled;
        }

        private static bool IsCashPayment(SalePayment payment)
        {
            return string.Equals(payment.PaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPaymentSummary(IEnumerable<SalePayment> payments)
        {
            var parts = payments
                .GroupBy(x => x.PaymentMethod)
                .Select(x => $"{x.Key}: {x.Sum(p => p.Amount):0.00}")
                .ToList();
            return parts.Count == 0 ? PaymentMethods.Cash : string.Join(", ", parts);
        }
    }
}

