using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddScoped<StockTransferService>();
        services.AddScoped<StockAdjustmentService>();
        return services;
    }
}
