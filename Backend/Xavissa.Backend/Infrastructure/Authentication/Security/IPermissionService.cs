namespace Xavissa.Backend.Security;

public interface IPermissionService
{
    bool CanManagePlatform();
    bool CanManageTenant(int tenantId);
    bool CanCreateStore(int tenantId);
    bool CanManageTenantUsers(int tenantId);
    bool CanManageStore(int tenantId, int storeId);
    bool CanCreateProduct(int tenantId);
    bool CanAssignProductToStore(int tenantId, int storeId);
    bool CanCreateVariant(int tenantId, int storeId);
    bool CanManageStock(int tenantId, int storeId);
    bool CanCreateSale(int tenantId, int storeId);
    bool CanRefundSale(int tenantId, int storeId);
    bool CanViewReports(int tenantId, int storeId);
}
