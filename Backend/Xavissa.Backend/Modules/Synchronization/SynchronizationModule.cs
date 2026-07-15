using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.Synchronization;

public static class SynchronizationModule
{
    public static IServiceCollection AddSynchronizationModule(this IServiceCollection services)
    {
        services.AddScoped<SyncService>();
        services.AddScoped<SyncConflictService>();
        return services;
    }
}
