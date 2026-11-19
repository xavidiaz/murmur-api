using Api.Auth;
using Api.Errors;
using Application.Servers;
using Contracts;
using Contracts.Servers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

internal static class ServerEndpoints
{
    public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder app)
    {
        var servers = app.MapGroup("").WithTags("Servers"); // signed-in only (the fallback policy)

        servers.MapPost(ApiRoutes.Servers.Collection, CreateAsync);
        servers.MapGet(ApiRoutes.Servers.Collection, ListMineAsync);
        servers.MapGet(ApiRoutes.Servers.Collection + "/{id:guid}", GetAsync);

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<ServerDto>>, UnauthorizedHttpResult>> ListMineAsync(
        HttpContext http, GetMyServers getMyServers, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();
        return TypedResults.Ok(await getMyServers.HandleAsync(userId, ct));
    }

    private static async Task<Results<Ok<ServerDetailDto>, ProblemHttpResult, UnauthorizedHttpResult>> GetAsync(
        Guid id, HttpContext http, GetServer getServer, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        var result = await getServer.HandleAsync(userId, id, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Created<ServerDetailDto>, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        CreateServerRequest request, HttpContext http, CreateServer createServer, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();

        var result = await createServer.HandleAsync(userId, request, ct);
        return result.IsSuccess
            ? TypedResults.Created(ApiRoutes.Servers.Item(result.Value.Id), result.Value)
            : result.Error.ToProblem();
    }
}
