using Application.Invites;
using Domain.Invites;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Invites;

/// <summary>Join with an invite — API-INV-04: the transactional redeem on a real Postgres.</summary>
public sealed class JoinWithAnInviteTests(TestDatabase database) : IClassFixture<TestDatabase>
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

    private async Task<(Server Server, Invite Invite)> InviteAsync(TimeSpan? lifetime = null, int? maxUses = null)
    {
        var owner = await NewUserAsync();
        var server = Server.Create(owner, "join test", null, Now);
        var invite = Invite.Create(server.Id, owner, Now, lifetime is { } l ? Now + l : null, maxUses);
        await using var db = database.CreateContext();
        await new ServerRepository(db).AddAsync(server, Ct);
        await new InviteRepository(db).TryAddAsync(invite, Ct);
        return (server, invite);
    }

    private async Task<RedeemOutcome> RedeemAsync(Invite invite, Guid userId, DateTimeOffset at)
    {
        await using var db = database.CreateContext();
        return await new InviteRepository(db).TryRedeemAsync(invite.Code, ServerMember.Join(invite.ServerId, userId, at), at, Ct);
    }

    private async Task<(int Uses, int Members, bool IsMember)> StateAsync(Invite invite, Guid userId)
    {
        await using var db = database.CreateContext();
        var uses = await db.Invites.Where(i => i.Code == invite.Code).Select(i => i.Uses).SingleAsync(Ct);
        var members = await db.Set<ServerMember>().CountAsync(m => m.ServerId == invite.ServerId, Ct);
        var isMember = await db.Set<ServerMember>().AnyAsync(m => m.ServerId == invite.ServerId && m.UserId == userId, Ct);
        return (uses, members, isMember);
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_counts_one_use_and_adds_me()
    {
        var (_, invite) = await InviteAsync(TimeSpan.FromHours(1), maxUses: 5);
        var me = await NewUserAsync();

        var outcome = await RedeemAsync(invite, me, Now.AddMinutes(1));

        Assert.Equal(RedeemOutcome.Joined, outcome);
        Assert.Equal((1, 2, true), await StateAsync(invite, me));
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task An_expired_invite_changes_nothing()
    {
        var (_, invite) = await InviteAsync(TimeSpan.FromSeconds(60));
        var me = await NewUserAsync();

        var outcome = await RedeemAsync(invite, me, Now.AddSeconds(60)); // exactly at expiry

        Assert.Equal(RedeemOutcome.NotUsable, outcome);
        Assert.Equal((0, 1, false), await StateAsync(invite, me));
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task A_used_up_invite_changes_nothing()
    {
        var (_, invite) = await InviteAsync(maxUses: 1);
        var first = await NewUserAsync();
        var me = await NewUserAsync();
        await RedeemAsync(invite, first, Now);

        var outcome = await RedeemAsync(invite, me, Now);

        Assert.Equal(RedeemOutcome.NotUsable, outcome);
        Assert.Equal((1, 2, false), await StateAsync(invite, me));
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Accepting_twice_at_the_same_moment_counts_exactly_one_use()
    {
        for (var round = 0; round < 10; round++)
        {
            // Given: an unlimited invite, and the same user accepting it from 6 places at once
            const int attempts = 6;
            var (_, invite) = await InviteAsync(maxUses: null);
            var me = await NewUserAsync();
            var contexts = Enumerable.Range(0, attempts).Select(_ => database.CreateContext()).ToArray();
            foreach (var context in contexts)
                await context.Database.OpenConnectionAsync(Ct);

            // When
            using var start = new Barrier(attempts);
            var outcomes = await Task.WhenAll(contexts.Select(context => Task.Run(async () =>
            {
                start.SignalAndWait(Ct);
                return await new InviteRepository(context).TryRedeemAsync(
                    invite.Code, ServerMember.Join(invite.ServerId, me, Now), Now, Ct);
            }, Ct)));
            foreach (var context in contexts)
                await context.DisposeAsync();

            // Then
            Assert.Single(outcomes, o => o == RedeemOutcome.Joined);
            Assert.All(outcomes.Where(o => o != RedeemOutcome.Joined), o => Assert.Equal(RedeemOutcome.AlreadyMember, o));
            Assert.Equal((1, 2, true), await StateAsync(invite, me));
        }
    }

    [Fact]
    [Trait("Story", "API-INV-04")]
    public async Task Finding_an_invite_to_join_says_whether_Im_already_in()
    {
        var (server, invite) = await InviteAsync();
        var me = await NewUserAsync();

        await using var db = database.CreateContext();
        var repository = new InviteRepository(db);
        var before = await repository.FindForJoinAsync(invite.Code, me, Ct);
        var owner = await repository.FindForJoinAsync(invite.Code, server.OwnerId, Ct);

        Assert.NotNull(before);
        Assert.False(before.IsMember);
        Assert.Equal((server.Id, "join test", server.OwnerId), (before.Server.Id, before.Server.Name, before.Server.OwnerId));
        Assert.True(owner!.IsMember);
        Assert.Null(await repository.FindForJoinAsync("Zz000000", me, Ct));
    }
}
