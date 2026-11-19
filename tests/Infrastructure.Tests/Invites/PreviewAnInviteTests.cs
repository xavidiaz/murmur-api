using Domain.Invites;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Invites;

/// <summary>Preview an invite — API-INV-03: the preview is one query on a real Postgres.</summary>
public sealed class PreviewAnInviteTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> NewUserAsync()
    {
        await using var db = database.CreateContext();
        var id = Guid.CreateVersion7();
        var name = "u_" + Guid.NewGuid().ToString("N")[..12];
        db.Users.Add(new ApplicationUser { Id = id, UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name });
        await db.SaveChangesAsync(Ct);
        return id;
    }

    [Fact]
    [Trait("Story", "API-INV-03")]
    public async Task Previewing_an_invite_is_one_query_with_the_servers_name_icon_and_member_count()
    {
        // Given: a server with two members and an invite to it
        var owner = await NewUserAsync();
        var member = await NewUserAsync();
        var server = Server.Create(owner, "preview test", "https://example.test/i.png", Now);
        var invite = Invite.Create(server.Id, owner, Now, Now.AddHours(24), maxUses: 10);
        await using (var db = database.CreateContext())
        {
            await new ServerRepository(db).AddAsync(server, Ct);
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO "ServerMembers" ("ServerId", "UserId", "JoinedAt") VALUES ({server.Id}, {member}, {Now})""", Ct);
            await new InviteRepository(db).TryAddAsync(invite, Ct);
        }

        // When
        var commands = new CommandCounter();
        await using var read = database.CreateContext(commands);
        var repository = new InviteRepository(read);
        var preview = await repository.FindPreviewAsync(invite.Code, Ct);
        var queries = commands.Count;
        var unknown = await repository.FindPreviewAsync("Zz000000", Ct);
        var otherCase = await repository.FindPreviewAsync(SwapCase(invite.Code), Ct);

        // Then
        Assert.NotNull(preview);
        Assert.Equal(1, queries);
        Assert.Equal(("preview test", "https://example.test/i.png", 2), (preview.ServerName, preview.ServerIconUrl, preview.MemberCount));
        Assert.Equal((invite.Code, server.Id, owner, Now.AddHours(24), (int?)10, 0),
            (preview.Invite.Code, preview.Invite.ServerId, preview.Invite.CreatorId, preview.Invite.ExpiresAt, preview.Invite.MaxUses, preview.Invite.Uses));
        Assert.Null(unknown);
        if (SwapCase(invite.Code) != invite.Code)
            Assert.Null(otherCase); // codes are case-sensitive (base62)
    }

    private static string SwapCase(string s) =>
        string.Concat(s.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)));
}
