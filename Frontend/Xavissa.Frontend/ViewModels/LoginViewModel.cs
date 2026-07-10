using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly ILoginCoordinator _login;
        private readonly INotificationService _notify;
        private readonly IConnectivityService _net;
        private readonly ISyncService _sync;
        private readonly ILocalizationService _localization;
        private readonly IApiTokenProvider _tokens;
        private readonly ISessionLoadState _sessionLoad;
        private readonly ILicenseClient _licenseClient;
        private readonly ILicenseCacheService _licenseCache;
        private readonly IDeviceFingerprintService _fingerprint;
        private readonly IWorkspaceService _workspace;
        private readonly IAuthService _auth;
        private readonly IBackendHealthService _backendHealth;
        private readonly ILicenseStateService _licenseState;
        private readonly IServiceProvider _services;

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                this.RaiseAndSetIfChanged(ref _username, value);
                UsernameError = string.IsNullOrWhiteSpace(value) ? _localization.GetString("Loc.UsernameRequired") : string.Empty;
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set
            {
                this.RaiseAndSetIfChanged(ref _password, value);
                PasswordError = string.IsNullOrWhiteSpace(value) ? _localization.GetString("Loc.PasswordRequired") : string.Empty;
            }
        }

        private bool _isPasswordVisible;
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set
            {
                this.RaiseAndSetIfChanged(ref _isPasswordVisible, value);
                this.RaisePropertyChanged(nameof(IsPasswordHidden));
                this.RaisePropertyChanged(nameof(PasswordToggleText));
            }
        }

        public bool IsPasswordHidden => !IsPasswordVisible;
        public string PasswordToggleText => IsPasswordVisible ? "Hide" : "Show";

        private string _usernameError = string.Empty;
        public string UsernameError
        {
            get => _usernameError;
            private set => this.RaiseAndSetIfChanged(ref _usernameError, value);
        }

        private string _passwordError = string.Empty;
        public string PasswordError
        {
            get => _passwordError;
            private set => this.RaiseAndSetIfChanged(ref _passwordError, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private string _busyMessage = string.Empty;
        public string BusyMessage
        {
            get => _busyMessage;
            private set => this.RaiseAndSetIfChanged(ref _busyMessage, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> TryDemoCommand { get; }
        public ReactiveCommand<Unit, Unit> ActivateLicenseCommand { get; }
        public ReactiveCommand<Unit, Unit> TogglePasswordVisibilityCommand { get; }
        public Interaction<Unit, Unit> NavigateAfterLogin { get; } = new();
        public ObservableCollection<AssignedStore> StoreChoices { get; } = new();

        private int? _selectedLoginStoreId;
        public int? SelectedLoginStoreId
        {
            get => _selectedLoginStoreId;
            set => this.RaiseAndSetIfChanged(ref _selectedLoginStoreId, value);
        }

        private bool _isStoreSelectionRequired;
        public bool IsStoreSelectionRequired
        {
            get => _isStoreSelectionRequired;
            private set => this.RaiseAndSetIfChanged(ref _isStoreSelectionRequired, value);
        }

        private bool _isBackendReady;
        public bool IsBackendReady
        {
            get => _isBackendReady;
            private set
            {
                this.RaiseAndSetIfChanged(ref _isBackendReady, value);
                this.RaisePropertyChanged(nameof(CanLoginOnline));
                this.RaisePropertyChanged(nameof(CanLoginOffline));
            }
        }

        private string _backendStatusMessage = "Backend is starting...";
        public string BackendStatusMessage
        {
            get => _backendStatusMessage;
            private set => this.RaiseAndSetIfChanged(ref _backendStatusMessage, value);
        }

        public bool CanLoginOnline => IsBackendReady && !IsBusy;
        public bool CanLoginOffline => !IsBackendReady && !IsBusy;

        private string _licenseKey = string.Empty;
        public string LicenseKey
        {
            get => _licenseKey;
            set => this.RaiseAndSetIfChanged(ref _licenseKey, value);
        }

        public LoginViewModel(
            ILoginCoordinator login,
            ISyncService sync,
            IConnectivityService net,
            INotificationService notify,
            ILocalizationService localization,
            IApiTokenProvider tokens,
            ISessionLoadState sessionLoad,
            ILicenseClient licenseClient,
            ILicenseCacheService licenseCache,
            IDeviceFingerprintService fingerprint,
            IWorkspaceService workspace,
            IAuthService auth,
            IBackendHealthService backendHealth,
            ILicenseStateService licenseState,
            IServiceProvider services)
        {
            _login = login ?? throw new ArgumentNullException(nameof(login));
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
            _net = net ?? throw new ArgumentNullException(nameof(net));
            _notify = notify ?? throw new ArgumentNullException(nameof(notify));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _sessionLoad = sessionLoad ?? throw new ArgumentNullException(nameof(sessionLoad));
            _licenseClient = licenseClient ?? throw new ArgumentNullException(nameof(licenseClient));
            _licenseCache = licenseCache ?? throw new ArgumentNullException(nameof(licenseCache));
            _fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _backendHealth = backendHealth ?? throw new ArgumentNullException(nameof(backendHealth));
            _licenseState = licenseState ?? throw new ArgumentNullException(nameof(licenseState));
            _services = services ?? throw new ArgumentNullException(nameof(services));

            LoginCommand = ReactiveCommand.CreateFromTask(LoginAsync);
            TryDemoCommand = ReactiveCommand.CreateFromTask(TryDemoAsync);
            ActivateLicenseCommand = ReactiveCommand.CreateFromTask(ActivateLicenseAsync);
            TogglePasswordVisibilityCommand = ReactiveCommand.Create(() =>
            {
                IsPasswordVisible = !IsPasswordVisible;
            });
            LoginCommand.ThrownExceptions.Subscribe(ex =>
            {
                _notify.Show(_localization.GetString("Loc.LoginFailed"));
            });

            ApplyBackendHealth(_backendHealth.Current);
            _backendHealth.Changed += ApplyBackendHealth;
            _backendHealth.StartMonitoring();
        }

        private async Task TryDemoAsync()
        {
            IsBusy = true;
            BusyMessage = _localization.GetString("Loc.StartingDemo");

            try
            {
                _workspace.ResetDemoWorkspace();
                await EnsureWorkspaceSchemaAsync();

                DemoStartResponse? onlineSession = null;
                if (_net.IsOnline())
                    onlineSession = await _licenseClient.StartDemoAsync(await _fingerprint.GetDeviceInfoAsync());

                if (onlineSession?.LicenseSnapshot == null)
                {
                    _notify.Show("Demo mode could not be started. Connect to the licensing server and try again.", NotificationType.Error);
                    return;
                }

                var tenantId = onlineSession.TenantId ?? onlineSession.LicenseSnapshot.TenantId ?? 0;
                var stores = new[]
                {
                    new AssignedStore { Id = 0, TenantId = tenantId, Name = "Demo Store", Role = AppRoles.StoreManager, StoreRoleCode = AppRoles.StoreManager },
                };
                var tenants = new[]
                {
                    new AssignedTenant { Id = tenantId, Name = "Xavissa Demo", Role = AppRoles.TenantAdmin, TenantRoleCode = AppRoles.TenantAdmin },
                };

                _auth.StartSession(new OfflineIdentity
                {
                    OnlineUserId = 0,
                    Username = "demo",
                    PlatformRoleCode = AppRoles.User,
                    PlatformRole = AppRoles.User,
                    ActingRole = AppRoles.TenantAdmin,
                    Role = AppRoles.TenantAdmin,
                    AllowedTenantsJson = JsonSerializer.Serialize(tenants),
                    AllowedStoresJson = JsonSerializer.Serialize(stores),
                    SelectedTenantId = tenantId,
                    SelectedStoreId = 0,
                    LastOnlineLogin = DateTime.UtcNow,
                    IsActive = true,
                });

                await NavigateAfterLogin.Handle(Unit.Default);
            }
            catch (Exception ex)
            {
                _notify.Show("Demo mode could not be opened: " + ex.Message, NotificationType.Error);
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task ActivateLicenseAsync()
        {
            if (string.IsNullOrWhiteSpace(LicenseKey))
            {
                _notify.Show("Enter a license key first.", NotificationType.Error);
                return;
            }

            IsBusy = true;
            BusyMessage = _localization.GetString("Loc.ActivatingLicense");

            try
            {
                var deviceInfo = await _fingerprint.GetDeviceInfoAsync();
                var result = await _licenseClient.ActivateAsync(LicenseKey.Trim(), deviceInfo);
                if (result?.SignedCache == null)
                {
                    _notify.Show(result?.Error ?? "License activation failed.", NotificationType.Error);
                    return;
                }

                await _licenseCache.SaveSignedCacheAsync(result.SignedCache);
                _workspace.UseRealWorkspace();
                await EnsureWorkspaceSchemaAsync();
                LicenseKey = string.Empty;
                _notify.Show("License activated. You can now log in to the real workspace.", NotificationType.Success);
            }
            catch (Exception ex)
            {
                _notify.Show("License activation failed: " + ex.Message, NotificationType.Error);
            }
            finally
            {
                IsBusy = false;
                BusyMessage = string.Empty;
            }
        }

        private async Task LoginAsync()
        {
            if (IsStoreSelectionRequired)
            {
                if (!SelectedLoginStoreId.HasValue)
                {
                    _notify.Show("Select a store before entering the workspace.", NotificationType.Warning);
                    return;
                }

                IsBusy = true;
                BusyMessage = _localization.GetString("Loc.SelectingStore");
                var selected = await _login.CompletePendingStoreSelectionAsync(SelectedLoginStoreId.Value);
                if (!selected)
                {
                    IsBusy = false;
                    BusyMessage = string.Empty;
                    _notify.Show("Store selection failed.", NotificationType.Error);
                    return;
                }

                IsStoreSelectionRequired = false;
                StoreChoices.Clear();
                await NavigateAfterLogin.Handle(Unit.Default);
                IsBusy = false;
                BusyMessage = string.Empty;
                _ = Task.Run(SyncOnlineDataAsync);
                return;
            }

            UsernameError = string.IsNullOrWhiteSpace(Username) ? _localization.GetString("Loc.UsernameRequired") : string.Empty;
            PasswordError = string.IsNullOrWhiteSpace(Password) ? _localization.GetString("Loc.PasswordRequired") : string.Empty;
            if (!string.IsNullOrEmpty(UsernameError) || !string.IsNullOrEmpty(PasswordError))
                return;

            IsBusy = true;
            _workspace.UseRealWorkspace();
            BusyMessage = _net.IsOnline()
                ? _localization.GetString("Loc.CheckingBackend")
                : _localization.GetString("Loc.OpeningLocalWorkspace");

            var loginUsername = Username.Trim();
            var loginPassword = Password;
            var success = await _login.LoginAsync(loginUsername, loginPassword);

            if (!success)
            {
                if (_login.HasPendingStoreSelection)
                {
                    StoreChoices.Clear();
                    foreach (var store in _login.PendingStoreChoices)
                        StoreChoices.Add(store);

                    SelectedLoginStoreId = StoreChoices.Count > 0 ? StoreChoices[0].Id : null;
                    IsStoreSelectionRequired = StoreChoices.Count > 1;
                    IsBusy = false;
                    BusyMessage = string.Empty;
                    BackendStatusMessage = "Select the store to issue a store-scoped session token.";
                    return;
                }

                IsBusy = false;
                BusyMessage = string.Empty;
                PasswordError = _localization.GetString("Loc.InvalidUsernameOrPassword");
                return;
            }

            Username = string.Empty;
            Password = string.Empty;
            IsPasswordVisible = false;
            UsernameError = string.Empty;
            PasswordError = string.Empty;

            await NavigateAfterLogin.Handle(Unit.Default);

            IsBusy = false;
            BusyMessage = string.Empty;

            _ = Task.Run(SyncOnlineDataAsync);
        }

        private void ApplyBackendHealth(BackendHealthSnapshot snapshot)
        {
            IsBackendReady = snapshot.IsReady;
            BackendStatusMessage = snapshot.Message;
            this.RaisePropertyChanged(nameof(CanLoginOnline));
            this.RaisePropertyChanged(nameof(CanLoginOffline));
        }

        private async Task<bool> EnsureLicenseForAuthenticatedTenantAsync()
        {
            await Task.CompletedTask;
            return true;
        }

        private async Task EnsureWorkspaceSchemaAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
            await db.Database.CloseConnectionAsync();
            await db.EnsureLocalSchemaAsync();
        }

        private async Task SyncOnlineDataAsync()
        {
            if (!_net.IsOnline() || string.IsNullOrWhiteSpace(_tokens.Token))
                return;

            try
            {
                _sessionLoad.Show(_localization.GetString("Loc.ConnectingOnlineData"));
                _sessionLoad.Show(_localization.GetString("Loc.SyncingLatestData"));
                await _sync.SyncAllAsync();
                _sessionLoad.NotifyOnlineDataApplied();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Background sync after login failed: " + ex.Message);
            }
            finally
            {
                _sessionLoad.Hide();
            }
        }
    }
}
