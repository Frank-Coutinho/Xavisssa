using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Licensing;

public static class LicensingModule
{
    public static IServiceCollection AddLicensingModule(this IServiceCollection services)
    {
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
        services.AddScoped<LicenseActivationViewModel>();
        services.AddScoped<LicenseBlockedViewModel>();
        services.AddScoped<LicenseStatusViewModel>();
        services.AddScoped<DemoExpiredViewModel>();
        return services;
    }
}
