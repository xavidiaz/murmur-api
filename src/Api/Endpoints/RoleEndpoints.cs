using Api.Auth;
using Api.Errors;
using Application.Roles;
using Contracts;
using Contracts.Servers;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Api.Endpoints;

internal static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var roles = app.MapGroup(ApiRoutes.Servers.Collection + "/{serverId:guid}/roles").WithTags("Roles");

        roles.MapGet("", ListAsync);
        roles.MapPost("", CreateAsync);
        roles.MapPatch("/{roleId:guid}", UpdateAsync);
        roles.MapDelete("/{roleId:guid}", DeleteAsync);

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<RoleDto>>, ProblemHttpResult, UnauthorizedHttpResult>> ListAsync(
        Guid serverId, HttpContext http, ListRoles listRoles, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();
        var result = await listRoles.HandleAsync(userId, serverId, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<Created<RoleDto>, ProblemHttpResult, UnauthorizedHttpResult>> CreateAsync(
        Guid serverId, CreateRoleRequest request, HttpContext http, CreateRole createRole, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();
        var result = await createRole.HandleAsync(userId, serverId, request, ct);
        return result.IsSuccess
            ? TypedResults.Created(ApiRoutes.Servers.Role(serverId, result.Value.Id), result.Value)
            : result.Error.ToProblem();
    }

    private static async Task<Results<Ok<RoleDto>, ProblemHttpResult, UnauthorizedHttpResult>> UpdateAsync(
        Guid serverId, Guid roleId, UpdateRoleRequest request, HttpContext http, UpdateRole updateRole, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();
        var result = await updateRole.HandleAsync(userId, serverId, roleId, request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> DeleteAsync(
        Guid serverId, Guid roleId, HttpContext http, DeleteRole deleteRole, CancellationToken ct)
    {
        if (http.User.UserId() is not { } userId)
            return TypedResults.Unauthorized();
        var result = await deleteRole.HandleAsync(userId, serverId, roleId, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }
}
