using Application.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SignUp>();
        services.AddScoped<GetCurrentUser>();
        services.AddScoped<SignIn>();
        services.AddScoped<RefreshSession>();
        return services;
    }
}
