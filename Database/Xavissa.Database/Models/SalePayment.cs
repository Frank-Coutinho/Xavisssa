namespace Xavissa.Database.Models;

public class SalePayment : ITenantScopedEntity, IStoreScopedEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public int? CashRegisterSessionId { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
}
