namespace Api.AcceptanceTests.Infrastructure;

/// <summary>
/// Optional base class for test classes. Its constructor takes the assembly fixture, so a derived class just writes
/// <c>public SignUpTests(PostgresFixture db) : base(db) { }</c> (or a primary constructor).
/// The test is skipped with "TEST_PG not set" when no database is configured.
/// </summary>
public abstract class AcceptanceTest
{
    /// <summary>Skips (Assert.Skip "TEST_PG not set") already at construction when no database is configured.</summary>
    protected AcceptanceTest(PostgresFixture db)
    {
        db.EnsureAvailable();
        Db = db;
    }

    protected PostgresFixture Db { get; }

    /// <summary>The shared API host. Never advance its clock — use <see cref="IsolatedApi"/> for that.</summary>
    protected ApiFactory Api => Db.SharedApi;

    /// <summary>A private API host with its own <see cref="ApiFactory.Clock"/>; dispose it (<c>await using</c>).</summary>
    protected ApiFactory IsolatedApi() => Db.CreateIsolatedApi();

    /// <summary>Registers a fresh random user and returns a client signed in as them.</summary>
    protected Task<ApiClient> NewUserAsync(string? displayName = null, ApiFactory? api = null) =>
        ApiClient.RegisterAsync(api ?? Api, displayName);

    /// <summary>A client without a token.</summary>
    protected ApiClient Anonymous(ApiFactory? api = null) => ApiClient.Anonymous(api ?? Api);

    /// <summary>A started hub connection for <paramref name="user"/> that records every event; dispose it.</summary>
    protected Task<HubClient> ConnectAsync(ApiClient user) => HubClient.ConnectAsync(user);
}
