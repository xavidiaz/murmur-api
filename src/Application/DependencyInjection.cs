using Application.Auth;
using Application.Invites;
using Application.Servers;
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
        services.AddScoped<SignOut>();
        services.AddScoped<CreateServer>();
        services.AddScoped<GetMyServers>();
        services.AddScoped<GetServer>();
        services.AddScoped<CreateInvite>();
        return services;
    }
}
