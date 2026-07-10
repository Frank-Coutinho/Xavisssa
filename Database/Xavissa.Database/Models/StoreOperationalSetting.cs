namespace Xavissa.Database.Models;

public static class CashRegisterModes
{
    public const string Disabled = "Disabled";
    public const string Optional = "Optional";
    public const string Required = "Required";
}

public class StoreOperationalSetting : ITenantScopedEntity, IStoreScopedEntity, IAuditableEntity
{
    public int Id { get; set; }
    public int? TenantId { get; set; }
    public int StoreId { get; set; }
    public string CashRegisterMode { get; set; } = CashRegisterModes.Disabled;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedBy { get; set; }

    public Store Store { get; set; } = null!;
}
