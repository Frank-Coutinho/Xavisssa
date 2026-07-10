namespace Xavissa.Database.Models;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string MobilePayment = "MobilePayment";
    public const string Other = "Other";
}

public class Sale : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public decimal? Discount { get; set; }
    public bool IsRefunded { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string? RefundReason { get; set; }
    public int StoreId { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? TenantId { get; set; }
    public string PaymentStatus { get; set; } = "Paid";
    public decimal? ChangeGiven { get; set; }
    public int? CashRegisterSessionId { get; set; }
    public string? CashRegisterTrackingMode { get; set; }
    public bool HasUntrackedCashPayment { get; set; }
    public string Status { get; set; } = "Completed";
    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }
    public int? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
}
