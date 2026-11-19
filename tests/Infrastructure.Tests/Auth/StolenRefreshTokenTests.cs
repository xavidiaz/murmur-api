using Application.Auth;
using Infrastructure.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Infrastructure.Tests.Auth;

/// <summary>A stolen refresh token is useless — API-AUTH-06: reuse ends the whole session, against a real Postgres.</summary>
public sealed class StolenRefreshTokenTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static readonly IOptions<JwtOptions> Jwt = Options.Create(new JwtOptions { SigningKey = new string('k', 64) });

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<UserAccount> NewUserAsync()
    {
        await using var db = database.CreateContext();
        var name = "u_" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return new UserAccount(user.Id, name, name, null);
    }

    private async Task<string> SignInAsync(UserAccount user)
    {
        await using var db = database.CreateContext();
        return (await new TokenService(db, _clock, Jwt).IssueAsync(user, Ct)).RefreshToken;
    }

    private async Task<RotationResult> RotateAsync(string refreshToken)
    {
        await using var db = database.CreateContext();
        return await new TokenService(db, _clock, Jwt).RotateRefreshTokenAsync(refreshToken, Ct);
    }

    private async Task<List<RefreshToken>> FamilyOfAsync(string refreshToken)
    {
        await using var db = database.CreateContext();
        var familyId = (await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(refreshToken), Ct)).FamilyId;
        return await db.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(Ct);
    }

    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task Reusing_an_old_refresh_token_is_rejected_and_the_whole_session_is_ended()
    {
        // Given
        var r1 = await SignInAsync(await NewUserAsync());
        var r2 = (await RotateAsync(r1)).Token!.RefreshToken;

        // When
        var reuse = await RotateAsync(r1);

        // Then
        Assert.Equal(RefreshTokenProblem.AlreadyRotated, reuse.Problem);
        Assert.Equal(RefreshTokenProblem.Revoked, (await RotateAsync(r2)).Problem);
        Assert.All(await FamilyOfAsync(r1), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task My_other_sessions_are_unaffected()
    {
        // Given
        var me = await NewUserAsync();
        var a1 = await SignInAsync(me);
        var b1 = await SignInAsync(me);
        await RotateAsync(a1);

        // When
        await RotateAsync(a1);

        // Then
        Assert.Single(await FamilyOfAsync(b1), t => t.RevokedAt is null);
        Assert.NotNull((await RotateAsync(b1)).Token);
    }

    [Fact]
    [Trait("Story", "API-AUTH-06")]
    public async Task Reuse_racing_a_rotation_of_the_newest_token_still_ends_the_whole_session()
    {
        // Reuse of R1 (which revokes the family) races a legitimate rotation R2 → R3. Whatever the interleaving,
        // no token of the family may survive: R3 must not slip past the revocation. The reuse's start is staggered
        // (0–4 ms in 100 µs steps) so that some rounds land it inside the rotation's window between retiring R2 and
        // committing R3, the window the family lock closes; the test can't pass by lucky timing.
        for (var round = 0; round < 40; round++)
        {
            // Given
            var r1 = await SignInAsync(await NewUserAsync());
            var r2 = (await RotateAsync(r1)).Token!.RefreshToken;
            var contexts = new[] { database.CreateContext(), database.CreateContext() };
            foreach (var context in contexts)
                await context.Database.OpenConnectionAsync(Ct);

            // When
            using var start = new Barrier(2);
            await Task.WhenAll(
                Task.Run(async () =>
                {
                    start.SignalAndWait(Ct);
                    SpinFor(TimeSpan.FromMicroseconds(100 * round));
                    return await new TokenService(contexts[0], _clock, Jwt).RotateRefreshTokenAsync(r1, Ct);
                }, Ct),
                Task.Run(async () => { start.SignalAndWait(Ct); return await new TokenService(contexts[1], _clock, Jwt).RotateRefreshTokenAsync(r2, Ct); }, Ct));
            foreach (var context in contexts)
                await context.DisposeAsync();

            // Then
            var family = await FamilyOfAsync(r1);
            Assert.True(family.All(t => t.RevokedAt is not null),
                $"round {round}: {family.Count(t => t.RevokedAt is null)} live token(s) survived the reuse");
        }
    }

    private static void SpinFor(TimeSpan duration)
    {
        var until = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        while (System.Diagnostics.Stopwatch.GetTimestamp() < until)
            Thread.SpinWait(20);
    }
}
