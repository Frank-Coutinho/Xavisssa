using Microsoft.AspNetCore.Mvc;
using Xavissa.Database.Security;

namespace Xavissa.Backend.Security
{
    public class TenantAccessService
    {
        private readonly IRequestContext _requestContext;

        public TenantAccessService(IRequestContext requestContext)
        {
            _requestContext = requestContext;
        }

        public int? CurrentUserId => _requestContext.UserId;
        public string PlatformRole => _requestContext.PlatformRole;
        public string? ActingRole => _requestContext.ActingRole;
        public bool IsPlatformAdmin => _requestContext.IsPlatformAdmin;
        public bool IsSupport => _requestContext.PlatformRole.IsSupport();
        public IReadOnlyCollection<int> AllowedTenantIds => _requestContext.AllowedTenantIds;
        public IReadOnlyCollection<int> AllowedStoreIds => _requestContext.AllowedStoreIds;
        public int? SelectedTenantId => _requestContext.SelectedTenantId;
        public int? SelectedStoreId => _requestContext.SelectedStoreId;

        public bool CanAccessTenant(int tenantId) => IsPlatformAdmin || AllowedTenantIds.Contains(tenantId);

        public bool CanAccessStore(int storeId) => IsPlatformAdmin || AllowedStoreIds.Contains(storeId);

        public bool CanManageTenant(int tenantId) =>
            IsPlatformAdmin
            || (CanAccessTenant(tenantId) && (ActingRole.IsTenantAdmin() || IsSupport));

        public bool CanManageStore(int storeId) =>
            IsPlatformAdmin
            || (CanAccessStore(storeId) && (ActingRole.IsTenantAdmin() || ActingRole.IsStoreManager() || IsSupport));

        public ActionResult? EnsurePlatformAdmin()
        {
            return IsPlatformAdmin ? null : new ForbidResult();
        }

        public ActionResult? EnsureTenantAccess(int tenantId)
        {
            return CanAccessTenant(tenantId) ? null : new ForbidResult();
        }

        public ActionResult? EnsureTenantManagement(int tenantId)
        {
            return CanManageTenant(tenantId) ? null : new ForbidResult();
        }

        public ActionResult? EnsureStoreManagement(int storeId)
        {
            return CanManageStore(storeId) ? null : new ForbidResult();
        }

        public ActionResult<int>? RequireSelectedStore()
        {
            if (!SelectedStoreId.HasValue)
                return new BadRequestObjectResult("A selected store is required.");

            if (!CanAccessStore(SelectedStoreId.Value))
                return new ForbidResult();

            return null;
        }
    }
}
