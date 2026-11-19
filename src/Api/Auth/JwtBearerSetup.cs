using Contracts;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Auth;

internal static class JwtBearerSetup
{
    /// <summary>
    /// JwtBearer validated against <see cref="JwtOptions"/> and the DI <see cref="TimeProvider"/> (resolved when the
    /// options are built, so a test clock registered later still wins), with zero clock skew. The token is read from
    /// the Authorization header, or from <c>access_token</c> in the query string for the SignalR hub.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((bearer, jwtOptions, clock) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        var now = clock.GetUtcNow().UtcDateTime;
                        return expires is { } exp && now < exp && (notBefore is not { } nbf || nbf <= now);
                    },
                    NameClaimType = "unique_name",
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token)
                            && context.HttpContext.Request.Path.StartsWithSegments(ApiRoutes.Hub)
                            && context.Request.Query["access_token"].ToString() is { Length: > 0 } token)
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        // Secure by default: every endpoint requires a signed-in user unless it is explicitly .AllowAnonymous().
        // The public endpoints are pinned by a test (Api.Tests › PublicEndpointsTests).
        services.AddAuthorization(options => options.FallbackPolicy =
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }
}
