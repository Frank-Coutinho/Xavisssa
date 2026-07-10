using System.ComponentModel.DataAnnotations.Schema;

namespace Xavissa.Database.Models;

public class TenantUser : IAuditableEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? TenantRoleId { get; set; }
    public Role? TenantRole { get; set; }
    [NotMapped]
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
