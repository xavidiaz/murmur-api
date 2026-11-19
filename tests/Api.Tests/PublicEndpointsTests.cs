using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>Hosts the API without a database: nothing here gets past authorization.</summary>
public sealed class NoDatabaseApi : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Host=unused.invalid;Database=unused");
        builder.UseSetting("MIGRATE_ON_START", "false");
        builder.UseSetting("Jwt:SigningKey", new string('k', 64));
    }
}

/// <summary>
/// Signing in is required by default; only a deliberate, short list of endpoints is public. A new public endpoint
/// must be added to <see cref="Public"/> on purpose (Join with an invite — API-INV-04, security hardening).
/// </summary>
public sealed partial class PublicEndpointsTests(NoDatabaseApi api) : IClassFixture<NoDatabaseApi>
{
    private static readonly string[] Public =
    [
        "POST /api/v1/auth/register",
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/refresh",
        "GET /api/v1/invites/{code}",
    ];

    private IEnumerable<(string Method, RouteEndpoint Endpoint)> Endpoints =>
        api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"]).Select(m => (m, e)));

    private static string Describe(string method, RouteEndpoint endpoint) => $"{method} /{endpoint.RoutePattern.RawText!.TrimStart('/')}";

    [Fact]
    [Trait("Story", "API-INV-04")]
    public void Only_register_sign_in_refresh_and_invite_preview_are_public()
    {
        var anonymous = Endpoints
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(x => Describe(x.Method, x.Endpoint))
            .Order()
            .ToArray();

        Assert.Equal(Public.Order(), anonymous);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Every_other_endpoint_falls_back_to_requiring_a_signed_in_user()
    {
        var fallback = await api.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Every_other_endpoint_turns_away_a_request_without_a_token()
    {
        // Given
        using var client = api.CreateClient();
        var protectedEndpoints = Endpoints
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();
        Assert.NotEmpty(protectedEndpoints);

        foreach (var (method, endpoint) in protectedEndpoints)
        {
            // When: the route's parameters filled with plausible values, no Authorization header
            var path = Parameter().Replace(endpoint.RoutePattern.RawText!,
                m => m.Groups["constraint"].Value == "guid" ? Guid.NewGuid().ToString() : "Zz000000");
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // Then
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{Describe(method, endpoint)} answered {(int)response.StatusCode} without a token");
        }
    }

    [GeneratedRegex(@"\{(?<name>[^}:?]+)(?::(?<constraint>[^}?]+))?\??\}")]
    private static partial Regex Parameter();
}
