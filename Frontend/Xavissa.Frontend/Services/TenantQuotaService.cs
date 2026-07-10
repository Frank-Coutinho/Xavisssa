using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;

namespace Xavissa.Frontend.Services;

public class TenantQuotaService : ITenantQuotaService
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;
    private readonly ILicenseStateService _licenseState;

    public TenantQuotaService(IDbContextFactory<LocalDbContext> dbFactory, ILicenseStateService licenseState)
    {
        _dbFactory = dbFactory;
        _licenseState = licenseState;
    }

    public async Task<(bool Allowed, string? Message)> CanCreateStoreAsync(int tenantId)
    {
        await Task.CompletedTask;
        return (true, null);
    }

    public async Task<(bool Allowed, string? Message)> CanCreateTenantUserAsync(int tenantId)
    {
        await Task.CompletedTask;
        return (true, null);
    }

    private async Task<int> CountActiveStoresAsync(int tenantId)
    {
        await using var db = _dbFactory.CreateDbContext();
        return await db.Stores.AsNoTracking().CountAsync(store => store.TenantId == tenantId && store.IsActive);
    }
}
