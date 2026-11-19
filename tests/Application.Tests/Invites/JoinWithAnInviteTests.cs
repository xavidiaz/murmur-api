using Application.Invites;
using Contracts;
using Contracts.Servers;
using Domain.Invites;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Application.Tests.Invites;

/// <summary>Join with an invite — API-INV-04: the use case, with the repository faked.</summary>
public sealed class JoinWithAnInviteTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeInviteRepository _invites = new();
    private readonly FakeTimeProvider _clock = new(Created.AddMinutes(1));
    private readonly Guid _owner = Guid.CreateVersion7();
    private readonly Guid _serverId = Guid.CreateVersion7();
    private readonly Guid _me = Guid.CreateVersion7();
    private AcceptInvite UseCase => new(_invites, _clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ServerDto Server => new(_serverId, "My Place", "https://example.test/i.png", _owner);

    private Invite Stored(TimeSpan? lifetime = null, int? maxUses = null, params bool[] isMemberOnEachRead)
    {
        var invite = Invite.Create(_serverId, _owner, Created, lifetime is { } l ? Created + l : null, maxUses);
        var reads = isMemberOnEachRead.Length == 0 ? [false] : isMemberOnEachRead;
        _invites.JoinTargets[invite.Code] = new Queue<JoinTarget>(reads.Select(member => new JoinTarget(invite, Server, member)));
        return invite;
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_an_invite_adds_me_to_the_server()
    {
        // Given
        var invite = Stored(TimeSpan.FromHours(1));

        // When
        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        // Then
        Assert.Equal(Server, result.Value);
        var redeem = Assert.Single(_invites.Redeems);
        Assert.Equal((invite.Code, _serverId, _me, _clock.GetUtcNow(), _clock.GetUtcNow()),
            (redeem.Code, redeem.Member.ServerId, redeem.Member.UserId, redeem.Member.JoinedAt, redeem.Now));
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_again_when_Im_already_in_changes_nothing()
    {
        var invite = Stored(isMemberOnEachRead: true);

        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        Assert.Equal(Server, result.Value);
        Assert.Empty(_invites.Redeems); // no use counted
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_an_invite_to_my_own_server_changes_nothing()
    {
        var invite = Stored(isMemberOnEachRead: true); // the owner is a member

        var result = await UseCase.HandleAsync(_owner, invite.Code, Ct);

        Assert.Equal(Server, result.Value);
        Assert.Empty(_invites.Redeems);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task An_expired_invite_cant_be_accepted()
    {
        // Given: the conditional update refuses, and the re-read shows why
        var invite = Stored(TimeSpan.FromSeconds(60));
        _clock.Advance(TimeSpan.FromMinutes(5));
        _invites.NextRedeemOutcome = RedeemOutcome.NotUsable;

        // When
        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        // Then
        Assert.Equal(InviteErrors.Expired, result.Error);
        Assert.Equal(ErrorCodes.InviteExpired, result.Error.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task A_used_up_invite_cant_be_accepted()
    {
        var invite = Stored(maxUses: 1);
        invite.Redeem(Created);
        _invites.NextRedeemOutcome = RedeemOutcome.NotUsable;

        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        Assert.Equal(InviteErrors.Exhausted, result.Error);
        Assert.Equal(ErrorCodes.InviteExhausted, result.Error.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Losing_a_race_with_my_own_other_accept_still_lets_me_in()
    {
        // Given: my other request joined first (member PK clash rolled my use back)
        var invite = Stored();
        _invites.NextRedeemOutcome = RedeemOutcome.AlreadyMember;

        // When
        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        // Then
        Assert.Equal(Server, result.Value);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Losing_the_last_use_to_my_own_other_accept_still_lets_me_in()
    {
        // Given: single-use invite; my other request used it, so my update found nothing left, but I'm in now
        var invite = Stored(maxUses: 1, isMemberOnEachRead: [false, true]);
        _invites.NextRedeemOutcome = RedeemOutcome.NotUsable;

        // When
        var result = await UseCase.HandleAsync(_me, invite.Code, Ct);

        // Then
        Assert.Equal(Server, result.Value);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task An_unknown_invite_cant_be_accepted()
    {
        var result = await UseCase.HandleAsync(_me, "Zz000000", Ct);

        Assert.Equal(InviteErrors.NotFound, result.Error);
        Assert.Empty(_invites.Redeems);
    }

    [Theory]
    [Trait("Story", "API-INV-04")]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("abcd-fgh")]
    public async Task A_malformed_code_is_not_found_without_a_lookup(string code)
    {
        var result = await UseCase.HandleAsync(_me, code, Ct);

        Assert.Equal(InviteErrors.NotFound, result.Error);
        Assert.Empty(_invites.JoinLookups);
    }
}
