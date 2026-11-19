using Application.Auth;
using Infrastructure.Auth;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Infrastructure.Tests.Auth;

/// <summary>Sign out — API-AUTH-07: ending a session (token family), against a real Postgres.</summary>
public sealed class SignOutTests(TestDatabase database) : IClassFixture<TestDatabase>
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

    private async Task<bool> SignOutAsync(Guid userId, string refreshToken)
    {
        await using var db = database.CreateContext();
        return await new TokenService(db, _clock, Jwt).EndSessionAsync(userId, refreshToken, Ct);
    }

    private async Task<int> LiveTokensInFamilyOfAsync(string refreshToken)
    {
        await using var db = database.CreateContext();
        var familyId = (await db.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(refreshToken), Ct)).FamilyId;
        return await db.RefreshTokens.CountAsync(t => t.FamilyId == familyId && t.RevokedAt == null, Ct);
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_ends_this_devices_whole_session()
    {
        // Given: the device has rotated once
        var me = await NewUserAsync();
        var r1 = await SignInAsync(me);
        var r2 = (await RotateAsync(r1)).Token!.RefreshToken;

        // When
        var ended = await SignOutAsync(me.Id, r2);

        // Then
        Assert.True(ended);
        Assert.Equal(0, await LiveTokensInFamilyOfAsync(r1));
        Assert.Equal(RefreshTokenProblem.Revoked, (await RotateAsync(r2)).Problem);
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task My_other_devices_stay_signed_in()
    {
        // Given
        var me = await NewUserAsync();
        var deviceA = await SignInAsync(me);
        var deviceB = await SignInAsync(me);

        // When
        await SignOutAsync(me.Id, deviceA);

        // Then
        Assert.Equal(1, await LiveTokensInFamilyOfAsync(deviceB));
        Assert.NotNull((await RotateAsync(deviceB)).Token);
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_with_someone_elses_refresh_token_changes_nothing()
    {
        // Given
        var me = await NewUserAsync();
        var someoneElse = await NewUserAsync();
        var theirs = await SignInAsync(someoneElse);

        // When
        var ended = await SignOutAsync(me.Id, theirs);

        // Then
        Assert.False(ended);
        Assert.Equal(1, await LiveTokensInFamilyOfAsync(theirs));
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_with_an_unknown_refresh_token_changes_nothing()
    {
        var me = await NewUserAsync();
        var mine = await SignInAsync(me);

        Assert.False(await SignOutAsync(me.Id, "made-up"));
        Assert.Equal(1, await LiveTokensInFamilyOfAsync(mine));
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_twice_changes_nothing_the_second_time()
    {
        // Given
        var me = await NewUserAsync();
        var token = await SignInAsync(me);
        await SignOutAsync(me.Id, token);

        // When
        var ended = await SignOutAsync(me.Id, token);

        // Then
        Assert.False(ended);
        Assert.Equal(0, await LiveTokensInFamilyOfAsync(token));
    }

    [Fact]
    [Trait("Story", "API-AUTH-07")]
    public async Task Signing_out_while_the_same_session_refreshes_leaves_no_live_token()
    {
        // Sign-out races a refresh of the same token. Whatever the interleaving, the session must end:
        // a token the refresh inserts must not slip past the sign-out. A sign-out has fewer round trips than a
        // rotation, so its start is staggered (0–4 ms in 100 µs steps) to also land inside the rotation's
        // window between retiring the old token and committing the new one.
        for (var round = 0; round < 40; round++)
        {
            // Given
            var me = await NewUserAsync();
            var token = await SignInAsync(me);
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
                    await new TokenService(contexts[0], _clock, Jwt).EndSessionAsync(me.Id, token, Ct);
                }, Ct),
                Task.Run(async () => { start.SignalAndWait(Ct); await new TokenService(contexts[1], _clock, Jwt).RotateRefreshTokenAsync(token, Ct); }, Ct));
            foreach (var context in contexts)
                await context.DisposeAsync();

            // Then
            var live = await LiveTokensInFamilyOfAsync(token);
            Assert.True(live == 0, $"round {round}: {live} live token(s) survived the sign-out");
        }
    }

    private static void SpinFor(TimeSpan duration)
    {
        var until = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(duration.TotalSeconds * System.Diagnostics.Stopwatch.Frequency);
        while (System.Diagnostics.Stopwatch.GetTimestamp() < until)
            Thread.SpinWait(20);
    }
}
