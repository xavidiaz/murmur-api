using Application.Invites;
using Domain.Invites;
using Domain.Servers;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests.Invites;

/// <summary>
/// A single-use invite really is single-use — API-INV-05: different people racing for the one use, counted directly
/// in the database (the acceptance tests can't read <c>Uses</c> once the invite is used up: preview is 410 then).
/// </summary>
public sealed class SingleUseInviteTests(TestDatabase database) : IClassFixture<TestDatabase>
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
    [Trait("Story", "API-INV-05")]
    public async Task When_several_people_accept_a_single_use_invite_at_the_same_moment_exactly_one_use_is_counted()
    {
        const int people = 8;
        for (var round = 0; round < 10; round++)
        {
            // Given: a single-use invite and 8 different people, each on their own open connection
            var owner = await NewUserAsync();
            var server = Server.Create(owner, "single use", null, Now);
            var invite = Invite.Create(server.Id, owner, Now, expiresAt: null, maxUses: 1);
            await using (var setup = database.CreateContext())
            {
                await new ServerRepository(setup).AddAsync(server, Ct);
                await new InviteRepository(setup).TryAddAsync(invite, Ct);
            }
            var users = new Guid[people];
            for (var i = 0; i < people; i++)
                users[i] = await NewUserAsync();
            var contexts = Enumerable.Range(0, people).Select(_ => database.CreateContext()).ToArray();
            foreach (var context in contexts)
                await context.Database.OpenConnectionAsync(Ct);

            // When: all of them redeem at once
            using var start = new Barrier(people);
            var outcomes = await Task.WhenAll(Enumerable.Range(0, people).Select(i => Task.Run(async () =>
            {
                start.SignalAndWait(Ct);
                return await new InviteRepository(contexts[i]).TryRedeemAsync(
                    invite.Code, ServerMember.Join(server.Id, users[i], Now), Now, Ct);
            }, Ct)));
            foreach (var context in contexts)
                await context.DisposeAsync();

            // Then: one winner; in the database exactly one use and exactly one new member
            Assert.Single(outcomes, o => o == RedeemOutcome.Joined);
            Assert.All(outcomes.Where(o => o != RedeemOutcome.Joined), o => Assert.Equal(RedeemOutcome.NotUsable, o));
            await using var check = database.CreateContext();
            var uses = await check.Invites.Where(i => i.Code == invite.Code).Select(i => i.Uses).SingleAsync(Ct);
            var joined = await check.Set<ServerMember>().CountAsync(m => m.ServerId == server.Id && m.UserId != owner, Ct);
            Assert.True((uses, joined) == (1, 1), $"round {round}: Uses = {uses}, new members = {joined}");
        }
    }
}
