using System.Collections.Generic;

namespace Xavissa.Frontend.Models
{
    public class Claim
    {
        public string type { get; set; } = string.Empty;
        public string value { get; set; } = string.Empty;
    }

    public class User
    {
        public int Id { get; set; }
        public string username { get; set; } = string.Empty;
        public string email { get; set; } = string.Empty;
        public UserRole? role { get; set; }
        public string claimTypesRole { get; set; } = string.Empty;
        public List<Claim> allClaims { get; set; } = new List<Claim>();

        public string DisplayName => username;
    }
}
