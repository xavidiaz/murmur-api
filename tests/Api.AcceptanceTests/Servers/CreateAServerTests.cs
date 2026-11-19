namespace Api.AcceptanceTests.Servers;

/// <summary>Create a server — API-SRV-02. As a user, I want to create a server so that I can host a community.</summary>
public sealed class CreateAServerTests(PostgresFixture db) : AcceptanceTest(db)
{
    private static readonly Permissions AllPermissions =
        Enum.GetValues<Permissions>().Aggregate(Permissions.None, (all, p) => all | p);

    private const Permissions EveryoneDefaults =
        Permissions.ViewChannel | Permissions.SendMessages | Permissions.CreateInvite;

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task Creating_a_server_makes_me_its_owner_and_gives_me_a_general_channel_and_an_everyone_role()
    {
        // Given
        using var me = await NewUserAsync();

        // When
        var response = await me.PostAsync(ApiRoutes.Servers.Collection, new CreateServerRequest("  My Place ", null));

        // Then
        var server = await ApiClient.ReadAsync<ServerDetailDto>(response, HttpStatusCode.Created);
        Assert.NotEqual(Guid.Empty, server.Id);
        Assert.Equal("My Place", server.Name);
        Assert.Null(server.IconUrl);
        Assert.Equal(me.Id, server.OwnerId);

        var channel = Assert.Single(server.Channels);
        Assert.Equal("general", channel.Name);
        Assert.Equal(ChannelType.Text, channel.Type);
        Assert.Equal(0, channel.Position);
        Assert.Equal(server.Id, channel.ServerId);

        var everyone = Assert.Single(server.Roles);
        Assert.Equal("@everyone", everyone.Name);
        Assert.True(everyone.IsEveryone);
        Assert.Equal(0, everyone.Position);
        Assert.Equal(EveryoneDefaults, everyone.Permissions);
        Assert.Equal(server.Id, everyone.ServerId);

        Assert.Equal(AllPermissions, server.MyPermissions);
    }

    [Theory]
    [Trait("Story", "API-SRV-02")]
    [InlineData("empty")]
    [InlineData("only spaces")]
    [InlineData("101 characters")]
    public async Task A_blank_name_or_one_over_100_characters_is_rejected(string kind)
    {
        // Given
        using var me = await NewUserAsync();
        var name = kind switch
        {
            "empty" => "",
            "only spaces" => "   ",
            "101 characters" => new string('n', 101),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        // When
        var response = await me.PostAsync(ApiRoutes.Servers.Collection, new CreateServerRequest(name, null));

        // Then
        var problem = await response.AssertValidationProblemAsync("name");
        var keys = problem.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.True(keys.SequenceEqual(["name"]), $"{kind}: expected errors only for 'name', got [{string.Join(", ", keys)}]");
    }

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task A_name_of_exactly_100_characters_is_accepted()
    {
        // Given
        using var me = await NewUserAsync();
        var name = new string('n', 100);

        // When
        var response = await me.PostAsync(ApiRoutes.Servers.Collection, new CreateServerRequest(name, null));

        // Then
        var server = await ApiClient.ReadAsync<ServerDetailDto>(response, HttpStatusCode.Created);
        Assert.Equal(name, server.Name);
    }

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task Creating_a_server_requires_signing_in()
    {
        // Given
        using var visitor = Anonymous();

        // When
        var response = await visitor.PostAsync(ApiRoutes.Servers.Collection, new CreateServerRequest(Unique.Name("server"), null));

        // Then
        await ApiClient.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }
}
