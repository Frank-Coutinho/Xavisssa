using System;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Frontend.Models
{
    public class SaleItem
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
        public int SaleId { get; set; }
        public Sale Sale { get; set; } = null!;
        public int ProductId { get; set; }
        public int VariantId { get; set; }
        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public string ProductCategory { get; set; } = string.Empty;
        public bool IsRefunded { get; set; }
        public int RefundedQuantity { get; set; }
        [NotMapped]
        public int RefundableQuantity { get; set; }
        public string? RefundReason { get; set; }
        public DateTime? RefundedAt { get; set; }
        public int? RefundedByUserId { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
