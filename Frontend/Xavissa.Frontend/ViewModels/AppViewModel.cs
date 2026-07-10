using System;
using System.Reactive;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Xavissa.Frontend.Data.Entities;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class AppViewModel : ViewModelBase
    {
        private readonly IServiceProvider _sp;
        private readonly INotificationService _notify;
        private readonly IAuthService _auth;
        private readonly ILocalizationService _localization;
        private readonly ILicenseStateService _licenseState;
        private readonly IDemoStateService _demoState;
        private readonly IWorkspaceService _workspace;

        private ViewModelBase _currentView;
        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public AppViewModel(IServiceProvider sp, INotificationService notify, IAuthService auth, ILocalizationService localization, ILicenseStateService licenseState, IDemoStateService demoState, IWorkspaceService workspace)
        {
            _sp = sp;
            _notify = notify;
            _auth = auth;
            _localization = localization;
            _licenseState = licenseState;
            _demoState = demoState;
            _workspace = workspace;

            CurrentView = _sp.GetRequiredService<LoginViewModel>();
        }

        // ----------------------------------------------------
        // LOGIN
        // ----------------------------------------------------

        private void ShowLoginView()
        {
            var loginVM = _sp.GetRequiredService<LoginViewModel>();

            loginVM.NavigateAfterLogin.RegisterHandler(interaction =>
            {
                if (!_auth.IsLoggedIn || _auth.CurrentUser == null)
                {
                    _notify.Show(_localization.GetString("Loc.LoginFailedInvalidSession"), NotificationType.Error);
                    interaction.SetOutput(Unit.Default);
                    return;
                }

                ShowMainView();

                interaction.SetOutput(Unit.Default);
            });

            CurrentView = loginVM;
        }

        private async System.Threading.Tasks.Task InitializeLicenseGateAsync()
        {
            await _demoState.LoadAsync();
            var result = LicenseStateResult.Allow(
                LicenseAccessStatus.ValidOffline,
                "POS workspace available.",
                LocalLicenseSnapshot.CreatePosAccessSnapshot(),
                offline: true);
            if (result.CanOpenWorkspace && result.Snapshot?.IsDemo == true)
                await ShowDemoWorkspaceAsync(result.Snapshot);
            else if (result.CanOpenWorkspace)
                ShowLoginView();
            else if (result.ShouldShowDemoExpired)
                ShowDemoExpiredView();
            else if (result.ShouldShowActivation)
                ShowActivationView();
            else
                ShowBlockedView(result);
        }

        private void ShowActivationView()
        {
            ShowLoginView();
        }

        private void ShowBlockedView(LicenseStateResult result)
        {
            if (result.ShouldShowDemoExpired)
            {
                ShowDemoExpiredView();
                return;
            }

            var blockedVm = _sp.GetRequiredService<LicenseBlockedViewModel>();
            blockedVm.Apply(result, _auth.IsTenantAdmin || !_auth.IsLoggedIn);
            blockedVm.Unblocked.RegisterHandler(interaction =>
            {
                ShowLoginView();
                interaction.SetOutput(Unit.Default);
            });
            blockedVm.ShowActivation.RegisterHandler(interaction =>
            {
                ShowActivationView();
                interaction.SetOutput(Unit.Default);
            });
            CurrentView = blockedVm;
        }

        private void ShowDemoExpiredView()
        {
            var expiredVm = _sp.GetRequiredService<DemoExpiredViewModel>();
            expiredVm.Restarted.RegisterHandler(interaction =>
            {
                _ = ShowDemoWorkspaceAsync(_licenseState.Current.Snapshot);
                interaction.SetOutput(Unit.Default);
            });
            expiredVm.ShowActivation.RegisterHandler(interaction =>
            {
                ShowActivationView();
                interaction.SetOutput(Unit.Default);
            });
            CurrentView = expiredVm;
        }

        // ----------------------------------------------------
        // MAIN
        // ----------------------------------------------------

        private void ShowMainView()
        {
            var mainVM = _sp.GetRequiredService<MainViewModel>();

            // ✅ Register interaction handler
            mainVM.NavigateToLogin.RegisterHandler(interaction =>
            {
                ShowLoginView();
                interaction.SetOutput(Unit.Default);
            });
            mainVM.ShowActivation.RegisterHandler(interaction =>
            {
                ShowLoginView();
                interaction.SetOutput(Unit.Default);
            });
            mainVM.DemoExpired.RegisterHandler(interaction =>
            {
                ShowDemoExpiredView();
                interaction.SetOutput(Unit.Default);
            });

            _notify.Show(_localization.GetString("Loc.MainDashboardLoaded"), NotificationType.Success);

            CurrentView = mainVM;
        }

        private async System.Threading.Tasks.Task ShowDemoWorkspaceAsync(LocalLicenseSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                ShowActivationView();
                return;
            }

            if (_demoState.Current.Status == DemoModeStatus.NotStarted && snapshot.IsDemo)
            {
                await _demoState.StartAsync(new StartDemoSessionResponse
                {
                    Success = true,
                    DemoSessionId = snapshot.ActivationId,
                    TenantId = snapshot.TenantId,
                    TenantCode = snapshot.TenantCode,
                    TenantName = snapshot.TenantName,
                    LicenseId = snapshot.LicenseId,
                    StartedAt = snapshot.ActivatedAt ?? snapshot.IssuedAt ?? DateTime.UtcNow,
                    ExpiresAt = snapshot.ExpiresAt ?? snapshot.SnapshotExpiresAt,
                    ResetOnClose = true,
                    DemoLicenseSnapshot = snapshot,
                    DemoModeEnabled = true,
                }, new DeviceIdentityDto { DeviceFingerprint = snapshot.DeviceFingerprint });
            }

            var state = await _demoState.CheckExpirationAsync();
            if (state.Status == DemoModeStatus.Expired)
            {
                ShowDemoExpiredView();
                return;
            }

            _workspace.UseDemoWorkspace();
            var tenantId = snapshot.TenantId ?? state.TenantId ?? 1000001;
            var stores = new[]
            {
                new AssignedStore { Id = 101, TenantId = tenantId, Name = "Loja Central", Role = AppRoles.StoreManager, StoreRoleCode = AppRoles.StoreManager },
                new AssignedStore { Id = 102, TenantId = tenantId, Name = "Loja Bairro", Role = AppRoles.StoreManager, StoreRoleCode = AppRoles.StoreManager },
            };
            var tenants = new[]
            {
                new AssignedTenant { Id = tenantId, Name = snapshot.TenantName, Role = AppRoles.TenantAdmin, TenantRoleCode = AppRoles.TenantAdmin },
            };

            _auth.StartSession(new OfflineIdentity
            {
                OnlineUserId = 1000001,
                Username = "demo-admin",
                PlatformRoleCode = AppRoles.User,
                PlatformRole = AppRoles.User,
                ActingRole = AppRoles.StoreManager,
                Role = AppRoles.StoreManager,
                AllowedTenantsJson = JsonSerializer.Serialize(tenants),
                AllowedStoresJson = JsonSerializer.Serialize(stores),
                SelectedTenantId = tenantId,
                SelectedStoreId = 101,
                LastOnlineLogin = DateTime.UtcNow,
                IsActive = true,
            });

            await _demoState.TrackEventAsync("DemoWorkspaceOpened", description: "Demo workspace opened.");
            ShowMainView();
        }
    }
}
