using Api.Auth;
using Api.Errors;
using Application.Auth;
using Contracts;
using Contracts.Auth;
using Contracts.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("").WithTags("Auth");

        auth.MapPost(ApiRoutes.Auth.Register, RegisterAsync);
        auth.MapPost(ApiRoutes.Auth.Login, LoginAsync);
        // Anonymous on purpose: the app refreshes once its access token has expired.
        auth.MapPost(ApiRoutes.Auth.Refresh, RefreshAsync).AllowAnonymous();
        auth.MapGet(ApiRoutes.Auth.Me, MeAsync).RequireAuthorization();

        return app;
    }

    private static async Task<Results<Created<AuthResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request, SignUp signUp, CancellationToken ct)
    {
        var result = await signUp.HandleAsync(request, ct);
        return result.IsSuccess
            ? TypedResults.Created(ApiRoutes.Auth.Me, result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, SignIn signIn, CancellationToken ct)
    {
        var result = await signIn.HandleAsync(request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> RefreshAsync(
        RefreshRequest request, RefreshSession refresh, CancellationToken ct)
    {
        var result = await refresh.HandleAsync(request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult>> MeAsync(
        HttpContext http, GetCurrentUser getCurrentUser, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        var user = await getCurrentUser.HandleAsync(userId, ct);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(user);
    }
}
