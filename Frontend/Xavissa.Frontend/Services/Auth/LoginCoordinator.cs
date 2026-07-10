using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models.Auth;

namespace Xavissa.Frontend.Services.Auth
{
    public class LoginCoordinator : ILoginCoordinator
    {
        private readonly IServiceProvider _provider;
        private readonly ILocalIdentityService _local;
        private readonly IAuthService _session;
        private readonly IBackendHealthService _backendHealth;
        private readonly IApiTokenProvider _tokens;
        private readonly IOnlineSessionCredentialCache _credentials;
        private string _pendingUsername = string.Empty;
        private string _pendingPassword = string.Empty;
        private LoginResponse? _pendingLogin;

        public LoginCoordinator(
            IServiceProvider provider,
            ILocalIdentityService local,
            IAuthService session,
            IConnectivityService net,
            IApiTokenProvider tokens,
            IBackendHealthService backendHealth,
            IOnlineSessionCredentialCache credentials)
        {
            _provider = provider;
            _local = local;
            _session = session;
            _tokens = tokens;
            _backendHealth = backendHealth;
            _credentials = credentials;
        }

        public bool HasPendingStoreSelection => _pendingLogin?.AllowedStores.Count > 1;
        public IReadOnlyList<AssignedStore> PendingStoreChoices =>
            _pendingLogin == null ? Array.Empty<AssignedStore>() : _pendingLogin.AllowedStores;

        public async Task<bool> LoginAsync(string username, string password)
        {
            using (var scope = _provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
                await db.EnsureLocalSchemaAsync();
            }

            _tokens.Clear();
            ClearPendingStoreSelection();

            if (await IsBackendReadyAsync())
            {
                var upgraded = await TryUpgradeSessionOnlineAsync(username, password, maxAttempts: 1);
                if (upgraded)
                {
                    Console.WriteLine("Logged in (ONLINE)");
                    return true;
                }

                return false;
            }

            var offline = await _local.ValidateOfflineLoginAsync(username, password);
            if (offline == null)
                return false;

            _session.StartSession(offline);
            _backendHealth.MarkOfflineCachedMode("Backend is unavailable. Continuing with a cached offline identity.");
            Console.WriteLine("Logged in (OFFLINE CACHE)");
            return true;
        }

        public async Task<bool> CompletePendingStoreSelectionAsync(int storeId)
        {
            if (_pendingLogin == null || string.IsNullOrWhiteSpace(_pendingUsername))
                return false;

            var online = _provider.GetRequiredService<IAuthRepositoryOnline>();
            var selected = await online.SelectStoreAsync(_pendingUsername, _pendingPassword, storeId);
            if (selected == null || string.IsNullOrWhiteSpace(selected.Token))
                return false;

            _tokens.SetToken(selected.Token);
            _credentials.Set(_pendingUsername, _pendingPassword);
            await _local.SaveFromOnlineLoginAsync(selected, _pendingPassword);

            var offlineUser = await _local.ValidateOfflineLoginAsync(_pendingUsername, _pendingPassword);
            if (offlineUser == null)
                return false;

            _session.StartSession(offlineUser);
            ClearPendingStoreSelection();
            return true;
        }

        public async Task<bool> TryUpgradeSessionOnlineAsync(string username, string password, int maxAttempts = 5)
        {
            for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
            {
                try
                {
                    if (!await IsBackendReadyAsync())
                    {
                        if (attempt < maxAttempts)
                            await Task.Delay(TimeSpan.FromSeconds(1.5));
                        continue;
                    }

                    var login = await ExecuteOnlineLoginAsync(username, password);
                    if (login == null)
                    {
                        if (attempt < maxAttempts && !HasPendingStoreSelection)
                            await Task.Delay(TimeSpan.FromSeconds(1.5));
                        continue;
                    }

                    await _local.SaveFromOnlineLoginAsync(login, password);

                    var offlineUser = await _local.ValidateOfflineLoginAsync(username, password);
                    if (offlineUser == null)
                        return false;

                    _session.StartSession(offlineUser);
                    if (!string.IsNullOrWhiteSpace(login.Token))
                        _credentials.Set(username, password);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Auth] Online upgrade attempt {attempt} failed: {ex.Message}");
                    if (attempt < maxAttempts)
                        await Task.Delay(TimeSpan.FromSeconds(1.5));
                }
            }

            return false;
        }

        private async Task<LoginResponse?> ExecuteOnlineLoginAsync(string username, string password)
        {
            var online = _provider.GetRequiredService<IAuthRepositoryOnline>();
            var login = await online.LoginAsync(username, password);
            if (login == null)
                return null;

            if (string.IsNullOrWhiteSpace(login.Token))
            {
                var selectedStoreId = login.SelectedStoreId
                    ?? (login.AllowedStores.Count == 1 ? login.AllowedStores[0].Id : null);

                if (selectedStoreId.HasValue)
                {
                    var storeScopedLogin = await online.SelectStoreAsync(username, password, selectedStoreId.Value);
                    if (storeScopedLogin != null)
                        login = storeScopedLogin;
                }
                else if (login.AllowedStores.Count > 1)
                {
                    _pendingUsername = username;
                    _pendingPassword = password;
                    _pendingLogin = login;
                    return null;
                }
            }

            if (!string.IsNullOrWhiteSpace(login.Token))
            {
                _tokens.SetToken(login.Token);
                Console.WriteLine($"[Auth] Token cached for '{username}'.");
            }
            else
            {
                Console.WriteLine(
                    $"[Auth] Login for '{username}' returned no token. " +
                    $"PlatformRoleCode={login.PlatformRoleCode ?? login.PlatformRole}, ActingRole={login.ActingRole}, " +
                    $"SelectedTenantId={login.SelectedTenantId}, SelectedStoreId={login.SelectedStoreId}.");
            }

            return login;
        }

        private async Task<bool> IsBackendReadyAsync()
        {
            var snapshot = await _backendHealth.CheckAsync();
            return snapshot.IsReady;
        }

        private void ClearPendingStoreSelection()
        {
            _pendingUsername = string.Empty;
            _pendingPassword = string.Empty;
            _pendingLogin = null;
        }
    }
}
