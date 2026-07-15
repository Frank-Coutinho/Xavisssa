using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.Sales;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddScoped<SalesService>();
        return services;
    }
}
