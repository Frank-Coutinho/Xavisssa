using Microsoft.Extensions.DependencyInjection;
using Xavissa.Frontend.Data.Repositories;
using Xavissa.Frontend.Services;
using Xavissa.Frontend.Services.Auth;
using Xavissa.Frontend.ViewModels;

namespace Xavissa.Frontend.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<ILoginCoordinator, LoginCoordinator>();
        services.AddScoped<IAuthRepositoryOnline, AuthRepositoryOnline>();
        services.AddScoped<ILocalIdentityService, LocalIdentityService>();
        services.AddScoped<IUserRepositoryOnline, UserRepositoryOnline>();
        services.AddScoped<IUserRepositoryOffline, UserRepositoryOffline>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<LoginViewModel>();
        return services;
    }
}
