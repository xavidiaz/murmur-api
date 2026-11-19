using Application.Invites;
using Contracts;
using Contracts.Invites;
using Domain.Invites;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Application.Tests.Invites;

/// <summary>Preview an invite — API-INV-03: the use case, with the repository faked.</summary>
public sealed class PreviewAnInviteTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeInviteRepository _invites = new();
    private readonly FakeTimeProvider _clock = new(Created);
    private readonly Guid _creator = Guid.CreateVersion7();
    private readonly Guid _serverId = Guid.CreateVersion7();
    private PreviewInvite UseCase => new(_invites, _clock);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Invite Stored(TimeSpan? lifetime = null, int? maxUses = null)
    {
        var invite = Invite.Create(_serverId, _creator, Created, lifetime is { } l ? Created + l : null, maxUses);
        _invites.Previews[invite.Code] = new InvitePreview(invite, "My Place", "https://example.test/i.png", MemberCount: 3);
        return invite;
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task Anyone_can_preview_an_invite_and_see_the_servers_name_icon_and_member_count()
    {
        // Given
        var invite = Stored(TimeSpan.FromHours(24), maxUses: 5);

        // When
        var result = await UseCase.HandleAsync(invite.Code, Ct);

        // Then
        Assert.Equal(new InviteDto(invite.Code, _serverId, "My Place", "https://example.test/i.png", _creator,
            Created.AddHours(24), 5, 0, 3), result.Value);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_unknown_code_isnt_found()
    {
        var result = await UseCase.HandleAsync("Zz000000", Ct);

        Assert.Equal(InviteErrors.NotFound, result.Error);
    }

    [Theory]
    [Trait("Story", "API-INV-03")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abcdefg")]
    [InlineData("abcdefghi")]
    [InlineData("abcd-fgh")]
    [InlineData("abcd fgh")]
    [InlineData("abcdéfgh")]
    [InlineData("../../ab")]
    public async Task A_malformed_code_gets_the_same_answer_as_an_unknown_one_without_a_lookup(string code)
    {
        var result = await UseCase.HandleAsync(code, Ct);

        Assert.Equal(InviteErrors.NotFound, result.Error);
        Assert.Empty(_invites.PreviewLookups);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_expired_invite_says_so()
    {
        // Given
        var invite = Stored(TimeSpan.FromSeconds(60));
        _clock.Advance(TimeSpan.FromSeconds(60)); // exactly at the expiry instant

        // When
        var result = await UseCase.HandleAsync(invite.Code, Ct);

        // Then
        Assert.Equal(InviteErrors.Expired, result.Error);
        Assert.Equal(ErrorCodes.InviteExpired, result.Error.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_invite_one_tick_before_expiry_can_still_be_previewed()
    {
        var invite = Stored(TimeSpan.FromSeconds(60));
        _clock.Advance(TimeSpan.FromSeconds(60) - TimeSpan.FromTicks(1));

        var result = await UseCase.HandleAsync(invite.Code, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task A_used_up_invite_says_so()
    {
        // Given
        var invite = Stored(maxUses: 1);
        invite.Redeem(Created);

        // When
        var result = await UseCase.HandleAsync(invite.Code, Ct);

        // Then
        Assert.Equal(InviteErrors.Exhausted, result.Error);
        Assert.Equal(ErrorCodes.InviteExhausted, result.Error.Code);
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task An_invite_that_is_both_expired_and_used_up_says_expired()
    {
        var invite = Stored(TimeSpan.FromSeconds(60), maxUses: 1);
        invite.Redeem(Created);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await UseCase.HandleAsync(invite.Code, Ct);

        Assert.Equal(InviteErrors.Expired, result.Error);
    }
}
