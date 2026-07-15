namespace Xavissa.Frontend.Models
{
    public class UserStoreAssignment
    {
        public int UserId { get; set; }
        public int TenantId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
