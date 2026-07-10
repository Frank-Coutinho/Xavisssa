namespace Xavissa.Backend.DTOs
{
    public class SaleCreateDto
    {
        public int? OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public required List<SaleItemDto> SaleItems { get; set; }
        public decimal? Discount { get; set; }
        public List<SalePaymentDto> SalePayments { get; set; } = new();
    }

    public class SaleItemDto
    {
        public int? OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public int VariantId { get; set; }
        public int? ProductId { get; set; }
        public int Quantity { get; set; }
        public bool IsRefunded { get; set; }
        public string? RefundReason { get; set; }
    }

    public class SalePaymentDto
    {
        public int? OnlineId { get; set; }
        public Guid SyncId { get; set; }
        public int? TenantId { get; set; }
        public int? StoreId { get; set; }
        public string? SourceDeviceId { get; set; }
        public DateTimeOffset? ClientCreatedAt { get; set; }
        public DateTimeOffset? ClientUpdatedAt { get; set; }
        public DateTimeOffset? LastSyncedAt { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
    }
}
