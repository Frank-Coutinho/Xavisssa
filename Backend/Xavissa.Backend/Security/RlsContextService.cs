using Microsoft.EntityFrameworkCore;
using Xavissa.Database;
using Xavissa.Database.Security;

namespace Xavissa.Backend.Security;

public class RlsContextService : IRlsContextService
{
    private readonly XavissaDbContext _db;
    private readonly IRequestContext _requestContext;

    public RlsContextService(XavissaDbContext db, IRequestContext requestContext)
    {
        _db = db;
        _requestContext = requestContext;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!_requestContext.IsAuthenticated || !_requestContext.UserId.HasValue)
            return;

        var canAccessAllStores =
            _requestContext.IsPlatformAdmin
            || _requestContext.PlatformRole.IsSupport()
            || _requestContext.ActingRole.IsTenantAdmin();

        if (!canAccessAllStores && !_requestContext.SelectedStoreId.HasValue)
            throw new InvalidOperationException("A selected store is required before store-scoped remote access.");

        await _db.Database.OpenConnectionAsync(cancellationToken);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"SELECT
                set_config('app.current_user_id', {_requestContext.UserId.Value.ToString()}, false),
                set_config('app.current_tenant_id', {ToConfigValue(_requestContext.SelectedTenantId)}, false),
                set_config('app.current_store_id', {ToConfigValue(_requestContext.SelectedStoreId)}, false),
                set_config('app.platform_role', {_requestContext.PlatformRole ?? string.Empty}, false),
                set_config('app.acting_role', {_requestContext.ActingRole ?? string.Empty}, false),
                set_config('app.allowed_tenant_ids', {string.Join(",", _requestContext.AllowedTenantIds)}, false),
                set_config('app.allowed_store_ids', {string.Join(",", _requestContext.AllowedStoreIds)}, false),
                set_config('app.can_access_all_stores', {canAccessAllStores.ToString().ToLowerInvariant()}, false);",
            cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            return;

        await _db.Database.ExecuteSqlRawAsync(
            @"RESET app.current_user_id;
              RESET app.current_tenant_id;
              RESET app.current_store_id;
              RESET app.platform_role;
              RESET app.acting_role;
              RESET app.allowed_tenant_ids;
              RESET app.allowed_store_ids;
              RESET app.can_access_all_stores;",
            cancellationToken);
        await _db.Database.CloseConnectionAsync();
    }

    private static string ToConfigValue(int? value) => value?.ToString() ?? string.Empty;
}
