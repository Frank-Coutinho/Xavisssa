namespace Xavissa.Backend.Security;

public class PermissionService : IPermissionService
{
    private readonly TenantAccessService _access;

    public PermissionService(TenantAccessService access)
    {
        _access = access;
    }

    public bool CanManagePlatform() => _access.IsPlatformAdmin;

    public bool CanManageTenant(int tenantId) =>
        _access.IsPlatformAdmin
        || (_access.CanAccessTenant(tenantId) && _access.ActingRole.IsTenantAdmin());

    public bool CanCreateStore(int tenantId) => CanManageTenant(tenantId);

    public bool CanManageTenantUsers(int tenantId) => CanManageTenant(tenantId);

    public bool CanManageStore(int tenantId, int storeId) =>
        CanManageTenant(tenantId)
        || (_access.CanAccessStore(storeId) && _access.ActingRole.IsStoreManager());

    public bool CanCreateProduct(int tenantId) => CanManageTenant(tenantId);

    public bool CanAssignProductToStore(int tenantId, int storeId) => CanManageStore(tenantId, storeId);

    public bool CanCreateVariant(int tenantId, int storeId) => CanManageStore(tenantId, storeId);

    public bool CanManageStock(int tenantId, int storeId) => CanManageStore(tenantId, storeId);

    public bool CanCreateSale(int tenantId, int storeId) =>
        CanManageStore(tenantId, storeId)
        || (_access.CanAccessStore(storeId) && _access.ActingRole.IsClerkLike());

    public bool CanRefundSale(int tenantId, int storeId) => CanManageStore(tenantId, storeId);

    public bool CanViewReports(int tenantId, int storeId) =>
        CanManageTenant(tenantId)
        || (_access.CanAccessStore(storeId) && _access.ActingRole.IsStoreManager());

}
