using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<IProductRepositoryOnline, ProductRepositoryOnline>();
        services.AddScoped<IProductRepositoryOffline, ProductRepositoryOffline>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStoreAdminRepository, StoreAdminRepository>();
        services.AddScoped<ManagementViewModel>();
        return services;
    }
}
