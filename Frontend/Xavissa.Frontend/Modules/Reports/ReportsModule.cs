using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Reports;

public static class ReportsModule
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<AnalyticsViewModel>();
        return services;
    }
}
