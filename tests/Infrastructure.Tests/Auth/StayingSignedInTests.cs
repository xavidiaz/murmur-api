using Application.Auth;
using Infrastructure.Auth;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Infrastructure.Tests.Auth;

/// <summary>Staying signed in — API-AUTH-05: refresh-token rotation against a real Postgres.</summary>
public sealed class StayingSignedInTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private static readonly IOptions<JwtOptions> Jwt = Options.Create(new JwtOptions { SigningKey = new string('k', 64) });

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TokenService Tokens(AppDbContext db) => new(db, _clock, Jwt);

    private async Task<(UserAccount User, string RefreshToken)> SignedInAsync()
    {
        await using var db = database.CreateContext();
        var name = "u_" + Guid.NewGuid().ToString("N")[..12];
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = name, NormalizedUserName = name.ToUpperInvariant(), DisplayName = name };
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        var account = new UserAccount(user.Id, name, name, null);
        var issued = await Tokens(db).IssueAsync(account, Ct);
        return (account, issued.RefreshToken);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task Refreshing_rotates_the_token_within_the_same_session()
    {
        // Given
        var (user, refreshToken) = await SignedInAsync();

        // When
        RotationResult result;
        await using (var db = database.CreateContext())
            result = await Tokens(db).RotateRefreshTokenAsync(refreshToken, Ct);

        // Then
        var rotated = Assert.IsType<RotatedRefreshToken>(result.Token);
        Assert.Equal(user.Id, rotated.UserId);
        Assert.NotEqual(refreshToken, rotated.RefreshToken);

        await using var check = database.CreateContext();
        var old = await check.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(refreshToken), Ct);
        var next = await check.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(rotated.RefreshToken), Ct);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(next.Id, old.ReplacedByTokenId);
        Assert.Equal(old.FamilyId, next.FamilyId);
        Assert.Null(next.RevokedAt);
        Assert.Equal(rotated.ExpiresAt, next.ExpiresAt);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task An_already_rotated_token_cannot_be_used_again()
    {
        // Given
        var (_, refreshToken) = await SignedInAsync();
        await using (var db = database.CreateContext())
            await Tokens(db).RotateRefreshTokenAsync(refreshToken, Ct);

        // When
        await using var again = database.CreateContext();
        var result = await Tokens(again).RotateRefreshTokenAsync(refreshToken, Ct);

        // Then
        Assert.Equal(RefreshTokenProblem.AlreadyRotated, result.Problem);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task An_expired_token_is_refused()
    {
        // Given
        var (_, refreshToken) = await SignedInAsync();
        _clock.Advance(JwtOptions.RefreshTokenLifetime);

        // When
        await using var db = database.CreateContext();
        var result = await Tokens(db).RotateRefreshTokenAsync(refreshToken, Ct);

        // Then
        Assert.Equal(RefreshTokenProblem.Expired, result.Problem);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task An_unknown_token_is_refused()
    {
        await using var db = database.CreateContext();
        var result = await Tokens(db).RotateRefreshTokenAsync("made-up", Ct);
        Assert.Equal(RefreshTokenProblem.Unknown, result.Problem);
    }

    [Fact]
    [Trait("Story", "API-AUTH-05")]
    public async Task Concurrent_refreshes_with_the_same_token_produce_at_most_one_success()
    {
        // Given
        const int attempts = 8;
        var (_, refreshToken) = await SignedInAsync();
        var contexts = Enumerable.Range(0, attempts).Select(_ => database.CreateContext()).ToArray();
        foreach (var context in contexts)
            await context.Database.OpenConnectionAsync(Ct); // so the race isn't decided by who connects first

        // When: every attempt starts at the same moment, each on its own connection
        using var start = new Barrier(attempts);
        var results = await Task.WhenAll(contexts.Select(context => Task.Run(async () =>
        {
            start.SignalAndWait(Ct);
            return await Tokens(context).RotateRefreshTokenAsync(refreshToken, Ct);
        }, Ct)));
        foreach (var context in contexts)
            await context.DisposeAsync();

        // Then
        Assert.Single(results, r => r.Token is not null);
        Assert.All(results.Where(r => r.Token is null), r => Assert.Equal(RefreshTokenProblem.AlreadyRotated, r.Problem));

        await using var check = database.CreateContext();
        var familyId = (await check.RefreshTokens.SingleAsync(t => t.TokenHash == TokenService.Hash(refreshToken), Ct)).FamilyId;
        var family = await check.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(Ct);
        Assert.Equal(2, family.Count); // the original and exactly one replacement
        // API-AUTH-06 (strict): the losers count as reuse, so the whole family, the winner's new token included, is ended.
        Assert.All(family, t => Assert.NotNull(t.RevokedAt));
    }
}
