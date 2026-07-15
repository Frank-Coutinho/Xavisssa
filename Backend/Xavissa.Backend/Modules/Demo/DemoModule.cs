using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.Demo;

public static class DemoModule
{
    public static IServiceCollection AddDemoModule(this IServiceCollection services)
    {
        services.AddScoped<IDemoService, DemoService>();
        return services;
    }
}
