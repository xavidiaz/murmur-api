using Api.Auth;
using Api.Errors;
using Application.Invites;
using Contracts;
using Contracts.Invites;
using Contracts.Servers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

internal static class InviteEndpoints
{
    public static IEndpointRouteBuilder MapInviteEndpoints(this IEndpointRouteBuilder app)
    {
        var invites = app.MapGroup("").WithTags("Invites");

        invites.MapPost(ApiRoutes.Servers.Collection + "/{serverId:guid}/invites", CreateAsync).RequireAuthorization();
        // Anonymous on purpose: people follow an invite link before they have an account.
        invites.MapGet(ApiRoutes.Base + "/invites/{code}", PreviewAsync).AllowAnonymous();
        invites.MapPost(ApiRoutes.Base + "/invites/{code}/accept", AcceptAsync).RequireAuthorization();

        return app;
    }

    private static async Task<Results<Ok<ServerDto>, ProblemHttpResult, UnauthorizedHttpResult>> AcceptAsync(
        string code, HttpContext http, AcceptInvite acceptInvite, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        var result = await acceptInvite.HandleAsync(userId, code, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<InviteDto>, ProblemHttpResult>> PreviewAsync(
        string code, PreviewInvite previewInvite, CancellationToken ct)
    {
        var result = await previewInvite.HandleAsync(code, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Created<InviteDto>, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        Guid serverId, CreateInviteRequest request, HttpContext http, CreateInvite createInvite, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        var result = await createInvite.HandleAsync(userId, serverId, request, ct);
        return result.IsSuccess
            ? TypedResults.Created(ApiRoutes.Invites.Item(result.Value.Code), result.Value)
            : result.Error.ToProblem();
    }
}
