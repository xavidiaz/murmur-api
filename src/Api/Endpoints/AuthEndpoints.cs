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

        // Public (sign-in is required by default): you can't have a token before you register or sign in.
        auth.MapPost(ApiRoutes.Auth.Register, RegisterAsync).AllowAnonymous();
        auth.MapPost(ApiRoutes.Auth.Login, LoginAsync).AllowAnonymous();
        // Anonymous on purpose: the app refreshes once its access token has expired.
        auth.MapPost(ApiRoutes.Auth.Refresh, RefreshAsync).AllowAnonymous();
        auth.MapPost(ApiRoutes.Auth.Logout, LogoutAsync);
        auth.MapGet(ApiRoutes.Auth.Me, MeAsync);

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

    /// <summary>Always 204 for a signed-in caller: it never reveals whether the token existed or whose it was.</summary>
    private static async Task<Results<NoContent, UnauthorizedHttpResult>> LogoutAsync(
        LogoutRequest request, HttpContext http, SignOut signOut, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        await signOut.HandleAsync(userId, request, ct);
        return TypedResults.NoContent();
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
