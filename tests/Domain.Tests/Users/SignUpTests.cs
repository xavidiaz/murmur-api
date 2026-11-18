using Domain.Users;
using Xunit;

namespace Domain.Tests.Users;

/// <summary>Sign up — API-AUTH-01: the profile rules that apply to a new account.</summary>
public sealed class SignUpTests
{
    [Theory]
    [Trait("Story", "API-AUTH-01")]
    [InlineData("Alice_X", "alice_x")]
    [InlineData("alice_x", "alice_x")]
    [InlineData("BOB.99", "bob.99")]
    public void My_username_is_stored_in_lowercase(string chosen, string stored)
    {
        // Given / When
        var userName = UserProfile.NormalizeUserName(chosen);

        // Then
        Assert.Equal(stored, userName);
    }

    [Theory]
    [Trait("Story", "API-AUTH-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void If_I_dont_pick_a_display_name_my_username_is_used(string? displayName)
    {
        // Given
        const string userName = "alice_x";

        // When
        var resolved = UserProfile.ResolveDisplayName(displayName, userName);

        // Then
        Assert.Equal(userName, resolved);
    }

    [Fact]
    [Trait("Story", "API-AUTH-01")]
    public void A_display_name_I_pick_is_kept_without_surrounding_spaces()
    {
        // Given
        const string displayName = "  Alice Example ";

        // When
        var resolved = UserProfile.ResolveDisplayName(displayName, "alice_x");

        // Then
        Assert.Equal("Alice Example", resolved);
    }
}
