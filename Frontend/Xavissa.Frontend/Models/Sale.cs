using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xavissa.Frontend.Models
{
    public class Sale
    {
        public int Id { get; set; }
        public int OnlineId { get; set; }
        public Guid SyncId { get; set; } = Guid.NewGuid();
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }

        [JsonPropertyName("saleDate")]
        public DateTime Timestamp { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal? Discount { get; set; }
        public decimal TotalPaid { get; set; }
        public string PaymentSummary { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Paid";
        public decimal? ChangeGiven { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public bool IsRefunded { get; set; }
        public string? RefundReason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool Synced { get; set; }
        [JsonPropertyName("saleItems")]
        public List<SaleItem> Items { get; set; } = new();
        public List<SalePayment> Payments { get; set; } = new();
        public bool SyncFailed { get; set; }
    }
}
