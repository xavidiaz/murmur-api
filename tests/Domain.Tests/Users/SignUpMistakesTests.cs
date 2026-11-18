using Domain.Users;
using Xunit;

namespace Domain.Tests.Users;

/// <summary>Sign-up mistakes are explained — API-AUTH-02: which usernames and display names are acceptable.</summary>
public sealed class SignUpMistakesTests
{
    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("ab")]
    [InlineData("alice_x")]
    [InlineData("bob.99")]
    [InlineData("abcdefghijklmnopqrstuvwxyz012345")] // 32
    public void A_well_formed_username_is_accepted(string userName) =>
        Assert.True(UserProfile.IsValidUserName(userName));

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456")] // 33
    [InlineData("u-dash")]
    [InlineData("u space")]
    [InlineData("Alice")] // not normalized yet
    [InlineData("ünï")]
    public void A_badly_formed_username_is_rejected(string userName) =>
        Assert.False(UserProfile.IsValidUserName(userName));

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("A")]
    [InlineData("Alice Example")]
    [InlineData("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD")] // 32
    public void A_display_name_of_1_to_32_characters_is_accepted(string displayName) =>
        Assert.True(UserProfile.IsValidDisplayName(displayName));

    [Theory]
    [Trait("Story", "API-AUTH-02")]
    [InlineData("")]
    [InlineData("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD")] // 33
    public void A_display_name_longer_than_32_characters_is_rejected(string displayName) =>
        Assert.False(UserProfile.IsValidDisplayName(displayName));

    [Fact]
    [Trait("Story", "API-AUTH-02")]
    public void A_display_name_is_measured_after_trimming()
    {
        // Given: 32 characters plus surrounding spaces
        var displayName = "  " + new string('D', 32) + "  ";

        // When
        var resolved = UserProfile.ResolveDisplayName(displayName, "alice_x");

        // Then
        Assert.True(UserProfile.IsValidDisplayName(resolved));
    }
}
