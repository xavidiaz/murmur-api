using System.Globalization;
using System.Security.Cryptography;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// A migrated throwaway Postgres database <c>murmur_test_&lt;ts&gt;_&lt;rand&gt;</c>, or (without CREATEDB) a schema
/// <c>test_&lt;ts&gt;_&lt;rand&gt;</c>, dropped on dispose. The server comes from env <c>TEST_PG</c> or
/// <c>~/.config/murmur/test-pg</c>; without either, tests skip. The connection string is never printed.
/// Names follow the acceptance fixture's pattern, so its stale-leftover cleanup covers crashed runs here too.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    private const string SkipReason = "TEST_PG not set";

    private string? _admin;
    private string? _connectionString;
    private string? _database;
    private string? _schema;

    public async ValueTask InitializeAsync()
    {
        _admin = ResolveConnectionString();
        if (_admin is null)
            return;

        var runId = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" +
                    RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", 6);
        await using (var conn = new NpgsqlConnection(_admin))
        {
            await conn.OpenAsync();
            try
            {
                await ExecuteAsync(conn, $"CREATE DATABASE \"murmur_test_{runId}\"");
                _database = "murmur_test_" + runId;
                _connectionString = new NpgsqlConnectionStringBuilder(_admin) { Database = _database }.ConnectionString;
            }
            catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.InsufficientPrivilege)
            {
                _schema = "test_" + runId;
                await ExecuteAsync(conn, $"CREATE SCHEMA \"{_schema}\"");
                _connectionString = new NpgsqlConnectionStringBuilder(_admin) { SearchPath = _schema }.ConnectionString;
            }
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    /// <summary>A new context on the throwaway database; skips the test when no server is configured.</summary>
    public AppDbContext CreateContext()
    {
        if (_connectionString is null)
            Assert.Skip(SkipReason);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);
    }

    public async ValueTask DisposeAsync()
    {
        if (_admin is null || (_database is null && _schema is null))
            return;
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_admin);
        await conn.OpenAsync();
        await ExecuteAsync(conn, _database is not null
            ? $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)"
            : $"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
    }

    private static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? ResolveConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("TEST_PG");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "murmur", "test-pg");
            raw = File.Exists(file) ? File.ReadAllText(file) : null;
        }
        raw = raw?.Trim();
        if (string.IsNullOrEmpty(raw))
            return null;
        try
        {
            return raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
                   raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? FromUri(new Uri(raw))
                : new NpgsqlConnectionStringBuilder(raw).ConnectionString;
        }
        catch (Exception)
        {
            // No inner exception: parser messages can quote the value.
            throw new InvalidOperationException("TEST_PG is not a valid connection string or postgres:// URI. (Value not shown.)");
        }
    }

    private static string FromUri(Uri uri)
    {
        var b = new NpgsqlConnectionStringBuilder { Host = uri.Host };
        if (!uri.IsDefaultPort && uri.Port > 0)
            b.Port = uri.Port;
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo[0].Length > 0)
            b.Username = Uri.UnescapeDataString(userInfo[0]);
        if (userInfo.Length > 1)
            b.Password = Uri.UnescapeDataString(userInfo[1]);
        var db = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (db.Length > 0)
            b.Database = db;
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            switch (kv[0].ToLowerInvariant())
            {
                case "sslmode": b.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), ignoreCase: true); break;
                case "connect_timeout": b.Timeout = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "application_name": b.ApplicationName = value; break;
                case "options": b.Options = value; break;
                default: throw new InvalidOperationException($"Unsupported URI parameter '{kv[0]}'.");
            }
        }
        return b.ConnectionString;
    }
}
