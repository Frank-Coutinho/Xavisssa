using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

public class AuthService : IAuthService
{
    private readonly IApiTokenProvider _tokens;

    public AuthService(IApiTokenProvider tokens)
    {
        _tokens = tokens;
    }

    public OfflineIdentity? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;
    public int? CurrentUserId => CurrentUser?.OnlineUserId;
    public string Username => CurrentUser?.Username ?? string.Empty;
    public string PlatformRole => string.IsNullOrWhiteSpace(CurrentUser?.PlatformRoleCode)
        ? CurrentUser?.PlatformRole ?? string.Empty
        : CurrentUser.PlatformRoleCode;
    public string ActingRole => CurrentUser?.ActingRole ?? string.Empty;
    public bool IsAuthenticated => IsLoggedIn;
    public bool IsOnlineSession => IsLoggedIn && !string.IsNullOrWhiteSpace(CurrentUser?.ApiToken) && !string.IsNullOrWhiteSpace(_tokens.Token);
    public bool IsOfflineSession => IsLoggedIn && !IsOnlineSession;
    public bool HasSelectedStore => SelectedStoreId.HasValue && SelectedStoreId.Value > 0;
    public bool IsTenantAdmin => AppRoles.IsTenantAdmin(ActingRole);
    public bool IsStoreManager => AppRoles.IsStoreManager(ActingRole);
    public bool IsClerk => AppRoles.IsClerkLike(ActingRole);
    public bool IsClerkOrCashier => AppRoles.IsClerkLike(ActingRole);
    public bool IsSystemAdmin => AppRoles.IsSystemAdmin(PlatformRole) || AppRoles.IsSystemAdmin(ActingRole);
    public bool IsSupport => AppRoles.IsSupport(PlatformRole) || AppRoles.IsSupport(ActingRole);
    public bool CanManageStores => IsTenantAdmin;
    public bool CanManageEmployees => CanManageUsers;
    public bool CanManageUsers => IsTenantAdmin || (IsStoreManager && HasSelectedStore);
    public bool CanManageCatalog => IsTenantAdmin || IsStoreManager;
    public bool CanManageVariants => (IsTenantAdmin && HasSelectedStore) || (IsStoreManager && HasSelectedStore);
    public bool CanManageStock => (IsTenantAdmin && HasSelectedStore) || (IsStoreManager && HasSelectedStore);
    public bool CanEditTenantPrinting => IsTenantAdmin;
    public bool CanEditStorePrinting => (IsStoreManager || IsTenantAdmin) && HasSelectedStore;
    public bool CanViewTenantAnalytics => IsTenantAdmin;
    public bool CanViewStoreAnalytics => HasSelectedStore && (IsTenantAdmin || IsStoreManager);
    public bool CanPerformSales => CanUsePOS;
    public bool CanUsePOS => HasSelectedStore && (IsStoreManager || IsClerkOrCashier);
    public bool CanViewHistory => IsTenantAdmin || IsStoreManager || IsClerkOrCashier;
    public bool CanRefundSales => HasSelectedStore && (IsTenantAdmin || IsStoreManager);
    public bool CanVoidSales => HasSelectedStore && (IsTenantAdmin || IsStoreManager);
    public bool CanUseCashRegister => HasSelectedStore && (IsStoreManager || IsClerkOrCashier);
    public bool CanManageCashRegisterSettings => HasSelectedStore && IsTenantAdmin;
    public bool CanViewAnalytics => CanViewTenantAnalytics || CanViewStoreAnalytics;
    public bool CanViewAuditLogs => IsTenantAdmin || (IsStoreManager && HasSelectedStore) || IsSupport || IsSystemAdmin;
    public bool CanViewSyncConflicts => IsTenantAdmin || IsStoreManager;
    public bool CanManageLicensing => IsTenantAdmin || IsSystemAdmin || IsSupport;
    public bool CanUsePlatformAdmin => IsSystemAdmin || IsSupport;
    public bool CanPrintBarcodeLabels => HasSelectedStore && (IsTenantAdmin || IsStoreManager);
    public int? SelectedTenantId => CurrentUser?.SelectedTenantId;
    public int? SelectedStoreId => CurrentUser?.SelectedStoreId;

    public string SelectedStoreRole
    {
        get
        {
            var storeId = SelectedStoreId;
            if (!storeId.HasValue)
                return string.Empty;
            return AllowedStores.FirstOrDefault(s => s.Id == storeId.Value)?.EffectiveRole ?? string.Empty;
        }
    }

    public IReadOnlyList<AssignedTenant> AllowedTenants =>
        CurrentUser == null
            ? Array.Empty<AssignedTenant>()
            : JsonSerializer.Deserialize<List<AssignedTenant>>(CurrentUser.AllowedTenantsJson) ?? new List<AssignedTenant>();

    public IReadOnlyList<AssignedStore> AllowedStores =>
        CurrentUser == null
            ? Array.Empty<AssignedStore>()
            : JsonSerializer.Deserialize<List<AssignedStore>>(CurrentUser.AllowedStoresJson) ?? new List<AssignedStore>();

    public event Action? UserChanged;
    public event Action? SessionExpired;

    public void StartSession(OfflineIdentity user)
    {
        var stores = JsonSerializer.Deserialize<List<AssignedStore>>(user.AllowedStoresJson) ?? new List<AssignedStore>();
        var tenants = JsonSerializer.Deserialize<List<AssignedTenant>>(user.AllowedTenantsJson) ?? new List<AssignedTenant>();

        var tenantRole = tenants.FirstOrDefault()?.EffectiveRole;
        var shouldAutoSelectStore = user.SelectedStoreId == null
            && stores.Count == 1
            && !AppRoles.IsTenantAdmin(user.ActingRole)
            && !AppRoles.IsTenantAdmin(tenantRole);

        if (shouldAutoSelectStore)
            user.SelectedStoreId = stores.Select(s => (int?)s.Id).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(user.PlatformRoleCode))
            user.PlatformRoleCode = AppRoles.NormalizeRoleCode(user.PlatformRole);

        if (user.SelectedTenantId == null)
            user.SelectedTenantId = stores.FirstOrDefault(s => s.Id == user.SelectedStoreId)?.TenantId
                ?? tenants.Select(t => (int?)t.Id).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(user.ActingRole))
            user.ActingRole = stores.FirstOrDefault(s => s.Id == user.SelectedStoreId)?.EffectiveRole
                ?? tenants.FirstOrDefault()?.EffectiveRole
                ?? user.PlatformRoleCode
                ?? user.PlatformRole;

        if (HasUsableToken(user.ApiToken))
            _tokens.SetToken(user.ApiToken);
        else
        {
            user.ApiToken = string.Empty;
            _tokens.Clear();
        }

        CurrentUser = user;
        UserChanged?.Invoke();
    }

    public void ApplyOnlineSession(LoginResponse login)
    {
        if (CurrentUser == null)
            return;

        var platformRoleCode = string.IsNullOrWhiteSpace(login.PlatformRoleCode)
            ? AppRoles.NormalizeRoleCode(login.PlatformRole)
            : login.PlatformRoleCode;

        CurrentUser.OnlineUserId = login.UserId;
        CurrentUser.Username = login.Username.Trim();
        CurrentUser.ApiToken = login.Token?.Trim() ?? string.Empty;
        CurrentUser.PlatformRoleId = login.PlatformRoleId;
        CurrentUser.PlatformRoleCode = platformRoleCode;
        CurrentUser.PlatformRole = login.PlatformRole;
        CurrentUser.ActingRole = login.ActingRole ?? string.Empty;
        CurrentUser.Role = string.IsNullOrWhiteSpace(CurrentUser.ActingRole)
            ? platformRoleCode
            : CurrentUser.ActingRole;
        CurrentUser.AllowedTenantsJson = JsonSerializer.Serialize(login.AllowedTenants);
        CurrentUser.AllowedStoresJson = JsonSerializer.Serialize(login.AllowedStores);
        CurrentUser.SelectedTenantId = login.SelectedTenantId
            ?? login.AllowedStores.FirstOrDefault(s => s.Id == login.SelectedStoreId)?.TenantId
            ?? login.AllowedTenants.Select(t => (int?)t.Id).FirstOrDefault();
        CurrentUser.SelectedStoreId = login.SelectedStoreId;
        CurrentUser.LastOnlineLogin = DateTime.UtcNow;

        if (HasUsableToken(CurrentUser.ApiToken))
            _tokens.SetToken(CurrentUser.ApiToken);

        UserChanged?.Invoke();
    }

    public bool SetSelectedStore(int storeId)
    {
        if (CurrentUser == null)
            return false;

        var store = AllowedStores.FirstOrDefault(s => s.Id == storeId);
        if (store == null)
            return false;

        CurrentUser.SelectedStoreId = storeId;
        CurrentUser.SelectedTenantId = store.TenantId;
        CurrentUser.ActingRole = store.EffectiveRole;
        UserChanged?.Invoke();
        return true;
    }

    public void Logout()
    {
        CurrentUser = null;
        _tokens.Clear();
        UserChanged?.Invoke();
    }

    public void NotifySessionExpired()
    {
        if (CurrentUser != null)
            CurrentUser.ApiToken = string.Empty;

        _tokens.Clear();
        SessionExpired?.Invoke();
        UserChanged?.Invoke();
    }

    private static bool HasUsableToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var validToUtc = jwt.ValidTo;
            return validToUtc == DateTime.MinValue || validToUtc > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }
}
