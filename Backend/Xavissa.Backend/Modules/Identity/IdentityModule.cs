using Microsoft.Extensions.DependencyInjection;
using Xavissa.Backend.Services;

namespace Xavissa.Backend.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<SystemAdminSeedService>();
        return services;
    }
}
