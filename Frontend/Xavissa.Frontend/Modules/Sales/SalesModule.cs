using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Sales;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddScoped<ISaleOnlineRepository, SaleRepositoryOnline>();
        services.AddScoped<ISaleOfflineRepository, SaleRepositoryOffline>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IBarcodeScannerInputService, BarcodeScannerInputService>();
        services.AddScoped<HomeViewModel>();
        services.AddScoped<HistoryViewModel>();
        return services;
    }
}
