using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Servers;

/// <summary>Create a server — API-SRV-02: the aggregate round-trips through EF Core on a real Postgres.</summary>
public sealed class CreateAServerTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Story", "API-SRV-02")]
    public async Task A_saved_server_loads_back_with_its_owner_everyone_role_and_general_channel()
    {
        // Given
        var owner = Guid.CreateVersion7();
        await using (var db = database.CreateContext())
        {
            var name = "u_" + Guid.NewGuid().ToString("N")[..12];
            db.Users.Add(new ApplicationUser { Id = owner, UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name });
            await db.SaveChangesAsync(Ct);
        }
        var joinedAt = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        var created = Server.Create(owner, "  My Place ", "https://example.test/icon.png", joinedAt);

        // When
        await using (var db = database.CreateContext())
            await new ServerRepository(db).AddAsync(created, Ct);

        // Then
        await using var check = database.CreateContext();
        var loaded = await check.Servers
            .Include(s => s.Members).Include(s => s.Roles).Include(s => s.Channels)
            .SingleAsync(s => s.Id == created.Id, Ct);
        Assert.Equal("My Place", loaded.Name);
        Assert.Equal("https://example.test/icon.png", loaded.IconUrl);
        Assert.Equal(owner, loaded.OwnerId);
        var member = Assert.Single(loaded.Members);
        Assert.Equal((owner, joinedAt), (member.UserId, member.JoinedAt));
        var everyone = Assert.Single(loaded.Roles);
        Assert.Equal((created.Roles.Single().Id, true, 0, Role.EveryoneDefaultPermissions),
            (everyone.Id, everyone.IsEveryone, everyone.Position, everyone.Permissions));
        var general = Assert.Single(loaded.Channels);
        Assert.Equal((created.Channels.Single().Id, "general", ChannelType.Text), (general.Id, general.Name, general.Type));
    }
}
