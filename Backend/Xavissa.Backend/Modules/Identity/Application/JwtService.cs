using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xavissa.Backend.Security;

namespace Xavissa.Backend.Services
{
    public interface IJwtService
    {
        string GenerateToken(
            int userId,
            string platformRole,
            string? actingRole,
            IEnumerable<int> allowedTenantIds,
            IEnumerable<int> allowedStoreIds,
            int? selectedTenantId = null,
            int? selectedStoreId = null
        );
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(
            int userId,
            string platformRole,
            string? actingRole,
            IEnumerable<int> allowedTenantIds,
            IEnumerable<int> allowedStoreIds,
            int? selectedTenantId = null,
            int? selectedStoreId = null
        )
        {
            var jwtKey = _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
                throw new InvalidOperationException("Jwt:Key is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("userId", userId.ToString()),
                new(ClaimTypes.Role, platformRole),
                new(ClaimNames.PlatformRole, platformRole),
                new(ClaimNames.AccessRole, platformRole.IsPlatformAdmin() ? AccessRoles.SystemAdmin : AccessRoles.User),
            };

            if (!string.IsNullOrWhiteSpace(actingRole))
                claims.Add(new Claim(ClaimNames.ActingRole, actingRole));

            foreach (var tenantId in allowedTenantIds.Distinct())
                claims.Add(new Claim(ClaimNames.TenantId, tenantId.ToString()));

            foreach (var storeId in allowedStoreIds.Distinct())
                claims.Add(new Claim(ClaimNames.StoreId, storeId.ToString()));

            if (selectedTenantId.HasValue)
                claims.Add(new Claim("selected_tenant_id", selectedTenantId.Value.ToString()));

            if (selectedStoreId.HasValue)
                claims.Add(new Claim("selected_store_id", selectedStoreId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
