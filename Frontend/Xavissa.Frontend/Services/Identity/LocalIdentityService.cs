using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Helpers;
using Xavissa.Frontend.Models.Auth;

public class LocalIdentityService : ILocalIdentityService
{
    private readonly LocalDbContext _db;

    public LocalIdentityService(LocalDbContext db)
    {
        _db = db;
    }

    public async Task SaveFromOnlineLoginAsync(LoginResponse login, string password)
    {
        _db.ChangeTracker.Clear();

        var normalizedUsername = NormalizeUsername(login.Username);
        var user = await FindByNormalizedUsernameAsync(normalizedUsername, track: false);
        var hash = PasswordHelper.HashPassword(password);
        var platformRoleCode = string.IsNullOrWhiteSpace(login.PlatformRoleCode)
            ? AppRoles.NormalizeRoleCode(login.PlatformRole)
            : login.PlatformRoleCode;
        var effectiveRole = string.IsNullOrWhiteSpace(login.ActingRole) ? platformRoleCode : login.ActingRole;

        if (user == null)
        {
            user = new OfflineIdentity();
            _db.Add(user);
        }
        else
        {
            _db.Update(user);
        }

        ApplyLoginData(user, login, hash, effectiveRole);

        try
        {
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }
        catch (DbUpdateException ex) when (IsOfflineIdentityUsernameConflict(ex))
        {
            // Recover when a differently formatted local username already exists.
            _db.ChangeTracker.Clear();

            var addedEntry = _db.ChangeTracker.Entries<OfflineIdentity>()
                .FirstOrDefault(e => e.State == EntityState.Added);

            if (addedEntry != null)
            {
                addedEntry.State = EntityState.Detached;
            }

            user = await FindByNormalizedUsernameAsync(normalizedUsername, track: false);
            if (user == null)
            {
                throw;
            }

            _db.Update(user);
            ApplyLoginData(user, login, hash, effectiveRole);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }
    }

    public async Task<OfflineIdentity?> ValidateOfflineLoginAsync(string username, string password)
    {
        var normalizedUsername = NormalizeUsername(username);
        var user = await _db.Set<OfflineIdentity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Username.Trim().ToLower() == normalizedUsername);

        if (user == null || !user.IsActive)
            return null;
        return PasswordHelper.VerifyPassword(password, user.PasswordHash) ? user : null;
    }

    public async Task ClearCachedTokenAsync(string username)
    {
        _db.ChangeTracker.Clear();

        var normalizedUsername = NormalizeUsername(username);
        var user = await FindByNormalizedUsernameAsync(normalizedUsername);
        if (user == null)
            return;

        user.ApiToken = string.Empty;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<OfflineIdentity?> FindByNormalizedUsernameAsync(string normalizedUsername, bool track = true)
    {
        var query = _db.Set<OfflineIdentity>().AsQueryable();
        if (!track)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(x => x.Username.Trim().ToLower() == normalizedUsername);
    }

    private static void ApplyLoginData(OfflineIdentity user, LoginResponse login, string hash, string effectiveRole)
    {
        user.OnlineUserId = login.UserId;
        user.Username = login.Username.Trim();
        user.PasswordHash = hash;
        user.ApiToken = login.Token?.Trim() ?? string.Empty;
        user.Role = effectiveRole;
        user.PlatformRoleId = login.PlatformRoleId;
        user.PlatformRoleCode = string.IsNullOrWhiteSpace(login.PlatformRoleCode)
            ? AppRoles.NormalizeRoleCode(login.PlatformRole)
            : login.PlatformRoleCode;
        user.PlatformRole = login.PlatformRole;
        user.ActingRole = login.ActingRole ?? string.Empty;
        user.AllowedTenantsJson = System.Text.Json.JsonSerializer.Serialize(login.AllowedTenants);
        user.AllowedStoresJson = System.Text.Json.JsonSerializer.Serialize(login.AllowedStores);
        var tenantRole = login.AllowedTenants.FirstOrDefault()?.EffectiveRole;
        var shouldAutoSelectStore = !login.SelectedStoreId.HasValue
            && login.AllowedStores.Count == 1
            && !AppRoles.IsTenantAdmin(login.ActingRole)
            && !AppRoles.IsTenantAdmin(tenantRole);

        user.SelectedTenantId = login.SelectedTenantId
            ?? login.AllowedStores.FirstOrDefault(s => s.Id == login.SelectedStoreId)?.TenantId
            ?? login.AllowedTenants.Select(t => (int?)t.Id).FirstOrDefault();
        user.SelectedStoreId = login.SelectedStoreId
            ?? (shouldAutoSelectStore ? login.AllowedStores.Select(s => (int?)s.Id).FirstOrDefault() : null);
        user.LastOnlineLogin = DateTime.UtcNow;
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLower();

    private static bool IsOfflineIdentityUsernameConflict(DbUpdateException ex) =>
        ex.InnerException is SqliteException sqliteEx &&
        sqliteEx.SqliteErrorCode == 19 &&
        sqliteEx.Message.Contains("OfflineIdentities.Username", StringComparison.Ordinal);
}
