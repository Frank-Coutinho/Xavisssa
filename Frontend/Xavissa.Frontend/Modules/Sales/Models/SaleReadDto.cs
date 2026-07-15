using System;
using System.Collections.Generic;

namespace Xavissa.Frontend.Models
{
    public class SaleReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? Discount { get; set; }
        public decimal TotalPaid { get; set; }
        public string PaymentSummary { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal? ChangeGiven { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public bool IsRefunded { get; set; }
        public string? RefundReason { get; set; }
        public List<SaleItemReadDto> SaleItems { get; set; } = new();
        public List<SalePaymentReadDto> SalePayments { get; set; } = new();
    }

    public class SaleItemReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int ProductId { get; set; }
        public int VariantId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string ProductCategory { get; set; } = string.Empty;
        public bool IsRefunded { get; set; }
        public int RefundedQuantity { get; set; }
        public int RefundableQuantity { get; set; }
        public string? RefundReason { get; set; }
    }

    public class SalePaymentReadDto
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }
}
