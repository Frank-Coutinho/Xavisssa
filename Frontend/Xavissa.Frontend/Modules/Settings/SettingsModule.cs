using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Settings;

public static class SettingsModule
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services)
    {
        services.AddScoped<ConfigViewModel>();
        return services;
    }
}
