namespace Xavissa.Database.Models;

public class DemoTemplate : IAuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TemplateTenantId { get; set; }
    public Tenant TemplateTenant { get; set; } = null!;
    public string SeedVersion { get; set; } = "v1";
    public int DefaultDurationMinutes { get; set; } = 1440;
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public ICollection<DemoSession> DemoSessions { get; set; } = new List<DemoSession>();
}
