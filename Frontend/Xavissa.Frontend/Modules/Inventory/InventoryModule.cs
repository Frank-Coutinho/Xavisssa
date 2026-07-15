using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Services;

namespace Xavissa.Frontend.Modules.Inventory;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddScoped<IStockAvailabilityService, StockAvailabilityService>();
        services.AddScoped<StockAdjustmentService>();
        services.AddScoped<IStockAdjustmentService>(provider => provider.GetRequiredService<StockAdjustmentService>());
        services.AddScoped<IStockAdjustmentSyncService>(provider => provider.GetRequiredService<StockAdjustmentService>());
        return services;
    }
}
