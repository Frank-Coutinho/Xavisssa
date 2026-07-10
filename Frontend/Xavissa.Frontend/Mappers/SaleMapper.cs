using System;
using System.Linq;
using Xavissa.Frontend.Models;

namespace Xavissa.Frontend.Mappers
{
    public static class SaleMapper
    {
        public static Sale FromReadDto(SaleReadDto dto)
        {
            return new Sale
            {
                Id = dto.Id,
                OnlineId = dto.OnlineId > 0 ? dto.OnlineId : dto.Id,
                SyncId = dto.SyncId == Guid.Empty ? Guid.NewGuid() : dto.SyncId,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                SourceDeviceId = dto.SourceDeviceId,
                ClientCreatedAt = dto.ClientCreatedAt,
                ClientUpdatedAt = dto.ClientUpdatedAt,
                LastSyncedAt = dto.LastSyncedAt ?? DateTimeOffset.UtcNow,
                Timestamp = dto.SaleDate,
                TotalAmount = dto.TotalAmount,
                Discount = dto.Discount,
                TotalPaid = dto.TotalPaid,
                PaymentSummary = dto.PaymentSummary,
                PaymentStatus = dto.PaymentStatus,
                ChangeGiven = dto.ChangeGiven,
                ReceiptNumber = dto.ReceiptNumber,
                IsRefunded = dto.IsRefunded,
                RefundReason = dto.RefundReason,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? dto.SaleDate,
                DeletedAt = dto.DeletedAt,
                Synced = true,
                Items = dto.SaleItems.Select(FromReadDto).ToList(),
                Payments = dto.SalePayments.Select(FromReadDto).ToList(),
            };
        }

        private static SaleItem FromReadDto(SaleItemReadDto dto)
        {
            return new SaleItem
            {
                Id = dto.Id,
                OnlineId = dto.OnlineId > 0 ? dto.OnlineId : dto.Id,
                SyncId = dto.SyncId == Guid.Empty ? Guid.NewGuid() : dto.SyncId,
                TenantId = dto.TenantId,
                StoreId = dto.StoreId,
                SourceDeviceId = dto.SourceDeviceId,
                ClientCreatedAt = dto.ClientCreatedAt,
                ClientUpdatedAt = dto.ClientUpdatedAt,
                LastSyncedAt = dto.LastSyncedAt ?? DateTimeOffset.UtcNow,
                ProductId = dto.ProductId,
                VariantId = dto.VariantId,
                ProductName = dto.ProductName,
                ProductCategory = dto.ProductCategory,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                Subtotal = dto.Subtotal,
                IsRefunded = dto.IsRefunded,
                RefundedQuantity = dto.RefundedQuantity,
                RefundableQuantity = dto.RefundableQuantity,
                RefundReason = dto.RefundReason,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt ?? (dto.Id > 0 ? DateTime.UtcNow : null),
                DeletedAt = dto.DeletedAt,
            };
        }

        private static SalePayment FromReadDto(SalePaymentReadDto dto)
        {
            return new SalePayment
            {
                Id = dto.Id,
                OnlineId = dto.OnlineId > 0 ? dto.OnlineId : dto.Id,
                SyncId = dto.SyncId == Guid.Empty ? Guid.NewGuid() : dto.SyncId,
                SourceDeviceId = dto.SourceDeviceId,
                ClientCreatedAt = dto.ClientCreatedAt,
                ClientUpdatedAt = dto.ClientUpdatedAt,
                LastSyncedAt = dto.LastSyncedAt ?? DateTimeOffset.UtcNow,
                PaymentMethod = dto.PaymentMethod,
                Amount = dto.Amount,
                ReferenceNumber = dto.ReferenceNumber,
                Notes = dto.Notes,
                CreatedAt = dto.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = dto.UpdatedAt,
                DeletedAt = dto.DeletedAt,
            };
        }
    }
}
