using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Shell;

public static class ShellModule
{
    public static IServiceCollection AddShell(this IServiceCollection services)
    {
        services.AddScoped<MainViewModel>();
        services.AddScoped<NoWorkspaceViewModel>();
        services.AddScoped<UnsupportedRoleViewModel>();
        services.AddScoped<AppViewModel>();
        return services;
    }
}
