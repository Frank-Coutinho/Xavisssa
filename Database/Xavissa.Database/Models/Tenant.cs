namespace Xavissa.Database.Models;

public class Tenant : IAuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDemo { get; set; }
    public bool IsDemoTemplate { get; set; }
    public DateTime? DemoExpiresAt { get; set; }
    public int? SourceDemoTemplateId { get; set; }
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<Store> Stores { get; set; } = new List<Store>();
    public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    public ICollection<DemoSession> DemoSessions { get; set; } = new List<DemoSession>();
}
