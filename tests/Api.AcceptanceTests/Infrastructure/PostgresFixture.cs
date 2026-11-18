using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Npgsql;

namespace Api.AcceptanceTests.Infrastructure;

/// <summary>
/// xUnit v3 assembly fixture: creates one throwaway database <c>murmur_test_&lt;yyyyMMddHHmmss&gt;_&lt;6 random&gt;</c>
/// (or, without CREATEDB, a schema <c>test_&lt;runid&gt;</c>) for the whole run and drops it afterwards.
/// The connection string comes from env <c>TEST_PG</c>, else <c>~/.config/murmur/test-pg</c>; if neither exists every
/// test is skipped. The connection string is never logged, printed or put into an exception message.
/// </summary>
public sealed partial class PostgresFixture : IAsyncLifetime
{
    public const string EnvVar = "TEST_PG";
    public const string SkipReason = "TEST_PG not set";
    private const string DatabasePrefix = "murmur_test_";
    private const string SchemaPrefix = "test_";
    private static readonly TimeSpan LeftoverAge = TimeSpan.FromHours(2);

    private string? _adminConnectionString;
    private string? _testConnectionString;
    private string? _databaseName;   // set when we created a database
    private string? _schemaName;     // set when we fell back to a schema
    private Lazy<ApiFactory>? _sharedApi;

    /// <summary>True when no connection string was configured; every test must skip.</summary>
    public bool IsUnavailable => _adminConnectionString is null;

    /// <summary>Connection string of the throwaway database/schema. Never print it.</summary>
    internal string TestConnectionString
    {
        get
        {
            EnsureAvailable();
            return _testConnectionString!;
        }
    }

    /// <summary>Skips the current test (Assert.Skip) when no connection string is configured.</summary>
    public void EnsureAvailable()
    {
        if (IsUnavailable)
            Assert.Skip(SkipReason);
    }

    /// <summary>
    /// The API instance shared by all tests that don't move the clock. Started (and migrated) on first use.
    /// Do not advance <c>SharedApi.Clock</c>; use <see cref="CreateIsolatedApi"/> for that.
    /// </summary>
    public ApiFactory SharedApi
    {
        get
        {
            EnsureAvailable();
            return _sharedApi!.Value;
        }
    }

    /// <summary>
    /// A separate API host (own DI container, own <see cref="ApiFactory.Clock"/>) on the same database.
    /// The shared host is started first so migrations are already applied. The caller disposes it.
    /// </summary>
    public ApiFactory CreateIsolatedApi()
    {
        _ = SharedApi; // ensures migrations ran once before a second host starts
        var api = new ApiFactory(TestConnectionString);
        _ = api.Server; // start now so failures surface here
        return api;
    }

    public async ValueTask InitializeAsync()
    {
        _adminConnectionString = ResolveConnectionString();
        if (_adminConnectionString is null)
            return;

        _adminConnectionString = Normalize(_adminConnectionString);

        await using var conn = new NpgsqlConnection(_adminConnectionString);
        await OpenOrExplainAsync(conn);

        var now = DateTime.UtcNow;
        var runId = now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + RandomSuffix();

        await DropLeftoverDatabasesAsync(conn, now);

        var dbName = DatabasePrefix + runId;
        try
        {
            await ExecuteAsync(conn, $"CREATE DATABASE {Quote(dbName)}");
            _databaseName = dbName;
            _testConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = dbName }.ConnectionString;
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            await DropLeftoverSchemasAsync(conn, now);
            var schema = SchemaPrefix + runId;
            await ExecuteAsync(conn, $"CREATE SCHEMA {Quote(schema)}");
            _schemaName = schema;
            _testConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString) { SearchPath = schema }.ConnectionString;
        }

        _sharedApi = new Lazy<ApiFactory>(() =>
        {
            var api = new ApiFactory(_testConnectionString);
            _ = api.Server; // start + migrate once; a failure is cached and reported by every test
            return api;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sharedApi is { IsValueCreated: true })
        {
            try { await _sharedApi.Value.DisposeAsync(); }
            catch { /* host failed to start; nothing to dispose */ }
        }

        if (_adminConnectionString is null || (_databaseName is null && _schemaName is null))
            return;

        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        if (_databaseName is not null)
            await ExecuteAsync(conn, $"DROP DATABASE IF EXISTS {Quote(_databaseName)} WITH (FORCE)");
        else
            await ExecuteAsync(conn, $"DROP SCHEMA IF EXISTS {Quote(_schemaName!)} CASCADE");
    }

    private static string? ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home))
            return null;
        var file = Path.Combine(home, ".config", "murmur", "test-pg");
        if (!File.Exists(file))
            return null;
        var fromFile = File.ReadAllText(file).Trim();
        return fromFile.Length == 0 ? null : fromFile;
    }

    /// <summary>
    /// Accepts a keyword connection string (<c>Host=…;Username=…</c>) or a libpq URI
    /// (<c>postgres[ql]://user:pass@host:port/db?sslmode=…</c>) and returns an Npgsql keyword string.
    /// Errors never include the value itself.
    /// </summary>
    private static string Normalize(string raw)
    {
        const string invalid = $"The test Postgres setting (${EnvVar} or ~/.config/murmur/test-pg) is not a valid " +
                               "Npgsql connection string or postgres:// URI. (Value not shown.)";
        try
        {
            if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
                !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
                return new NpgsqlConnectionStringBuilder(raw).ConnectionString;

            var uri = new Uri(raw);
            var b = new NpgsqlConnectionStringBuilder { Host = uri.Host };
            if (!uri.IsDefaultPort && uri.Port > 0)
                b.Port = uri.Port;

            var userInfo = uri.UserInfo;
            if (userInfo.Length > 0)
            {
                var colon = userInfo.IndexOf(':');
                b.Username = Uri.UnescapeDataString(colon < 0 ? userInfo : userInfo[..colon]);
                if (colon >= 0)
                    b.Password = Uri.UnescapeDataString(userInfo[(colon + 1)..]);
            }

            var db = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (db.Length > 0)
                b.Database = db;

            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]).ToLowerInvariant();
                var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
                switch (key)
                {
                    case "sslmode": b.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), ignoreCase: true); break;
                    case "connect_timeout": b.Timeout = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "application_name": b.ApplicationName = value; break;
                    case "options": b.Options = value; break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported query parameter '{key}' in the test Postgres URI. (Value not shown.)");
                }
            }
            return b.ConnectionString;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            // Deliberately no inner exception: parser messages can quote the value.
            throw new InvalidOperationException(invalid);
        }
    }

    /// <summary>Opens the admin connection; on failure throws a message that contains no connection details.</summary>
    private static async Task OpenOrExplainAsync(NpgsqlConnection conn)
    {
        try
        {
            await conn.OpenAsync();
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.InvalidPassword
                                              or PostgresErrorCodes.InvalidAuthorizationSpecification)
        {
            // Deliberately no inner exception: the server message would name the user/host.
            throw new InvalidOperationException(
                $"Postgres rejected the test credentials (SQLSTATE {e.SqlState}). " +
                $"Fix the connection string in ${EnvVar} or ~/.config/murmur/test-pg. (Connection details are not shown.)");
        }
        catch (PostgresException e)
        {
            throw new InvalidOperationException(
                $"Postgres refused the test connection (SQLSTATE {e.SqlState}). (Connection details are not shown.)");
        }
        catch (Exception e) when (e is NpgsqlException or ArgumentException or TimeoutException or System.Net.Sockets.SocketException)
        {
            if (FindInner<PostgresException>(e) is { } pg)
            {
                var auth = pg.SqlState is PostgresErrorCodes.InvalidPassword or PostgresErrorCodes.InvalidAuthorizationSpecification;
                throw new InvalidOperationException(
                    (auth ? "Postgres rejected the test credentials" : "Postgres refused the test connection") +
                    $" (SQLSTATE {pg.SqlState}). Fix ${EnvVar} or ~/.config/murmur/test-pg. (Connection details are not shown.)");
            }
            // Type names only: Npgsql/socket messages can contain host, user or database names.
            throw new InvalidOperationException(
                $"Could not connect to the test Postgres ({TypeChain(e)}). Check ${EnvVar} or ~/.config/murmur/test-pg. " +
                "(Connection details are not shown.)");
        }
    }

    private static T? FindInner<T>(Exception? e) where T : Exception
    {
        for (; e is not null; e = e.InnerException)
            if (e is T t)
                return t;
        return null;
    }

    private static string TypeChain(Exception? e)
    {
        var names = new List<string>();
        for (; e is not null; e = e.InnerException)
            names.Add(e.GetType().Name);
        return string.Join(" -> ", names);
    }

    private static async Task DropLeftoverDatabasesAsync(NpgsqlConnection conn, DateTime now)
    {
        var names = await QueryNamesAsync(conn, @"SELECT datname FROM pg_database WHERE datname LIKE 'murmur\_test\_%'");
        foreach (var name in names)
        {
            if (!IsStale(LeftoverDatabase(), name, now))
                continue;
            try { await ExecuteAsync(conn, $"DROP DATABASE IF EXISTS {Quote(name)} WITH (FORCE)"); }
            catch (PostgresException) { /* not ours to drop (owner/privileges) or in use: leave it */ }
        }
    }

    private static async Task DropLeftoverSchemasAsync(NpgsqlConnection conn, DateTime now)
    {
        var names = await QueryNamesAsync(conn, @"SELECT nspname FROM pg_namespace WHERE nspname LIKE 'test\_%'");
        foreach (var name in names)
        {
            if (!IsStale(LeftoverSchema(), name, now))
                continue;
            try { await ExecuteAsync(conn, $"DROP SCHEMA IF EXISTS {Quote(name)} CASCADE"); }
            catch (PostgresException) { /* not ours to drop: leave it */ }
        }
    }

    /// <summary>Only names matching our exact pattern whose embedded UTC timestamp is older than 2 h.</summary>
    private static bool IsStale(Regex pattern, string name, DateTime now)
    {
        var m = pattern.Match(name);
        if (!m.Success)
            return false;
        if (!DateTime.TryParseExact(m.Groups["ts"].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var stamp))
            return false;
        return now - stamp > LeftoverAge;
    }

    [GeneratedRegex(@"^murmur_test_(?<ts>\d{14})_[a-z0-9]{6}$")]
    private static partial Regex LeftoverDatabase();

    [GeneratedRegex(@"^test_(?<ts>\d{14})_[a-z0-9]{6}$")]
    private static partial Regex LeftoverSchema();

    private static async Task<List<string>> QueryNamesAsync(NpgsqlConnection conn, string sql)
    {
        var result = new List<string>();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));
        return result;
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Identifiers are generated or regex-validated above; quoting is belt and braces.</summary>
    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string RandomSuffix()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(alphabet, 6);
    }
}
