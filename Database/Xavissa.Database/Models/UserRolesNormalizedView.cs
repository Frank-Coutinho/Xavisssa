namespace Xavissa.Database.Models;

public class UserRolesNormalizedView
{
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public int? PlatformRoleId { get; set; }
    public string? PlatformRoleCode { get; set; }
    public int? TenantId { get; set; }
    public int? TenantRoleId { get; set; }
    public string? TenantRoleCode { get; set; }
    public int? StoreId { get; set; }
    public int? StoreRoleId { get; set; }
    public string? StoreRoleCode { get; set; }
}
