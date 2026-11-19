using Domain.Invites;
using Xunit;

namespace Domain.Tests.Invites;

/// <summary>Invite rules — API-INV-01: when an invite is still usable.</summary>
public sealed class InviteRulesTests
{
    private static readonly Guid ServerId = Guid.CreateVersion7();
    private static readonly Guid CreatorId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static Invite NewInvite(TimeSpan? lifetime = null, int? maxUses = null) =>
        Invite.Create(ServerId, CreatorId, Now, lifetime is { } l ? Now + l : null, maxUses);

    // ---- Using an invite counts one use. ----

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void A_new_invite_has_not_been_used()
    {
        // When
        var invite = Invite.Create(ServerId, CreatorId, Now, Now.AddHours(24), maxUses: 5);

        // Then
        Assert.Equal(0, invite.Uses);
        Assert.Equal((ServerId, CreatorId, Now, Now.AddHours(24), (int?)5),
            (invite.ServerId, invite.CreatorId, invite.CreatedAt, invite.ExpiresAt, invite.MaxUses));
        Assert.Equal(InviteUsability.Usable, invite.CanBeUsed(Now));
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void Using_an_invite_counts_one_use()
    {
        // Given
        var invite = NewInvite(TimeSpan.FromHours(1), maxUses: 10);

        // When
        var first = invite.Redeem(Now);
        var second = invite.Redeem(Now.AddMinutes(1));
        var third = invite.Redeem(Now.AddMinutes(2));

        // Then
        Assert.Equal([InviteUsability.Usable, InviteUsability.Usable, InviteUsability.Usable], [first, second, third]);
        Assert.Equal(3, invite.Uses);
    }

    // ---- An expired invite can't be used, and neither can one that has used up its uses. ----

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_can_still_be_used_one_tick_before_it_expires()
    {
        // Given
        var invite = NewInvite(TimeSpan.FromHours(1));

        // When
        var result = invite.Redeem(invite.ExpiresAt!.Value.AddTicks(-1));

        // Then
        Assert.Equal(InviteUsability.Usable, result);
        Assert.Equal(1, invite.Uses);
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_cannot_be_used_from_the_moment_it_expires()
    {
        // Given
        var invite = NewInvite(TimeSpan.FromHours(1));

        // When
        var atExpiry = invite.Redeem(invite.ExpiresAt!.Value);
        var later = invite.Redeem(invite.ExpiresAt!.Value.AddDays(1));

        // Then
        Assert.Equal(InviteUsability.Expired, atExpiry);
        Assert.Equal(InviteUsability.Expired, later);
        Assert.Equal(0, invite.Uses); // a refused redeem changes nothing
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_without_an_expiry_never_expires()
    {
        // Given
        var invite = NewInvite(lifetime: null);

        // When
        var result = invite.Redeem(Now.AddYears(100));

        // Then
        Assert.Null(invite.ExpiresAt);
        Assert.Equal(InviteUsability.Usable, result);
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_that_has_used_up_its_uses_cannot_be_used()
    {
        // Given
        var invite = NewInvite(maxUses: 2);
        invite.Redeem(Now);
        invite.Redeem(Now);

        // When
        var result = invite.Redeem(Now);

        // Then
        Assert.Equal(InviteUsability.Exhausted, result);
        Assert.Equal(InviteUsability.Exhausted, invite.CanBeUsed(Now));
        Assert.Equal(2, invite.Uses); // a refused redeem changes nothing
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void A_single_use_invite_can_be_used_exactly_once()
    {
        // Given
        var invite = NewInvite(maxUses: 1);

        // When / Then
        Assert.Equal(InviteUsability.Usable, invite.Redeem(Now));
        Assert.Equal(InviteUsability.Exhausted, invite.Redeem(Now));
        Assert.Equal(1, invite.Uses);
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_without_a_use_limit_can_be_used_any_number_of_times()
    {
        // Given
        var invite = NewInvite(maxUses: null);

        // When
        for (var i = 0; i < 2000; i++)
            Assert.Equal(InviteUsability.Usable, invite.Redeem(Now));

        // Then
        Assert.Equal(2000, invite.Uses);
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void An_invite_that_is_both_expired_and_used_up_reports_expired()
    {
        // Given
        var invite = NewInvite(TimeSpan.FromHours(1), maxUses: 1);
        invite.Redeem(Now);

        // When
        var result = invite.Redeem(invite.ExpiresAt!.Value);

        // Then
        Assert.Equal(InviteUsability.Expired, result);
    }

    // ---- The contract's ranges, enforced by Create. ----

    [Theory]
    [Trait("Story", "API-INV-01")]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(1000)]
    public void A_use_limit_of_1_to_1000_or_none_is_allowed(int? maxUses)
    {
        Assert.True(Invite.IsValidMaxUses(maxUses));
        Assert.Equal(maxUses, NewInvite(maxUses: maxUses).MaxUses);
    }

    [Theory]
    [Trait("Story", "API-INV-01")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void A_use_limit_outside_1_to_1000_is_refused(int maxUses)
    {
        Assert.False(Invite.IsValidMaxUses(maxUses));
        Assert.Throws<ArgumentException>(() => NewInvite(maxUses: maxUses));
    }

    [Theory]
    [Trait("Story", "API-INV-01")]
    [InlineData(60)]
    [InlineData(86_400)]
    [InlineData(604_800)]
    public void An_expiry_60_seconds_to_7_days_away_is_allowed(int seconds)
    {
        var lifetime = TimeSpan.FromSeconds(seconds);
        Assert.True(Invite.IsValidLifetime(lifetime));
        Assert.Equal(Now + lifetime, NewInvite(lifetime).ExpiresAt);
    }

    [Theory]
    [Trait("Story", "API-INV-01")]
    [InlineData(59)]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(604_801)]
    public void An_expiry_outside_60_seconds_to_7_days_is_refused(int seconds)
    {
        var lifetime = TimeSpan.FromSeconds(seconds);
        Assert.False(Invite.IsValidLifetime(lifetime));
        Assert.Throws<ArgumentException>(() => NewInvite(lifetime));
    }

    // ---- Invite codes are 8 letters and digits. ----

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void Invite_codes_are_8_letters_and_digits()
    {
        // When
        var code = NewInvite().Code;

        // Then
        Assert.Equal(8, code.Length);
        Assert.All(code, c => Assert.True(char.IsAsciiLetterOrDigit(c), $"'{c}' is not a letter or digit"));
    }

    [Fact]
    [Trait("Story", "API-INV-01")]
    public void Invite_codes_are_unique_and_use_the_whole_alphabet_evenly()
    {
        // Given
        const int codes = 10_000;
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        // When
        var generated = Enumerable.Range(0, codes).Select(_ => InviteCode.Generate()).ToList();

        // Then
        Assert.Equal(codes, generated.Distinct(StringComparer.Ordinal).Count());
        Assert.All(generated, code => Assert.Equal(8, code.Length));
        var counts = generated.SelectMany(c => c).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        Assert.Empty(counts.Keys.Except(alphabet));
        Assert.Equal(alphabet.Length, counts.Count);
        // 80 000 characters over 62 symbols ≈ 1290 each (σ ≈ 36). ±25 % is > 8σ: only a biased generator fails this.
        var expected = codes * 8.0 / alphabet.Length;
        Assert.All(counts, kv => Assert.InRange(kv.Value, expected * 0.75, expected * 1.25));
    }
}
