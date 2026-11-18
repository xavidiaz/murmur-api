using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>
/// Hosts the real API in-memory against the throwaway database. Uses only the agreed seams:
/// <c>ConnectionStrings:Default</c>, <c>MIGRATE_ON_START</c>, <c>Jwt:*</c> and <see cref="TimeProvider"/> in DI.
/// Obtain one via <see cref="PostgresFixture.SharedApi"/> or <see cref="PostgresFixture.CreateIsolatedApi"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "murmur";
    public const string JwtAudience = "murmur";

    private readonly string _connectionString;

    internal ApiFactory(string connectionString)
    {
        _connectionString = connectionString;
        SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        // Starts at real "now" so expiries look sensible. Auto-advance by 1 ms per read keeps every timestamp
        // distinct and strictly increasing, so UUIDv7 message ids created by the API order deterministically.
        Clock = new FakeTimeProvider(DateTimeOffset.UtcNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };
    }

    /// <summary>The API's clock. Only advance it on an isolated factory (<see cref="PostgresFixture.CreateIsolatedApi"/>).</summary>
    public FakeTimeProvider Clock { get; }

    /// <summary>
    /// <c>Jwt:SigningKey</c>: 64 random bytes, Base64-encoded (88 ASCII chars, so ≥ 32 bytes whether the API
    /// Base64-decodes it or takes its UTF-8 bytes).
    /// </summary>
    public string SigningKey { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = _connectionString,
            ["MIGRATE_ON_START"] = "true",
            ["Jwt:SigningKey"] = SigningKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
        };

        // "Testing" avoids Development-only behaviour such as loading user-secrets.
        builder.UseEnvironment("Testing");

        // UseSetting is visible to code that reads configuration before builder.Build();
        // the in-memory source added last also wins over appsettings*.json / env vars.
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
