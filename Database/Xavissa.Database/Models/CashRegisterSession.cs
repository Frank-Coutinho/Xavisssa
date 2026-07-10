namespace Xavissa.Database.Models;

public class CashRegisterSession : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity, IOfflineSyncEntity
{
    public int Id { get; set; }
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public string? SourceDeviceId { get; set; }
    public DateTimeOffset? ClientCreatedAt { get; set; }
    public DateTimeOffset? ClientUpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public int OpenedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCashAmount { get; set; }
    public decimal? ExpectedCashAmount { get; set; }
    public decimal? CountedCashAmount { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<CashRegisterCashMovement> CashMovements { get; set; } = new List<CashRegisterCashMovement>();
    public ICollection<SalePayment> SalePayments { get; set; } = new List<SalePayment>();
}
