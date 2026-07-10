using System.Threading.Tasks;

namespace Xavissa.Frontend.Services;

public interface ITenantQuotaService
{
    Task<(bool Allowed, string? Message)> CanCreateStoreAsync(int tenantId);
    Task<(bool Allowed, string? Message)> CanCreateTenantUserAsync(int tenantId);
}
