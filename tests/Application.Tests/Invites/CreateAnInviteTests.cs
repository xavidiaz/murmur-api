using Application.Common;
using Application.Invites;
using Application.Servers;
using Application.Tests.Servers;
using Contracts.Invites;
using Contracts.Servers;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using DomainInvite = Domain.Invites.Invite;
using DomainPermissions = Domain.Servers.Permissions;

namespace Application.Tests.Invites;

internal sealed class FakeInviteRepository : IInviteRepository
{
    public List<DomainInvite> Saved { get; } = [];
    public List<string> Attempts { get; } = [];

    /// <summary>How many of the next adds report a code collision.</summary>
    public int CollisionsToReport { get; set; }

    public Task<bool> TryAddAsync(DomainInvite invite, CancellationToken ct)
    {
        Attempts.Add(invite.Code);
        if (CollisionsToReport > 0)
        {
            CollisionsToReport--;
            return Task.FromResult(false);
        }
        Saved.Add(invite);
        return Task.FromResult(true);
    }

    /// <summary>Previews served by <see cref="FindPreviewAsync"/>, keyed by code.</summary>
    public Dictionary<string, InvitePreview> Previews { get; } = new(StringComparer.Ordinal);
    public List<string> PreviewLookups { get; } = [];

    public Task<InvitePreview?> FindPreviewAsync(string code, CancellationToken ct)
    {
        PreviewLookups.Add(code);
        return Task.FromResult(Previews.GetValueOrDefault(code));
    }
}

/// <summary>Create an invite — API-INV-02: the use case, with repositories faked.</summary>
public sealed class CreateAnInviteTests
{
    private readonly FakeServerRepository _servers = new();
    private readonly FakeInviteRepository _invites = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly Guid _owner = Guid.CreateVersion7();
    private CreateInvite UseCase => new(_servers, _invites, _clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ServerDetailDto> ServerAsync() =>
        (await new CreateServer(_servers, _clock).HandleAsync(_owner, new CreateServerRequest("My Place", "https://example.test/i.png"), Ct)).Value;

    private async Task<Result<InviteDto>> CreateAsync(Guid serverId, int? maxUses = null, int? expiresInSeconds = null, Guid? as_ = null) =>
        await UseCase.HandleAsync(as_ ?? _owner, serverId, new CreateInviteRequest(maxUses, expiresInSeconds), Ct);

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task By_default_an_invite_lasts_24_hours_and_can_be_used_any_number_of_times()
    {
        // Given
        var server = await ServerAsync();

        // When
        var invite = (await CreateAsync(server.Id)).Value;

        // Then
        Assert.Equal(8, invite.Code.Length);
        Assert.Equal((server.Id, "My Place", "https://example.test/i.png", _owner), (invite.ServerId, invite.ServerName, invite.ServerIconUrl, invite.CreatorId));
        Assert.Equal(_clock.GetUtcNow().AddHours(24), invite.ExpiresAt);
        Assert.Equal(((int?)null, 0, 1), (invite.MaxUses, invite.Uses, invite.MemberCount));
        Assert.Equal(invite.Code, Assert.Single(_invites.Saved).Code);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task I_can_make_an_invite_that_never_expires()
    {
        var server = await ServerAsync();

        var invite = (await CreateAsync(server.Id, expiresInSeconds: 0)).Value;

        Assert.Null(invite.ExpiresAt);
        Assert.Null(_invites.Saved.Single().ExpiresAt);
    }

    [Theory]
    [Trait("Story", "API-INV-02")]
    [InlineData(60)]
    [InlineData(61)]
    [InlineData(604_799)]
    [InlineData(604_800)]
    public async Task An_expiry_from_60_seconds_to_7_days_is_counted_from_now(int seconds)
    {
        var server = await ServerAsync();

        var invite = (await CreateAsync(server.Id, expiresInSeconds: seconds)).Value;

        Assert.Equal(_clock.GetUtcNow().AddSeconds(seconds), invite.ExpiresAt);
    }

    [Theory]
    [Trait("Story", "API-INV-02")]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task A_use_limit_from_1_to_1000_is_kept(int maxUses)
    {
        var server = await ServerAsync();

        var invite = (await CreateAsync(server.Id, maxUses: maxUses)).Value;

        Assert.Equal(maxUses, invite.MaxUses);
    }

    [Theory]
    [Trait("Story", "API-INV-02")]
    [InlineData("expiresInSeconds", null, -1)]
    [InlineData("expiresInSeconds", null, 1)]
    [InlineData("expiresInSeconds", null, 59)]
    [InlineData("expiresInSeconds", null, 604_801)]
    [InlineData("expiresInSeconds", null, int.MaxValue)]
    [InlineData("maxUses", 0, null)]
    [InlineData("maxUses", -1, null)]
    [InlineData("maxUses", 1001, null)]
    public async Task Silly_settings_are_rejected_on_their_own(string field, int? maxUses, int? expiresInSeconds)
    {
        var server = await ServerAsync();

        var result = await CreateAsync(server.Id, maxUses, expiresInSeconds);

        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal([field], result.Error.Errors!.Keys);
        Assert.Empty(_invites.Attempts);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Both_silly_settings_are_reported_together()
    {
        var server = await ServerAsync();

        var result = await CreateAsync(server.Id, maxUses: 0, expiresInSeconds: 30);

        Assert.Equal(["maxUses", "expiresInSeconds"], result.Error.Errors!.Keys);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Someone_outside_the_server_gets_the_same_answer_as_for_a_server_that_doesnt_exist()
    {
        var server = await ServerAsync();

        var outsider = await CreateAsync(server.Id, as_: Guid.CreateVersion7());
        var missing = await CreateAsync(Guid.CreateVersion7());

        Assert.Equal(ServerErrors.NotFound, outsider.Error);
        Assert.Equal(ServerErrors.NotFound, missing.Error);
        Assert.Empty(_invites.Attempts);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task A_member_who_may_not_create_invites_is_refused()
    {
        // Given: a member whose @everyone role lacks CreateInvite
        var server = await ServerAsync();
        var member = Guid.CreateVersion7();
        _servers.AccessOverrides[(server.Id, member)] = new ServerAccess(server.Id, server.Name, server.IconUrl, _owner,
            DomainPermissions.ViewChannel | DomainPermissions.SendMessages, MemberCount: 2);

        // When
        var result = await CreateAsync(server.Id, as_: member);

        // Then
        Assert.Equal(ServerErrors.Forbidden, result.Error);
        Assert.Empty(_invites.Attempts);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task A_code_collision_is_retried_with_a_new_code()
    {
        // Given
        var server = await ServerAsync();
        _invites.CollisionsToReport = 2;

        // When
        var invite = (await CreateAsync(server.Id)).Value;

        // Then
        Assert.Equal(3, _invites.Attempts.Count);
        Assert.Equal(3, _invites.Attempts.Distinct().Count());
        Assert.Equal(_invites.Attempts[^1], invite.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-02")]
    public async Task Endless_code_collisions_give_up_after_a_bounded_number_of_tries()
    {
        var server = await ServerAsync();
        _invites.CollisionsToReport = int.MaxValue;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateAsync(server.Id));

        Assert.Equal(CreateInvite.MaxCodeAttempts, _invites.Attempts.Count);
    }
}
