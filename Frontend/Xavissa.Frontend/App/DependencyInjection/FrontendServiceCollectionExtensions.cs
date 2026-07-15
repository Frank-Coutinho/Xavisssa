using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xavissa.Frontend.Auth.Common;
using Xavissa.Frontend.Data;
using Xavissa.Frontend.Infrastructure.LocalDatabase;
using Xavissa.Frontend.Models;
using Xavissa.Frontend.Modules.Catalog;
using Xavissa.Frontend.Modules.Identity;
using Xavissa.Frontend.Modules.Licensing;
using Xavissa.Frontend.Modules.Reports;
using Xavissa.Frontend.Modules.Sales;
using Xavissa.Frontend.Modules.Settings;
using Xavissa.Frontend.Modules.Synchronization;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.Services.Auth;
using Xavissa.Frontend.Shell;

namespace Xavissa.Frontend.Bootstrap.DependencyInjection;

public static class FrontendServiceCollectionExtensions
{
    public static IServiceCollection AddXavissaFrontend(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BackendOptions>(configuration.GetSection("Backend"));
        services.Configure<LicensingOptions>(configuration.GetSection("Licensing"));

        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IDbContextFactory<LocalDbContext>, WorkspaceDbContextFactory>();
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<LocalDbContext>>().CreateDbContext());
        services.AddSingleton<IWorkspaceSchemaInitializer, WorkspaceSchemaInitializer>();

        services.AddSingleton<IApiTokenProvider, ApiTokenProvider>();
        services.AddTransient<AuthMessageHandler>();
        services.AddHttpClient("backend", (sp, client) =>
            client.BaseAddress = sp.GetRequiredService<IOptions<BackendOptions>>().Value.BaseUri)
            .AddHttpMessageHandler<AuthMessageHandler>();
        services.AddHttpClient("licensing", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LicensingOptions>>().Value;
            if (options.LicensingApiBaseUrl != null)
                client.BaseAddress = options.LicensingApiBaseUrl;
        });
        services.AddHttpClient("demo", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LicensingOptions>>().Value;
            client.BaseAddress = options.DemoApiBaseUrl ?? options.LicensingApiBaseUrl;
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

        return services
            .AddIdentityModule()
            .AddCatalogModule()
            .AddSalesModule()
            .AddReportsModule()
            .AddSettingsModule()
            .AddSynchronizationModule()
            .AddLicensingModule()
            .AddShell();
    }
}
