namespace Xavissa.Database.Models;

public class DemoSession : ITenantScopedEntity
{
    public int Id { get; set; }
    public string DemoTokenHash { get; set; } = string.Empty;
    public int? DemoTemplateId { get; set; }
    public DemoTemplate? DemoTemplate { get; set; }
    public int? TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? AppVersion { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsActive { get; set; } = true;
    public bool ResetOnClose { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DemoSessionEvent> Events { get; set; } = new List<DemoSessionEvent>();
}
