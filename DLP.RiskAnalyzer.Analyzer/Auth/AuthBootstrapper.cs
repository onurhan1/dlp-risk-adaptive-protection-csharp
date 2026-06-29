using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DLP.RiskAnalyzer.Analyzer.Auth;

/// <summary>
/// Result of the startup auth/config bootstrap.
/// </summary>
public class AuthBootstrapResult
{
    public AuthJwtSettings Jwt { get; init; } = new();

    /// <summary>
    /// Configuration overrides (key -> value) loaded from the DB, to be layered over appsettings
    /// as an in-memory configuration source so existing consumers read DB values transparently.
    /// </summary>
    public Dictionary<string, string?> ConfigOverrides { get; init; } = new();
}

/// <summary>
/// Ensures the <c>auth</c> schema and <c>auth.auth_settings</c> table exist and loads sensitive
/// configuration from the database instead of appsettings:
///   - JWT settings (secret auto-generated on first run),
///   - Redis / DLP / InternalApi secrets (seeded from appsettings on first run, then DB-authoritative).
///
/// Runs at startup (before the host is built) using a raw Npgsql connection, because both the JWT
/// signing key and the Redis/DLP/InternalApi settings are needed before the DI container is built.
/// The DB connection string itself stays in appsettings (it is required to reach the database).
/// </summary>
public static class AuthBootstrapper
{
    // (db key, appsettings key, optional default) for sensitive config moved into auth.auth_settings.
    private static readonly (string DbKey, string ConfigKey, string? Default)[] ManagedConfig =
    {
        ("redis_host", "Redis:Host", "localhost"),
        ("redis_port", "Redis:Port", "6379"),
        ("redis_password", "Redis:Password", null),
        ("dlp_manager_ip", "DLP:ManagerIP", null),
        ("dlp_manager_port", "DLP:ManagerPort", null),
        ("dlp_username", "DLP:Username", null),
        ("dlp_password", "DLP:Password", null),
        ("dlp_use_https", "DLP:UseHttps", null),
        ("dlp_timeout", "DLP:Timeout", null),
        ("internal_api_shared_secret", "InternalApi:SharedSecret", null),
    };

    public static AuthBootstrapResult EnsureAndLoad(IConfiguration configuration, ILogger? logger = null)
    {
        var result = new AuthBootstrapResult();
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using (var ddl = conn.CreateCommand())
            {
                ddl.CommandText = @"
                    CREATE SCHEMA IF NOT EXISTS auth;
                    CREATE TABLE IF NOT EXISTS auth.auth_settings (
                        key VARCHAR(100) PRIMARY KEY,
                        value TEXT NOT NULL,
                        updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );";
                ddl.ExecuteNonQuery();
            }

            // JWT settings
            result.Jwt.SecretKey = GetOrCreate(conn, "jwt_secret_key", GenerateSecretKey);
            result.Jwt.Issuer = GetOrCreate(conn, "jwt_issuer", () => "DLP-RiskAnalyzer");
            result.Jwt.Audience = GetOrCreate(conn, "jwt_audience", () => "DLP-RiskAnalyzer-Client");
            var expRaw = GetOrCreate(conn, "jwt_expiration_hours", () => "8");
            result.Jwt.ExpirationHours = int.TryParse(expRaw, out var hours) && hours > 0 ? hours : 8;

            // Sensitive infra config — seed from appsettings on first run, then DB is authoritative.
            foreach (var (dbKey, configKey, def) in ManagedConfig)
            {
                var seed = configuration[configKey] ?? def ?? string.Empty;
                var value = GetOrCreate(conn, dbKey, () => seed);
                result.ConfigOverrides[configKey] = value;
            }

            logger?.LogInformation("Auth/config loaded from database (auth.auth_settings). JWT issuer={Issuer}, {Count} config keys.",
                result.Jwt.Issuer, result.ConfigOverrides.Count);
        }
        catch (Exception ex)
        {
            // Safe fallback: keep appsettings values (no overrides) and an ephemeral JWT key so the app still starts.
            logger?.LogError(ex, "Failed to bootstrap auth/config from database. Falling back to appsettings + ephemeral JWT key.");
            if (string.IsNullOrEmpty(result.Jwt.SecretKey))
                result.Jwt.SecretKey = GenerateSecretKey();
            result.ConfigOverrides.Clear();
        }

        return result;
    }

    /// <summary>Reads a value; if absent, creates it from <paramref name="factory"/> and persists it.</summary>
    private static string GetOrCreate(NpgsqlConnection conn, string key, Func<string> factory)
    {
        using (var read = conn.CreateCommand())
        {
            read.CommandText = "SELECT value FROM auth.auth_settings WHERE key = @k";
            read.Parameters.AddWithValue("k", key);
            var existing = read.ExecuteScalar() as string;
            if (existing != null)
                return existing;
        }

        var value = factory() ?? string.Empty;
        using (var insert = conn.CreateCommand())
        {
            insert.CommandText = @"
                INSERT INTO auth.auth_settings (key, value, updated_at)
                VALUES (@k, @v, CURRENT_TIMESTAMP)
                ON CONFLICT (key) DO NOTHING";
            insert.Parameters.AddWithValue("k", key);
            insert.Parameters.AddWithValue("v", value);
            insert.ExecuteNonQuery();
        }

        // Re-read in case of a concurrent insert (ON CONFLICT DO NOTHING).
        using (var reRead = conn.CreateCommand())
        {
            reRead.CommandText = "SELECT value FROM auth.auth_settings WHERE key = @k";
            reRead.Parameters.AddWithValue("k", key);
            return reRead.ExecuteScalar() as string ?? value;
        }
    }

    private static string GenerateSecretKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
