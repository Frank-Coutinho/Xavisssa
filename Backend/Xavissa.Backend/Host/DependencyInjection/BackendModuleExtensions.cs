using Xavissa.Backend.Modules.CashRegister;
using Xavissa.Backend.Modules.Catalog;
using Xavissa.Backend.Modules.Demo;
using Xavissa.Backend.Modules.Identity;
using Xavissa.Backend.Modules.Inventory;
using Xavissa.Backend.Modules.Sales;
using Xavissa.Backend.Modules.Synchronization;

namespace Xavissa.Backend.Host.DependencyInjection;

public static class BackendModuleExtensions
{
    public static IServiceCollection AddBackendModules(this IServiceCollection services)
    {
        return services
            .AddCatalogModule()
            .AddSalesModule()
            .AddInventoryModule()
            .AddCashRegisterModule()
            .AddIdentityModule()
            .AddSynchronizationModule()
            .AddDemoModule();
    }
}
