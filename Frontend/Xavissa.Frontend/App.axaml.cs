using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.Services.Auth;
using Xavissa.Frontend.ViewModels;
using Xavissa.Frontend.Views;

namespace Xavissa.Frontend
{
    public partial class App : Application
    {
        private IHost? _host;

        public override void Initialize()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override async void OnFrameworkInitializationCompleted()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter(
                        "Microsoft.EntityFrameworkCore.Database.Command",
                        LogLevel.Warning
                    );
                })
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.Configure<BackendOptions>(context.Configuration.GetSection("Backend"));
                        services.Configure<LicensingOptions>(context.Configuration.GetSection("Licensing"));
                        services.AddSingleton<IWorkspaceService, WorkspaceService>();
                        services.AddSingleton<IDbContextFactory<LocalDbContext>, WorkspaceDbContextFactory>();
                        services.AddScoped(sp =>
                            sp.GetRequiredService<IDbContextFactory<LocalDbContext>>().CreateDbContext());

                        services.AddSingleton<IApiTokenProvider, ApiTokenProvider>();
                        services.AddTransient<AuthMessageHandler>();
                        services
                            .AddHttpClient(
                                "backend",
                                (sp, c) => { c.BaseAddress = sp.GetRequiredService<IOptions<BackendOptions>>().Value.BaseUri; })
                            .AddHttpMessageHandler<AuthMessageHandler>();
                        services.AddHttpClient(
                            "licensing",
                            (sp, c) =>
                            {
                                var options = sp.GetRequiredService<IOptions<LicensingOptions>>().Value;
                                if (options.LicensingApiBaseUrl != null)
                                    c.BaseAddress = options.LicensingApiBaseUrl;
                            });
                        services.AddHttpClient(
                            "demo",
                            (sp, c) =>
                            {
                                var options = sp.GetRequiredService<IOptions<LicensingOptions>>().Value;
                                if (options.DemoApiBaseUrl != null)
                                    c.BaseAddress = options.DemoApiBaseUrl;
                                else if (options.LicensingApiBaseUrl != null)
                                    c.BaseAddress = options.LicensingApiBaseUrl;
                            });

                        services.AddSingleton<IBackendHealthService, BackendHealthService>();
                        services.AddSingleton<IApiErrorHandler, ApiErrorHandler>();
                        services.AddSingleton<IOnlineSessionCredentialCache, OnlineSessionCredentialCache>();
                        services.AddSingleton<IConnectivityService, ConnectivityService>();
                        services.AddSingleton<IPrinterService, PrinterService>();
                        services.AddSingleton<INotificationService, NotificationService>();
                        services.AddSingleton<IConfirmationDialogService, ConfirmationDialogService>();
                        services.AddSingleton<IThemeService, ThemeService>();
                        services.AddSingleton<ILocalizationService, LocalizationService>();
                        services.AddSingleton<ISessionLoadState, SessionLoadState>();
                        services.AddSingleton<IBackendProcessManager, BackendProcessManager>();
                        services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();
                        services.AddSingleton<IDeviceFingerprintService, DeviceFingerprintService>();
                        services.AddSingleton<ILocalLicenseStore, LocalLicenseStore>();
                        services.AddSingleton<ILicenseSnapshotVerifier, LicenseSnapshotVerifier>();
                        services.AddSingleton<ILicensingApiClient, LicensingApiClient>();
                        services.AddSingleton<ILicenseStateService, LicenseStateService>();
                        services.AddSingleton<ILicenseFeatureGate, LicenseFeatureGate>();
                        services.AddSingleton<ITenantQuotaService, TenantQuotaService>();
                        services.AddSingleton<ILicenseCacheService, LicenseCacheService>();
                        services.AddSingleton<ILicenseClient, LicenseClient>();
                        services.AddSingleton<IDemoApiClient, DemoApiClient>();
                        services.AddSingleton<IDemoStateService, DemoStateService>();
                        services.AddSingleton<IDemoCleanupService, DemoCleanupService>();
                        services.AddSingleton<IDemoWorkspaceSeeder, DemoWorkspaceSeeder>();
                        services.AddSingleton<IAuthService, AuthService>();
                        services.AddSingleton<ILoginCoordinator, LoginCoordinator>();
                        services.AddSingleton<BackgroundSyncService>();
                        services.AddSingleton<IBackgroundSyncService>(sp =>
                            sp.GetRequiredService<BackgroundSyncService>());
                        services.AddHostedService(sp =>
                            sp.GetRequiredService<BackgroundSyncService>());

                        services.AddScoped<ISyncService, SyncService>();
                        services.AddScoped<IBarcodeScannerInputService, BarcodeScannerInputService>();

                        services.AddScoped<IUserRepositoryOnline, UserRepositoryOnline>();
                        services.AddScoped<IUserRepositoryOffline, UserRepositoryOffline>();
                        services.AddScoped<IUserRepository, UserRepository>();
                        services.AddScoped<IStoreAdminRepository, StoreAdminRepository>();

                        services.AddScoped<IProductRepositoryOnline, ProductRepositoryOnline>();
                        services.AddScoped<IProductRepositoryOffline, ProductRepositoryOffline>();
                        services.AddScoped<IProductRepository, ProductRepository>();

                        services.AddScoped<ISaleOnlineRepository, SaleRepositoryOnline>();
                        services.AddScoped<ISaleOfflineRepository, SaleRepositoryOffline>();
                        services.AddScoped<ISaleRepository, SaleRepository>();

                        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
                        services.AddScoped<IAuthRepositoryOnline, AuthRepositoryOnline>();
                        services.AddScoped<ILocalIdentityService, LocalIdentityService>();

                        services.AddScoped<LoginViewModel>();
                        services.AddScoped<MainViewModel>();
                        services.AddScoped<HomeViewModel>();
                        services.AddScoped<HistoryViewModel>();
                        services.AddScoped<AnalyticsViewModel>();
                        services.AddScoped<ManagementViewModel>();
                        services.AddScoped<ConfigViewModel>();
                        services.AddScoped<NoWorkspaceViewModel>();
                        services.AddScoped<UnsupportedRoleViewModel>();
                        services.AddScoped<LicenseActivationViewModel>();
                        services.AddScoped<LicenseBlockedViewModel>();
                        services.AddScoped<LicenseStatusViewModel>();
                        services.AddScoped<DemoExpiredViewModel>();
                        services.AddScoped<AppViewModel>();
                    }
                )
                .Build();

            await _host.StartAsync();

            var printerService = _host.Services.GetRequiredService<IPrinterService>();
            var themeService = _host.Services.GetRequiredService<IThemeService>();
            var localizationService = _host.Services.GetRequiredService<ILocalizationService>();

            localizationService.SetLanguage(printerService.LanguageCode);
            if (printerService.IsDarkMode)
                themeService.SetDark();
            else
                themeService.SetLight();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var rootVm = _host.Services.GetRequiredService<AppViewModel>();

                desktop.MainWindow = new MainWindow
                {
                    Width = 1000,
                    Height = 600,
                    DataContext = rootVm,
                };

                desktop.Exit += async (_, __) =>
                {
                    var demoCleanup = _host.Services.GetRequiredService<IDemoCleanupService>();
                    await demoCleanup.CleanupOnCloseAsync();
                    var backend = _host.Services.GetRequiredService<IBackendProcessManager>();
                    await backend.StopAsync();
                    await _host.StopAsync();
                    _host.Dispose();
                };
            }

            // Keep the critical UI path short. Login also calls EnsureLocalSchemaAsync,
            // so this background warm-up is safe if the user acts immediately.
            _ = Task.Run(async () =>
            {
                using var scope = _host.Services.CreateScope();
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();
                    await db.EnsureLocalSchemaAsync();
                    Debug.WriteLine("Local SQLite schema ensured.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Local DB bootstrap failed:");
                    Debug.WriteLine(ex);
                }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var health = _host.Services.GetRequiredService<IBackendHealthService>();
                    health.MarkBackendStarting();
                    var backend = _host.Services.GetRequiredService<IBackendProcessManager>();
                    await backend.StartAsync();
                    health.StartMonitoring();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Backend startup failed: " + ex.Message);
                    _host.Services.GetRequiredService<IBackendHealthService>().MarkOfflineCachedMode();
                }
            });

            base.OnFrameworkInitializationCompleted();
        }
    }
}
