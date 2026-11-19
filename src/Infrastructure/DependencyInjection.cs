using Application.Auth;
using Application.Servers;
using Infrastructure.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    /// <summary>
    /// Registers persistence, Identity and the token service. Configuration is read lazily from the final
    /// <see cref="IConfiguration"/> in DI, so test hosts can override any setting.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            options.UseNpgsql(connectionString);
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789_.";
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
            })
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinimumKeyBytes,
                $"Jwt:SigningKey must be at least {JwtOptions.MinimumKeyBytes} bytes.")
            .ValidateOnStart();

        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddScoped<ITokenIssuer, TokenService>();
        services.AddScoped<IServerRepository, ServerRepository>();
        return services;
    }

    /// <summary>Applies pending migrations. EF Core takes a database lock, so concurrent hosts are safe.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}
