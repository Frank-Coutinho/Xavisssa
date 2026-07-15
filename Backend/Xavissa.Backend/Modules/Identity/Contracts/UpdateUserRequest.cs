namespace Xavissa.Backend.DTOs
{
    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? PlatformRole { get; set; }
        public int? PlatformRoleId { get; set; }
        public string? PlatformRoleCode { get; set; }
        public bool? IsActive { get; set; }
    }
}
