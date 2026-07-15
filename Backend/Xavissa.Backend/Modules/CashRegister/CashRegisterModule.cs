using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.CashRegister;

public static class CashRegisterModule
{
    public static IServiceCollection AddCashRegisterModule(this IServiceCollection services)
    {
        services.AddScoped<CashRegisterService>();
        return services;
    }
}
