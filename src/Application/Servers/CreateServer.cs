using Application.Common;
using Contracts.Servers;
using Server = Domain.Servers.Server;

namespace Application.Servers;

/// <summary>Creates a server owned by the caller, in its starting state (API-SRV-02).</summary>
public sealed class CreateServer(IServerRepository servers, TimeProvider clock)
{
    public async Task<Result<ServerDetailDto>> HandleAsync(Guid userId, CreateServerRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name is null ? null : Server.NormalizeName(request.Name);
        if (name is null || !Server.IsValidName(name))
            errors["name"] = [$"Name must be {Server.NameMinLength}–{Server.NameMaxLength} characters."];
        if (request.IconUrl is not null && !Server.IsValidIconUrl(request.IconUrl))
            errors["iconUrl"] = [$"Icon URL must be an absolute http(s) URL of at most {Server.IconUrlMaxLength} characters."];
        if (errors.Count > 0)
            return AppError.Validation(errors);

        var server = Server.Create(userId, name!, request.IconUrl, clock.GetUtcNow());
        await servers.AddAsync(server, ct);
        return server.ToDetailDto(userId);
    }
}
