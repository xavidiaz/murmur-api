using Api.Auth;
using Api.Errors;
using Application.Roles;
using Application.Servers;
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

        var members = app.MapGroup(ApiRoutes.Servers.Collection + "/{serverId:guid}/members").WithTags("Members");
        members.MapGet("", ListMembersAsync);
        members.MapPut("/{userId:guid}/roles/{roleId:guid}", AssignAsync);
        members.MapDelete("/{userId:guid}/roles/{roleId:guid}", UnassignAsync);

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<MemberDto>>, ProblemHttpResult, UnauthorizedHttpResult>> ListMembersAsync(
        Guid serverId, HttpContext http, ListMembers listMembers, CancellationToken ct)
    {
        if (http.User.UserId() is not { } me)
            return TypedResults.Unauthorized();
        var result = await listMembers.HandleAsync(me, serverId, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> AssignAsync(
        Guid serverId, Guid userId, Guid roleId, HttpContext http, AssignRole assignRole, CancellationToken ct)
    {
        if (http.User.UserId() is not { } me)
            return TypedResults.Unauthorized();
        var result = await assignRole.HandleAsync(me, serverId, userId, roleId, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> UnassignAsync(
        Guid serverId, Guid userId, Guid roleId, HttpContext http, UnassignRole unassignRole, CancellationToken ct)
    {
        if (http.User.UserId() is not { } me)
            return TypedResults.Unauthorized();
        var result = await unassignRole.HandleAsync(me, serverId, userId, roleId, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
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
