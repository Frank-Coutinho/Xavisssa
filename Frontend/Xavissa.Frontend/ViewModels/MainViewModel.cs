using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Options;
using ReactiveUI;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Models.Auth;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private enum ManagementDestination
        {
            Team,
            Stores,
            Categories,
            Catalog,
            Variants,
        }

        private readonly IAuthService _auth;
        private readonly IApiTokenProvider _tokens;
        private readonly IBackgroundSyncService _backgroundSync;
        private readonly IConnectivityService _net;
        private readonly INotificationService _notify;
        private readonly ILocalIdentityService _localIdentity;
        private readonly IAuthRepositoryOnline _authOnline;
        private readonly IOnlineSessionCredentialCache _credentials;
        private readonly ILocalizationService _localization;
        private readonly ISessionLoadState _sessionLoad;
        private readonly ILicenseFeatureGate _featureGate;
        private readonly IDemoStateService _demoState;
        private readonly LicensingOptions _licenseOptions;
        private bool _wasOnline;
        private bool _syncInProgress;
        private DateTime? _lastSyncAt;
        private string _syncStatusText = string.Empty;
        private bool _isSwitchingStore;
        private bool _isSidebarExpanded = true;
        private bool _isManagementMenuExpanded;

        public HomeViewModel HomeVm { get; }
        public HistoryViewModel HistoryVm { get; }
        public AnalyticsViewModel AnalyticsVm { get; }
        public ManagementViewModel ManagementVm { get; }
        public ConfigViewModel ConfigVm { get; }
        public NoWorkspaceViewModel NoWorkspaceVm { get; }
        public UnsupportedRoleViewModel UnsupportedRoleVm { get; }

        private ViewModelBase _currentView;
        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentView, value);
                UpdateNavigationState();
                this.RaisePropertyChanged(nameof(CurrentSectionTitle));
                this.RaisePropertyChanged(nameof(CurrentSectionSubtitle));
            }
        }

        private string _currentUser = string.Empty;
        public string CurrentUser { get => _currentUser; private set => this.RaiseAndSetIfChanged(ref _currentUser, value); }

        private string _currentDate = string.Empty;
        public string CurrentDate { get => _currentDate; private set => this.RaiseAndSetIfChanged(ref _currentDate, value); }

        private string _currentTime = string.Empty;
        public string CurrentTime { get => _currentTime; private set => this.RaiseAndSetIfChanged(ref _currentTime, value); }

        public string CurrentRoleLabel => _auth.IsSystemAdmin
            ? "System admin"
            : _auth.IsSupport
                ? "Support"
                : _auth.IsTenantAdmin ? _localization.GetString("Loc.RoleTenantAdmin") : _auth.IsStoreManager ? _localization.GetString("Loc.RoleStoreManager") : _localization.GetString("Loc.RoleClerk");
        public string WorkspaceTitle => _auth.IsTenantAdmin ? _localization.GetString("Loc.WorkspaceTenantOperations") : _auth.IsStoreManager ? _localization.GetString("Loc.WorkspaceStoreOperations") : _localization.GetString("Loc.WorkspacePointOfSale");
        public string WorkspaceSubtitle => _auth.IsTenantAdmin
            ? _localization.GetString("Loc.WorkspaceSubtitleTenant")
            : _auth.IsStoreManager
                ? _localization.GetString("Loc.WorkspaceSubtitleStoreManager")
                : _localization.GetString("Loc.WorkspaceSubtitleClerk");
        public string AppShellTitle => "Xavissa";
        public bool IsOnline => _net.IsOnline();
        public string ConnectivityStatusText => IsOnline ? "Online" : "Offline";
        public string SyncStatusText => _syncInProgress
            ? (string.IsNullOrWhiteSpace(_syncStatusText) ? _localization.GetString("Loc.Syncing") : _syncStatusText)
            : !string.IsNullOrWhiteSpace(_syncStatusText)
                ? _syncStatusText
            : IsOnline
                ? (_lastSyncAt.HasValue ? _localization.GetString("Loc.OnlineAndUpToDate") : _localization.GetString("Loc.AllChangesSavedLocally"))
                : _localization.GetString("Loc.OfflineCache");
        public string LastSyncText => _lastSyncAt.HasValue
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, _localization.GetString("Loc.LastSyncFormat"), _lastSyncAt.Value.ToLocalTime())
            : _localization.GetString("Loc.LastSyncNever");
        public bool IsDemoSession => _demoState.IsDemoActive || string.Equals(_auth.Username, "demo-admin", StringComparison.OrdinalIgnoreCase);
        public string DemoRemainingText => _demoState.RemainingSeconds <= 0
            ? "expired"
            : _demoState.RemainingSeconds < 60
                ? "less than 1 min left"
                : $"{Math.Max(1, _demoState.RemainingSeconds / 60)} min left";
        public bool ShowDemoWarning => IsDemoSession && _demoState.RemainingSeconds is > 0 and <= 600;
        public string DemoWarningText => "Demo expires in 10 minutes.";

        public string CurrentSectionTitle => CurrentView == HomeVm
            ? _localization.GetString("Loc.SalesWorkspace")
            : CurrentView == HistoryVm
                ? _localization.GetString("Loc.SalesHistory")
                : CurrentView == AnalyticsVm
                    ? _localization.GetString("Loc.Analytics")
                    : CurrentView == ManagementVm
                        ? CurrentManagementSectionTitle
                        : _localization.GetString("Loc.Settings");
        public string CurrentSectionSubtitle => CurrentView == HomeVm
            ? _localization.GetString("Loc.SectionSubtitleHome")
            : CurrentView == HistoryVm
                ? (_auth.IsTenantAdmin
                    ? _localization.GetString("Loc.SectionSubtitleHistoryTenant")
                    : _localization.GetString("Loc.SectionSubtitleHistoryStore"))
                : CurrentView == AnalyticsVm
                    ? (_auth.CanViewTenantAnalytics
                        ? _localization.GetString("Loc.SectionSubtitleAnalyticsTenant")
                        : _localization.GetString("Loc.SectionSubtitleAnalyticsStore"))
                : CurrentView == ManagementVm
                    ? WorkspaceSubtitle
                    : (_auth.CanEditTenantPrinting
                        ? _localization.GetString("Loc.SectionSubtitleSettingsTenant")
                        : _auth.CanEditStorePrinting
                            ? _localization.GetString("Loc.SectionSubtitleSettingsStore")
                            : _localization.GetString("Loc.SectionSubtitleSettingsGeneral"));

        public string CurrentManagementSectionTitle
        {
            get
            {
                if (IsManagementTeamActive)
                    return ManagementVm.TeamTabHeader;
                if (IsManagementStoresActive)
                    return ManagementVm.StoreTabHeader;
                if (IsManagementCategoriesActive)
                    return ManagementVm.CategoriesTabHeader;
                if (IsManagementProductsActive)
                    return ManagementVm.CatalogTabHeader;
                if (IsManagementVariantsActive)
                    return ManagementVm.VariantManagementTabHeader;

                return ManagementSectionTitle;
            }
        }

        public string ManagementSectionTitle => _auth.IsTenantAdmin
            ? _localization.GetString("Loc.Management")
            : _auth.IsStoreManager
                ? _localization.GetString("Loc.StoreProducts")
                : _localization.GetString("Loc.AssignedTasks");

        public bool CanAccessManagement => HasAssignedWorkspace && !IsUnsupportedDesktopRole && (_auth.CanManageEmployees || _auth.CanManageCatalog || _auth.CanManageStores);
        public bool CanAccessAnalytics => HasAssignedWorkspace && !IsUnsupportedDesktopRole && (_auth.CanViewTenantAnalytics || _auth.CanViewStoreAnalytics);
        public bool CanAccessHistory => HasAssignedWorkspace && !IsUnsupportedDesktopRole && _auth.CanViewHistory;
        public bool CanAccessConfig => HasAssignedWorkspace && !IsUnsupportedDesktopRole && _auth.IsLoggedIn;
        public bool CanPerformSales => _auth.CanPerformSales;
        public bool ShowSalesWorkspace => !IsUnsupportedDesktopRole && _auth.CanUsePOS;
        public bool ShowClerkNavigation => _auth.IsClerkOrCashier;
        public bool IsUnsupportedDesktopRole => _auth.CanUsePlatformAdmin && !_auth.IsTenantAdmin && !_auth.IsStoreManager && !_auth.IsClerkOrCashier;
        public bool ShowAnalyticsSection => !IsUnsupportedDesktopRole && (_auth.IsTenantAdmin || _auth.IsStoreManager);
        public bool HasAssignedWorkspace => _auth.AllowedTenants.Count > 0 || _auth.AllowedStores.Count > 0;

        public bool IsSidebarExpanded
        {
            get => _isSidebarExpanded;
            private set
            {
                if (!this.RaiseAndSetIfChanged(ref _isSidebarExpanded, value))
                    return;

                this.RaisePropertyChanged(nameof(SidebarWidth));
                this.RaisePropertyChanged(nameof(ShowManagementMenu));
            }
        }

        public double SidebarWidth => IsSidebarExpanded ? 248 : 68;
        public double SidebarOpenWidth => 248;
        public double SidebarCompactWidth => 68;

        public bool IsManagementMenuExpanded
        {
            get => _isManagementMenuExpanded;
            set
            {
                if (!this.RaiseAndSetIfChanged(ref _isManagementMenuExpanded, value))
                    return;

                this.RaisePropertyChanged(nameof(ShowManagementMenu));
            }
        }
        public bool ShowManagementMenu => CanAccessManagement && IsSidebarExpanded && IsManagementMenuExpanded;

        public bool IsHomeActive => CurrentView == HomeVm;
        public bool IsHistoryActive => CurrentView == HistoryVm;
        public bool IsAnalyticsActive => CurrentView == AnalyticsVm;
        public bool IsManagementActive => CurrentView == ManagementVm;
        public bool IsConfigActive => CurrentView == ConfigVm;
        public bool IsManagementTeamActive => IsManagementActive && IsSelectedManagementDestination(ManagementDestination.Team);
        public bool IsManagementStoresActive => IsManagementActive && IsSelectedManagementDestination(ManagementDestination.Stores);
        public bool IsManagementCategoriesActive => IsManagementActive && IsSelectedManagementDestination(ManagementDestination.Categories);
        public bool IsManagementProductsActive => IsManagementActive && IsSelectedManagementDestination(ManagementDestination.Catalog);
        public bool IsManagementVariantsActive => IsManagementActive && IsSelectedManagementDestination(ManagementDestination.Variants);

        public bool IsSessionBusy => _sessionLoad.IsBusy;
        public string SessionStatusText => _sessionLoad.StatusText;
        public bool HasStoreSwitcher => AllowedStores.Count > 1;
        public string CurrentStoreDisplayName
        {
            get
            {
                if (_auth.IsTenantAdmin && !_auth.SelectedStoreId.HasValue)
                    return _localization.GetString("Loc.WorkspaceTenantOperations");

                var selectedStore = _auth.AllowedStores.FirstOrDefault(store => store.Id == _auth.SelectedStoreId);
                if (selectedStore != null && !string.IsNullOrWhiteSpace(selectedStore.Name))
                    return selectedStore.Name;

                return _auth.AllowedStores.Count > 0
                    ? _auth.AllowedStores[0].Name
                    : _localization.GetString("Loc.Settings");
            }
        }

        public IReadOnlyList<AssignedStore> AllowedStores => _auth.AllowedStores;
        public int? SelectedStoreId
        {
            get => _auth.SelectedStoreId;
            set
            {
                if (!value.HasValue || value == _auth.SelectedStoreId || _isSwitchingStore)
                    return;

                _ = SwitchSelectedStoreAsync(value.Value);
            }
        }

        public Interaction<Unit, Unit> NavigateToLogin { get; } = new();
        public Interaction<Unit, Unit> ShowActivation { get; } = new();
        public Interaction<Unit, Unit> DemoExpired { get; } = new();
        public ReactiveCommand<Unit, Unit> ShowHomeCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowHistoryCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowAnalyticsCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowConfigCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleSidebarCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleManagementMenuCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementUsersCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementStoresCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementCategoriesCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementProductsCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowManagementVariantsCommand { get; }
        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
        public ReactiveCommand<Unit, Unit> ActivateLicenseCommand { get; }
        public ReactiveCommand<Unit, Unit> ContactSalesCommand { get; }

        public MainViewModel(IAuthService auth, IApiTokenProvider tokens, IBackgroundSyncService backgroundSync, IConnectivityService net, INotificationService notify, ILocalIdentityService localIdentity, IAuthRepositoryOnline authOnline, IOnlineSessionCredentialCache credentials, ILocalizationService localization, ISessionLoadState sessionLoad, ILicenseFeatureGate featureGate, IDemoStateService demoState, IOptions<LicensingOptions> licenseOptions, HomeViewModel homeVm, HistoryViewModel historyVm, AnalyticsViewModel analyticsVm, ManagementViewModel managementVm, ConfigViewModel configVm, NoWorkspaceViewModel noWorkspaceVm, UnsupportedRoleViewModel unsupportedRoleVm)
        {
            _auth = auth;
            _tokens = tokens;
            _backgroundSync = backgroundSync;
            _net = net;
            _notify = notify;
            _localIdentity = localIdentity;
            _authOnline = authOnline;
            _credentials = credentials;
            _localization = localization;
            _sessionLoad = sessionLoad;
            _featureGate = featureGate;
            _demoState = demoState;
            _licenseOptions = licenseOptions.Value;
            HomeVm = homeVm;
            HistoryVm = historyVm;
            AnalyticsVm = analyticsVm;
            ManagementVm = managementVm;
            ConfigVm = configVm;
            NoWorkspaceVm = noWorkspaceVm;
            UnsupportedRoleVm = unsupportedRoleVm;

            CurrentUser = _auth.CurrentUser?.Username ?? _localization.GetString("Loc.Guest");
            CurrentView = ResolveDefaultView();

            ShowHomeCommand = ReactiveCommand.Create(() =>
            {
                if (!ShowSalesWorkspace)
                    return;

                CurrentView = HomeVm;
                _ = LoadCurrentViewAsync();
            });
            ShowHistoryCommand = ReactiveCommand.Create(() =>
            {
                CurrentView = HistoryVm;
                _ = LoadCurrentViewAsync();
            });
            ShowAnalyticsCommand = ReactiveCommand.CreateFromTask(ShowAnalyticsAsync);
            ShowManagementCommand = ReactiveCommand.Create(OpenManagementSection);
            ShowConfigCommand = ReactiveCommand.Create(() =>
            {
                if (!CanAccessConfig)
                    return;

                CurrentView = ConfigVm;
                _ = LoadCurrentViewAsync();
            });
            ToggleSidebarCommand = ReactiveCommand.Create(ToggleSidebar);
            ToggleManagementMenuCommand = ReactiveCommand.Create(ToggleManagementMenu);
            ShowManagementUsersCommand = ReactiveCommand.Create(() => NavigateToManagement(ManagementDestination.Team));
            ShowManagementStoresCommand = ReactiveCommand.Create(() => NavigateToManagement(ManagementDestination.Stores));
            ShowManagementCategoriesCommand = ReactiveCommand.Create(() => NavigateToManagement(ManagementDestination.Categories));
            ShowManagementProductsCommand = ReactiveCommand.Create(() => NavigateToManagement(ManagementDestination.Catalog));
            ShowManagementVariantsCommand = ReactiveCommand.Create(() => NavigateToManagement(ManagementDestination.Variants));
            LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
            ActivateLicenseCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                await _demoState.TrackEventAsync("LicenseActivationClicked", description: "Activation clicked from demo shell.");
                await ShowActivation.Handle(Unit.Default).ToTask();
            });
            ContactSalesCommand = ReactiveCommand.CreateFromTask(ContactSalesAsync);

            _auth.UserChanged += RefreshUser;
            _auth.SessionExpired += OnSessionExpired;
            _localization.LanguageChanged += RefreshLocalizedText;
            _sessionLoad.Changed += OnSessionLoadChanged;
            _sessionLoad.OnlineDataApplied += OnOnlineDataApplied;
            _backgroundSync.StatusChanged += OnBackgroundSyncStatusChanged;
            ManagementVm.PropertyChanged += OnManagementPropertyChanged;
            _demoState.StateChanged += OnDemoStateChanged;
            StartClock();
            StartConnectivityMonitor();
            StartDemoExpirationMonitor();
            UpdateNavigationState();
            Dispatcher.UIThread.Post(() => _ = LoadCurrentViewAsync(), DispatcherPriority.Background);
        }

        private async Task LogoutAsync()
        {
            if (!string.IsNullOrWhiteSpace(_auth.CurrentUser?.Username))
                await _localIdentity.ClearCachedTokenAsync(_auth.CurrentUser.Username);

            _tokens.Clear();
            _credentials.Clear();
            _auth.Logout();
            await NavigateToLogin.Handle(Unit.Default).ToTask();
        }

        private async void OnSessionExpired()
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                _notify.Show("Your session has expired. Please sign in again.", NotificationType.Warning, 3500);
                await NavigateToLogin.Handle(Unit.Default).ToTask();
            });
        }

        private void RefreshUser()
        {
            CurrentUser = _auth.CurrentUser?.Username ?? _localization.GetString("Loc.Guest");
            this.RaisePropertyChanged(nameof(CanAccessManagement));
            this.RaisePropertyChanged(nameof(CanAccessAnalytics));
            this.RaisePropertyChanged(nameof(CanAccessHistory));
            this.RaisePropertyChanged(nameof(CanAccessConfig));
            this.RaisePropertyChanged(nameof(CanPerformSales));
            this.RaisePropertyChanged(nameof(AllowedStores));
            this.RaisePropertyChanged(nameof(SelectedStoreId));
            this.RaisePropertyChanged(nameof(HasStoreSwitcher));
            this.RaisePropertyChanged(nameof(CurrentStoreDisplayName));
            this.RaisePropertyChanged(nameof(CurrentRoleLabel));
            this.RaisePropertyChanged(nameof(WorkspaceTitle));
            this.RaisePropertyChanged(nameof(WorkspaceSubtitle));
            this.RaisePropertyChanged(nameof(ManagementSectionTitle));
            this.RaisePropertyChanged(nameof(ShowSalesWorkspace));
            this.RaisePropertyChanged(nameof(ShowClerkNavigation));
            this.RaisePropertyChanged(nameof(ShowAnalyticsSection));
            this.RaisePropertyChanged(nameof(IsUnsupportedDesktopRole));
            this.RaisePropertyChanged(nameof(HasAssignedWorkspace));
            this.RaisePropertyChanged(nameof(CurrentSectionTitle));
            this.RaisePropertyChanged(nameof(CurrentSectionSubtitle));
            this.RaisePropertyChanged(nameof(CurrentManagementSectionTitle));
            this.RaisePropertyChanged(nameof(ShowManagementMenu));
            this.RaisePropertyChanged(nameof(IsDemoSession));
            this.RaisePropertyChanged(nameof(DemoRemainingText));
            this.RaisePropertyChanged(nameof(ShowDemoWarning));
            this.RaisePropertyChanged(nameof(SyncStatusText));
            this.RaisePropertyChanged(nameof(LastSyncText));

            if (!CanAccessManagement && CurrentView == ManagementVm)
                CurrentView = ResolveDefaultView();
            if ((!CanAccessAnalytics || !ShowAnalyticsSection) && CurrentView == AnalyticsVm)
                CurrentView = ResolveDefaultView();
            if (!CanAccessConfig && CurrentView == ConfigVm)
                CurrentView = ResolveDefaultView();
            if (!ShowSalesWorkspace && CurrentView == HomeVm)
                CurrentView = ResolveDefaultView();

            if (!CanAccessManagement)
                IsManagementMenuExpanded = false;
            else
                this.RaisePropertyChanged(nameof(ShowManagementMenu));

            UpdateNavigationState();
        }

        private async Task RefreshStoreScopedViewsAsync()
        {
            try
            {
                StartSyncStatus(_localization.GetString("Loc.UpdatingCatalog"));
                HomeVm.ResetForStoreChange();
                HistoryVm.ResetForStoreChange();
                AnalyticsVm.ResetForStoreChange();
                await ReloadDataViewsAsync();
                _backgroundSync.RequestSync(BackgroundSyncReason.StoreChanged);
            }
            catch (Exception ex)
            {
                MarkSyncFailed();
                _notify.Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, _localization.GetString("Loc.RefreshStoreDataFailed"), ex.Message), NotificationType.Warning, 3000);
            }
            finally
            {
                _syncInProgress = false;
                this.RaisePropertyChanged(nameof(SyncStatusText));
            }
        }

        private async Task SwitchSelectedStoreAsync(int storeId)
        {
            try
            {
                _isSwitchingStore = true;

                if (_net.IsOnline())
                {
                    if (!_credentials.HasCredentials)
                    {
                        _notify.Show("Sign in again before switching stores online.", NotificationType.Warning, 3500);
                        this.RaisePropertyChanged(nameof(SelectedStoreId));
                        return;
                    }

                    var login = await _authOnline.SelectStoreAsync(_credentials.Username, _credentials.Password, storeId);
                    if (login == null || string.IsNullOrWhiteSpace(login.Token))
                    {
                        _notify.Show("Store switch failed. The backend did not issue a store-scoped token.", NotificationType.Error, 3500);
                        this.RaisePropertyChanged(nameof(SelectedStoreId));
                        return;
                    }

                    await _localIdentity.SaveFromOnlineLoginAsync(login, _credentials.Password);
                    _auth.ApplyOnlineSession(login);
                }
                else if (!_auth.SetSelectedStore(storeId))
                {
                    _notify.Show("Store switch is unavailable for this cached identity.", NotificationType.Warning, 3000);
                    this.RaisePropertyChanged(nameof(SelectedStoreId));
                    return;
                }
                else
                {
                    _notify.Show("Switched store in offline cached mode. Backend scope will refresh when online.", NotificationType.Warning, 3500);
                }

                this.RaisePropertyChanged(nameof(SelectedStoreId));
                this.RaisePropertyChanged(nameof(CurrentRoleLabel));
                this.RaisePropertyChanged(nameof(WorkspaceTitle));
                this.RaisePropertyChanged(nameof(WorkspaceSubtitle));
                this.RaisePropertyChanged(nameof(CurrentStoreDisplayName));
                await RefreshStoreScopedViewsAsync();
            }
            catch (ApiException ex)
            {
                _notify.Show(ex.Message, ex.IsPermissionDenied ? NotificationType.Warning : NotificationType.Error, 3500);
                this.RaisePropertyChanged(nameof(SelectedStoreId));
            }
            catch (Exception ex)
            {
                _notify.Show("Store switch failed: " + ex.Message, NotificationType.Error, 3500);
                this.RaisePropertyChanged(nameof(SelectedStoreId));
            }
            finally
            {
                _isSwitchingStore = false;
            }
        }

        private void UpdateNavigationState()
        {
            this.RaisePropertyChanged(nameof(IsHomeActive));
            this.RaisePropertyChanged(nameof(IsHistoryActive));
            this.RaisePropertyChanged(nameof(IsAnalyticsActive));
            this.RaisePropertyChanged(nameof(IsManagementActive));
            this.RaisePropertyChanged(nameof(IsConfigActive));
            this.RaisePropertyChanged(nameof(IsManagementTeamActive));
            this.RaisePropertyChanged(nameof(IsManagementStoresActive));
            this.RaisePropertyChanged(nameof(IsManagementCategoriesActive));
            this.RaisePropertyChanged(nameof(IsManagementProductsActive));
            this.RaisePropertyChanged(nameof(IsManagementVariantsActive));
            this.RaisePropertyChanged(nameof(CurrentManagementSectionTitle));
        }

        private ViewModelBase ResolveDefaultView()
        {
            if (!HasAssignedWorkspace)
                return NoWorkspaceVm;
            if (IsUnsupportedDesktopRole)
                return UnsupportedRoleVm;
            if (ShowAnalyticsSection && CanAccessAnalytics)
                return AnalyticsVm;
            if (ShowSalesWorkspace)
                return HomeVm;
            if (CanAccessManagement)
                return ManagementVm;
            return ConfigVm;
        }

        private void ToggleSidebar()
        {
            IsSidebarExpanded = !IsSidebarExpanded;
        }

        private void OpenManagementSection()
        {
            if (!CanAccessManagement)
                return;

            CurrentView = ManagementVm;
            _ = LoadCurrentViewAsync();
        }

        private async Task ShowAnalyticsAsync()
        {
            if (!await EnsureDemoStillActiveAsync())
                return;

            if (!ShowAnalyticsSection || !CanAccessAnalytics)
                return;

            await _demoState.TrackEventAsync("ReportOpened", "Report", "Analytics", "Analytics report opened.");

            var feature = await _featureGate.EnsureFeatureAsync(LicenseFeature.AdvancedReports, _auth.SelectedTenantId);
            if (!feature.Allowed)
            {
                _notify.Show(feature.Message ?? "Advanced reports are not included in the current license.", NotificationType.Warning);
                return;
            }

            CurrentView = AnalyticsVm;
            await LoadCurrentViewAsync();
        }

        private void ToggleManagementMenu()
        {
            if (!CanAccessManagement || !IsSidebarExpanded)
                return;

            IsManagementMenuExpanded = !IsManagementMenuExpanded;
        }

        private void NavigateToManagement(ManagementDestination destination)
        {
            if (!CanAccessManagement)
                return;

            OpenManagementSection();

            var targetIndex = GetManagementTabIndex(destination);
            if (targetIndex.HasValue)
                ManagementVm.SelectedTabIndex = targetIndex.Value;

            UpdateNavigationState();
            this.RaisePropertyChanged(nameof(CurrentSectionTitle));
        }

        private int? GetManagementTabIndex(ManagementDestination destination)
        {
            var index = 0;

            if (ManagementVm.ShowEmployeeTabs)
            {
                if (destination == ManagementDestination.Team)
                    return index;
                index++;
            }

            if (ManagementVm.ShowStoreTab)
            {
                if (destination == ManagementDestination.Stores)
                    return index;
                index++;
            }

            if (ManagementVm.ShowCategoriesTab)
            {
                if (destination == ManagementDestination.Categories)
                    return index;
                index++;
            }

            if (ManagementVm.ShowCatalogTabs)
            {
                if (destination == ManagementDestination.Catalog)
                    return index;
                index++;
            }

            if (ManagementVm.ShowVariantManagementTab && destination == ManagementDestination.Variants)
                return index;

            return null;
        }

        private bool IsSelectedManagementDestination(ManagementDestination destination)
        {
            var targetIndex = GetManagementTabIndex(destination);
            return targetIndex.HasValue && ManagementVm.SelectedTabIndex == targetIndex.Value;
        }

        private void OnManagementPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagementViewModel.SelectedTabIndex)
                || e.PropertyName == nameof(ManagementViewModel.ShowEmployeeTabs)
                || e.PropertyName == nameof(ManagementViewModel.ShowStoreTab)
                || e.PropertyName == nameof(ManagementViewModel.ShowCategoriesTab)
                || e.PropertyName == nameof(ManagementViewModel.ShowCatalogTabs)
                || e.PropertyName == nameof(ManagementViewModel.ShowVariantManagementTab)
                || e.PropertyName == nameof(ManagementViewModel.TeamTabHeader)
                || e.PropertyName == nameof(ManagementViewModel.StoreTabHeader)
                || e.PropertyName == nameof(ManagementViewModel.CategoriesTabHeader)
                || e.PropertyName == nameof(ManagementViewModel.CatalogTabHeader)
                || e.PropertyName == nameof(ManagementViewModel.VariantManagementTabHeader))
            {
                UpdateNavigationState();
                this.RaisePropertyChanged(nameof(CurrentSectionTitle));
            }
        }

        private void StartClock()
        {
            UpdateTime();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => UpdateTime();
            timer.Start();
        }

        private void StartDemoExpirationMonitor()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            timer.Tick += async (_, _) =>
            {
                await _demoState.CheckExpirationAsync();
                if (_demoState.IsExpired)
                    await DemoExpired.Handle(Unit.Default).ToTask();
            };
            timer.Start();
        }

        private void StartConnectivityMonitor()
        {
            _wasOnline = _net.IsOnline();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += async (_, _) =>
            {
                var isOnline = _net.IsOnline();
                if (isOnline && !_wasOnline)
                {
                    _notify.Show(_localization.GetString("Loc.Online"));
                    RequestSyncAfterReconnect();
                }
                else if (!isOnline && _wasOnline)
                {
                    _notify.Show(_localization.GetString("Loc.Offline"), NotificationType.Warning, 2500);
                }

                _wasOnline = isOnline;
                this.RaisePropertyChanged(nameof(IsOnline));
                this.RaisePropertyChanged(nameof(ConnectivityStatusText));
            };
            timer.Start();
        }

        private void RequestSyncAfterReconnect()
        {
            if (_syncInProgress)
                return;
            _syncInProgress = true;
            _syncStatusText = _localization.GetString("Loc.UploadingSales");
            this.RaisePropertyChanged(nameof(SyncStatusText));
            _notify.Show(_localization.GetString("Loc.OnlineSyncing"));
            _backgroundSync.RequestSync(BackgroundSyncReason.Reconnected);
        }

        private void UpdateTime()
        {
            CurrentDate = DateTime.Now.ToString("dd MMM yyyy");
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            this.RaisePropertyChanged(nameof(DemoRemainingText));
            this.RaisePropertyChanged(nameof(ShowDemoWarning));
        }

        private void OnDemoStateChanged(DemoSessionState state)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                this.RaisePropertyChanged(nameof(IsDemoSession));
                this.RaisePropertyChanged(nameof(DemoRemainingText));
                this.RaisePropertyChanged(nameof(ShowDemoWarning));
            });
        }

        private async Task<bool> EnsureDemoStillActiveAsync()
        {
            await _demoState.CheckExpirationAsync();
            if (!_demoState.IsExpired)
                return true;

            await DemoExpired.Handle(Unit.Default).ToTask();
            return false;
        }

        private async Task ContactSalesAsync()
        {
            await _demoState.TrackEventAsync("ContactSalesClicked", description: "Contact sales clicked from demo shell.");
            if (string.IsNullOrWhiteSpace(_licenseOptions.DemoContactWhatsApp))
            {
                _notify.Show("Contact sales is not configured yet.", NotificationType.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_licenseOptions.DemoContactWhatsApp) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _notify.Show("Could not open contact link: " + ex.Message, NotificationType.Warning);
            }
        }

        private void OnBackgroundSyncStatusChanged(object? sender, BackgroundSyncStatusChangedEventArgs e)
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var status = e.Status;
                _syncInProgress = status.State == BackgroundSyncState.Running;

                if (status.State == BackgroundSyncState.Failed)
                    _syncStatusText = _localization.GetString("Loc.SyncFailed");
                else if (status.State == BackgroundSyncState.Running)
                    _syncStatusText = status.Message;
                else if (status.State == BackgroundSyncState.Skipped)
                    _syncStatusText = string.Empty;
                else
                    _syncStatusText = string.Empty;

                if (status.LastSuccessfulSyncAt.HasValue)
                    _lastSyncAt = status.LastSuccessfulSyncAt.Value.LocalDateTime;

                this.RaisePropertyChanged(nameof(SyncStatusText));
                this.RaisePropertyChanged(nameof(LastSyncText));

                if (status.State == BackgroundSyncState.Idle && status.ShouldRefreshLocalViews)
                {
                    try
                    {
                        await ReloadDataViewsAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Background sync view reload failed: " + ex.Message);
                    }
                }
            });
        }

        private void RefreshLocalizedText()
        {
            this.RaisePropertyChanged(nameof(CurrentRoleLabel));
            this.RaisePropertyChanged(nameof(WorkspaceTitle));
            this.RaisePropertyChanged(nameof(WorkspaceSubtitle));
            this.RaisePropertyChanged(nameof(CurrentSectionTitle));
            this.RaisePropertyChanged(nameof(CurrentSectionSubtitle));
            this.RaisePropertyChanged(nameof(ManagementSectionTitle));
            this.RaisePropertyChanged(nameof(CurrentManagementSectionTitle));
            this.RaisePropertyChanged(nameof(SyncStatusText));
            this.RaisePropertyChanged(nameof(LastSyncText));

            if (string.IsNullOrWhiteSpace(_auth.CurrentUser?.Username))
                CurrentUser = _localization.GetString("Loc.Guest");
        }

        private void OnSessionLoadChanged()
        {
            Dispatcher.UIThread.Post(() =>
            {
                this.RaisePropertyChanged(nameof(IsSessionBusy));
                this.RaisePropertyChanged(nameof(SessionStatusText));
            });
        }

        private void OnOnlineDataApplied()
        {
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    MarkSyncComplete();
                    await ReloadDataViewsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Post-login refresh failed: " + ex.Message);
                }
            });
        }

        private async Task ReloadDataViewsAsync()
        {
            var reloads = new List<Task>
            {
                HomeVm.LoadProductsAsync(),
                HistoryVm.ReloadCommand.Execute().ToTask(),
            };

            if (CanAccessAnalytics && ShowAnalyticsSection)
                reloads.Add(AnalyticsVm.LoadAnalyticsAsync());

            if (CanAccessManagement)
                reloads.Add(ManagementVm.LoadCommand.Execute().ToTask());

            await Task.WhenAll(reloads);
        }

        private async Task LoadCurrentViewAsync()
        {
            if (CurrentView == HomeVm && ShowSalesWorkspace)
            {
                await HomeVm.LoadProductsAsync();
                return;
            }

            if (CurrentView == HistoryVm && CanAccessHistory)
            {
                await HistoryVm.EnsureLoadedAsync();
                return;
            }

            if (CurrentView == AnalyticsVm && CanAccessAnalytics && ShowAnalyticsSection)
            {
                await AnalyticsVm.EnsureLoadedAsync();
                return;
            }

            if (CurrentView == ManagementVm && CanAccessManagement)
            {
                await ManagementVm.EnsureLoadedAsync();
                return;
            }

            if (CurrentView == ConfigVm && CanAccessConfig)
                await ConfigVm.EnsureLoadedAsync();
        }

        private void StartSyncStatus(string status)
        {
            _syncInProgress = true;
            _syncStatusText = status;
            this.RaisePropertyChanged(nameof(SyncStatusText));
        }

        private void MarkSyncComplete()
        {
            _lastSyncAt = DateTime.Now;
            _syncStatusText = string.Empty;
            this.RaisePropertyChanged(nameof(SyncStatusText));
            this.RaisePropertyChanged(nameof(LastSyncText));
        }

        private void MarkSyncFailed()
        {
            _syncStatusText = _localization.GetString("Loc.SyncFailed");
            this.RaisePropertyChanged(nameof(SyncStatusText));
        }
    }
}
