using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xavissa.Database.Security;

namespace Xavissa.Backend.Security
{
    public class HttpRequestContext : IRequestContext
    {
        private const string StoreHeaderName = "X-Store-Id";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpRequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public bool IsAuthenticated => HttpContext?.User?.Identity?.IsAuthenticated == true;

        public int? UserId =>
            int.TryParse(
                HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? HttpContext?.User?.FindFirstValue(ClaimTypes.Name),
                out var userId
            )
                ? userId
                : null;

        public string PlatformRole =>
            HttpContext?.User?.FindFirstValue(ClaimNames.PlatformRole)
                ?? HttpContext?.User?.FindFirstValue(ClaimTypes.Role)
                ?? AccessRoles.User;

        public bool IsPlatformAdmin => PlatformRole.IsPlatformAdmin();

        public string? ActingRole => HttpContext?.User?.FindFirstValue(ClaimNames.ActingRole);

        public IReadOnlyCollection<int> AllowedTenantIds =>
            HttpContext?.User?.FindAll(ClaimNames.TenantId)
                .Select(c => int.TryParse(c.Value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();

        public IReadOnlyCollection<int> AllowedStoreIds =>
            HttpContext?.User?.FindAll(ClaimNames.StoreId)
                .Select(c => int.TryParse(c.Value, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();

        public int? SelectedTenantId
        {
            get
            {
                var claimValue = HttpContext?.User?.FindFirstValue("selected_tenant_id")
                    ?? HttpContext?.User?.FindFirstValue(ClaimNames.TenantId);
                return int.TryParse(claimValue, out var tenantId) ? tenantId : null;
            }
        }

        public int? SelectedStoreId
        {
            get
            {
                var claimValue = HttpContext?.User?.FindFirstValue("selected_store_id")
                    ?? HttpContext?.User?.FindFirstValue(ClaimNames.StoreId);
                if (int.TryParse(claimValue, out var claimedStoreId))
                    return claimedStoreId;

                var headerValue = HttpContext?.Request?.Headers[StoreHeaderName].FirstOrDefault();
                return int.TryParse(headerValue, out var headerStoreId) ? headerStoreId : null;
            }
        }

        public string? IpAddress => HttpContext?.Connection?.RemoteIpAddress?.ToString();

        private HttpContext? HttpContext => _httpContextAccessor.HttpContext;
    }
}
