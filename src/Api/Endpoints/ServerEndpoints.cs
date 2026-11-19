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
        var servers = app.MapGroup("").WithTags("Servers").RequireAuthorization();

        servers.MapPost(ApiRoutes.Servers.Collection, CreateAsync);

        return app;
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
