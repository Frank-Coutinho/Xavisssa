using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Modules.Synchronization;

public static class SynchronizationModule
{
    public static IServiceCollection AddSynchronizationModule(this IServiceCollection services)
    {
        services.AddSingleton<BackgroundSyncService>();
        services.AddSingleton<IBackgroundSyncService>(sp =>
            sp.GetRequiredService<BackgroundSyncService>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundSyncService>());
        services.AddScoped<ISyncService, SyncService>();
        return services;
    }
}
