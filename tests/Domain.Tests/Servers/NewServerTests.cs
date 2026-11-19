using Domain.Servers;
using Xunit;

namespace Domain.Tests.Servers;

/// <summary>A new server starts in a sensible state — API-SRV-01.</summary>
public sealed class NewServerTests
{
    private static readonly Guid Owner = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 30, 0, TimeSpan.Zero);

    // ---- The name is trimmed; an empty name or one over 100 characters is refused. ----

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void The_name_is_trimmed()
    {
        // Given
        const string name = "   Book club  ";

        // When
        var server = Server.Create(Owner, name, iconUrl: null, Now);

        // Then
        Assert.Equal("Book club", server.Name);
        Assert.Equal("Book club", Server.NormalizeName(name));
    }

    [Theory]
    [Trait("Story", "API-SRV-01")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t ")]
    public void An_empty_name_is_refused(string name)
    {
        // Given / When
        var valid = Server.IsValidName(Server.NormalizeName(name));

        // Then
        Assert.False(valid);
        Assert.Throws<ArgumentException>(() => Server.Create(Owner, name, iconUrl: null, Now));
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_name_of_exactly_100_characters_is_allowed()
    {
        // Given: 100 characters once trimmed
        var name = "  " + new string('n', 100) + "  ";

        // When
        var server = Server.Create(Owner, name, iconUrl: null, Now);

        // Then
        Assert.True(Server.IsValidName(Server.NormalizeName(name)));
        Assert.Equal(100, server.Name.Length);
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_name_over_100_characters_is_refused()
    {
        // Given
        var name = new string('n', 101);

        // When
        var valid = Server.IsValidName(Server.NormalizeName(name));

        // Then
        Assert.False(valid);
        Assert.Throws<ArgumentException>(() => Server.Create(Owner, name, iconUrl: null, Now));
    }

    // ---- A new server has its owner as the only member, an @everyone role ..., and a general text channel. ----

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_new_server_belongs_to_its_owner()
    {
        // When
        var server = Server.Create(Owner, "Book club", "https://example.test/icon.png", Now);

        // Then
        Assert.Equal(Owner, server.OwnerId);
        Assert.Equal("https://example.test/icon.png", server.IconUrl);
        Assert.Equal(7, server.Id.Version);
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_new_server_has_its_owner_as_the_only_member()
    {
        // When
        var server = Server.Create(Owner, "Book club", iconUrl: null, Now);

        // Then
        var member = Assert.Single(server.Members);
        Assert.Equal(Owner, member.UserId);
        Assert.Equal(server.Id, member.ServerId);
        Assert.Equal(Now, member.JoinedAt);
        Assert.Null(member.Nickname);
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_new_server_has_an_everyone_role_that_can_view_channels_send_messages_and_create_invites()
    {
        // When
        var server = Server.Create(Owner, "Book club", iconUrl: null, Now);

        // Then
        var everyone = Assert.Single(server.Roles);
        Assert.Equal("@everyone", everyone.Name);
        Assert.True(everyone.IsEveryone);
        Assert.Equal(0, everyone.Position);
        Assert.Equal(Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite, everyone.Permissions);
        Assert.Null(everyone.Color);
        Assert.Equal(server.Id, everyone.ServerId);
        Assert.Equal(7, everyone.Id.Version);
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void A_new_server_has_a_general_text_channel()
    {
        // When
        var server = Server.Create(Owner, "Book club", iconUrl: null, Now);

        // Then
        var general = Assert.Single(server.Channels);
        Assert.Equal("general", general.Name);
        Assert.Equal(ChannelType.Text, general.Type);
        Assert.Equal(0, general.Position);
        Assert.Null(general.Topic);
        Assert.Equal(server.Id, general.ServerId);
        Assert.Equal(7, general.Id.Version);
    }

    [Fact]
    [Trait("Story", "API-SRV-01")]
    public void Every_id_of_a_new_server_is_distinct()
    {
        // When
        var server = Server.Create(Owner, "Book club", iconUrl: null, Now);

        // Then
        Guid[] ids = [server.Id, server.Roles.Single().Id, server.Channels.Single().Id];
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }
}
