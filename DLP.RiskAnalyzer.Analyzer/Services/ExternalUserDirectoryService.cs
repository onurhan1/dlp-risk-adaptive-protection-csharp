using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ExternalUserDirectoryService : IExternalUserDirectoryService
{
    private const string Prefix = "external_user_db_";
    private const string EnabledKey = Prefix + "enabled";
    private const string HostKey = Prefix + "host";
    private const string PortKey = Prefix + "port";
    private const string DatabaseKey = Prefix + "database";
    private const string UsernameKey = Prefix + "username";
    private const string PasswordKey = Prefix + "password_protected";
    private const string EncryptKey = Prefix + "encrypt";
    private const string TrustServerCertificateKey = Prefix + "trust_server_certificate";
    private const string TableNameKey = Prefix + "table_name";
    private const string MatchColumnKey = Prefix + "match_column";
    private const string FirstNameColumnKey = Prefix + "first_name_column";
    private const string LastNameColumnKey = Prefix + "last_name_column";
    private const string FullNameColumnKey = Prefix + "full_name_column";
    private const string EmailColumnKey = Prefix + "email_column";
    private const string DepartmentColumnKey = Prefix + "department_column";
    private const string WhereClauseKey = Prefix + "where_clause";

    private static readonly Regex IdentifierPart = new(@"^[A-Za-z_][A-Za-z0-9_ ]*$", RegexOptions.Compiled);

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExternalUserDirectoryService> _logger;

    public ExternalUserDirectoryService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IMemoryCache cache,
        ILogger<ExternalUserDirectoryService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("ExternalUserDb.SettingsProtector");
        _cache = cache;
        _logger = logger;
    }

    public async Task<ExternalUserDbSettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        var dict = await LoadAsync(ct);
        return BuildResponse(dict);
    }

    public async Task<ExternalUserDbSettingsResponse> SaveSettingsAsync(ExternalUserDbSettingsRequest request, CancellationToken ct = default)
    {
        ValidateSettings(request, allowEmptyPassword: true);

        await UpsertAsync(EnabledKey, request.Enabled.ToString(), ct);
        await UpsertAsync(HostKey, request.Host.Trim(), ct);
        await UpsertAsync(PortKey, request.Port.ToString(), ct);
        await UpsertAsync(DatabaseKey, request.Database.Trim(), ct);
        await UpsertAsync(UsernameKey, request.Username.Trim(), ct);
        await UpsertAsync(EncryptKey, request.Encrypt.ToString(), ct);
        await UpsertAsync(TrustServerCertificateKey, request.TrustServerCertificate.ToString(), ct);
        await UpsertAsync(TableNameKey, request.TableName.Trim(), ct);
        await UpsertAsync(MatchColumnKey, request.MatchColumn.Trim(), ct);
        await UpsertAsync(FirstNameColumnKey, request.FirstNameColumn.Trim(), ct);
        await UpsertAsync(LastNameColumnKey, request.LastNameColumn.Trim(), ct);
        await UpsertAsync(FullNameColumnKey, request.FullNameColumn.Trim(), ct);
        await UpsertAsync(EmailColumnKey, request.EmailColumn.Trim(), ct);
        await UpsertAsync(DepartmentColumnKey, request.DepartmentColumn.Trim(), ct);
        await UpsertAsync(WhereClauseKey, request.WhereClause.Trim(), ct);

        if (!string.IsNullOrWhiteSpace(request.Password))
            await UpsertAsync(PasswordKey, _protector.Protect(request.Password), ct);

        _cache.Remove("external-user-db:settings");
        return await GetSettingsAsync(ct);
    }

    public async Task<ExternalUserLookupResult> TestConnectionAsync(ExternalUserDbSettingsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ct);

        ValidateSettings(request, allowEmptyPassword: false);

        try
        {
            await using var connection = CreateConnection(BuildConnectionString(request));
            await connection.OpenAsync(ct);
            return Result(true, "MSSQL baglantisi basarili");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External user DB connection test failed");
            return Result(false, $"MSSQL baglanti testi basarisiz: {ex.Message}");
        }
    }

    public async Task<ExternalUserLookupResult> TestLookupAsync(ExternalUserLookupRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TestUsername))
            return Result(false, "Test kullanici adi zorunludur");
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ct);

        ValidateSettings(request, allowEmptyPassword: false);

        try
        {
            var user = await LookupAsync(request, request.TestUsername, ct);
            return user == null
                ? Result(false, "Kullanici bulunamadi")
                : Result(true, "Kullanici bulundu", user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External user DB lookup test failed");
            return Result(false, $"MSSQL kullanici testi basarisiz: {ex.Message}");
        }
    }

    public async Task<ExternalUserProfileDto?> ResolveUserAsync(string? userName, CancellationToken ct = default)
    {
        var normalized = NormalizeUserName(userName);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var settings = await GetCachedSettingsAsync(ct);
        if (!settings.Enabled || !settings.IsConfigured) return null;

        var cacheKey = $"external-user-db:user:{normalized.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ExternalUserProfileDto? cached)) return cached;

        try
        {
            settings.Password = await GetSecretAsync(ct);
            var user = await LookupAsync(settings, normalized, ct);
            _cache.Set(cacheKey, user, TimeSpan.FromMinutes(user == null ? 5 : 30));
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External user DB lookup failed for {UserName}", normalized);
            _cache.Set<ExternalUserProfileDto?>(cacheKey, null, TimeSpan.FromMinutes(5));
            return null;
        }
    }

    private async Task<ExternalUserProfileDto?> LookupAsync(ExternalUserDbSettingsRequest settings, string userName, CancellationToken ct)
    {
        await using var connection = CreateConnection(BuildConnectionString(settings));
        await connection.OpenAsync(ct);

        var query = BuildLookupSql(settings);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        command.CommandType = CommandType.Text;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@username";
        parameter.Value = userName;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var firstName = Read(reader, "first_name");
        var lastName = Read(reader, "last_name");
        var fullName = Read(reader, "full_name");
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = string.Join(" ", new[] { firstName, lastName }.Where(v => !string.IsNullOrWhiteSpace(v)));

        return new ExternalUserProfileDto
        {
            UserName = Read(reader, "user_name") ?? userName,
            FirstName = firstName,
            LastName = lastName,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Email = Read(reader, "email"),
            Department = Read(reader, "department")
        };
    }

    private static string BuildLookupSql(ExternalUserDbSettingsRequest settings)
    {
        var projections = new List<string>
        {
            $"{QuoteIdentifier(settings.MatchColumn)} AS [user_name]"
        };
        AddProjection(projections, settings.FirstNameColumn, "first_name");
        AddProjection(projections, settings.LastNameColumn, "last_name");
        AddProjection(projections, settings.FullNameColumn, "full_name");
        AddProjection(projections, settings.EmailColumn, "email");
        AddProjection(projections, settings.DepartmentColumn, "department");

        var where = $"{QuoteIdentifier(settings.MatchColumn)} = @username";
        if (!string.IsNullOrWhiteSpace(settings.WhereClause))
            where += $" AND ({SafeWhereClause(settings.WhereClause)})";

        return $"SELECT TOP (1) {string.Join(", ", projections)} FROM {QuoteMultipartIdentifier(settings.TableName)} WHERE {where}";
    }

    private static void AddProjection(List<string> projections, string column, string alias)
    {
        if (!string.IsNullOrWhiteSpace(column))
            projections.Add($"{QuoteIdentifier(column)} AS [{alias}]");
        else
            projections.Add($"CAST(NULL AS nvarchar(4000)) AS [{alias}]");
    }

    private static string BuildConnectionString(ExternalUserDbSettingsRequest settings)
    {
        var builder = new DbConnectionStringBuilder
        {
            ["Server"] = $"{settings.Host.Trim()},{settings.Port}",
            ["Database"] = settings.Database.Trim(),
            ["User ID"] = settings.Username.Trim(),
            ["Password"] = settings.Password ?? string.Empty,
            ["Encrypt"] = settings.Encrypt,
            ["TrustServerCertificate"] = settings.TrustServerCertificate,
            ["Connect Timeout"] = 15
        };
        return builder.ConnectionString;
    }

    private static DbConnection CreateConnection(string connectionString)
    {
        var connectionType = ResolveSqlConnectionType();
        if (connectionType == null)
        {
            throw new InvalidOperationException(
                "MSSQL provider bulunamadi. Kapali ortamda build'in kirilmamasi icin SqlClient compile-time bagimliligi kaldirildi; " +
                "MSSQL entegrasyonunu calistirmak icin publish ciktisinda Microsoft.Data.SqlClient ya da System.Data.SqlClient assembly'si bulunmalidir.");
        }

        if (Activator.CreateInstance(connectionType, connectionString) is DbConnection connection)
            return connection;

        if (Activator.CreateInstance(connectionType) is DbConnection fallback)
        {
            fallback.ConnectionString = connectionString;
            return fallback;
        }

        throw new InvalidOperationException("MSSQL provider yuklendi ancak DbConnection olusturulamadi");
    }

    private static Type? ResolveSqlConnectionType()
    {
        var providerTypes = new[]
        {
            ("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlConnection"),
            ("System.Data.SqlClient", "System.Data.SqlClient.SqlConnection")
        };

        foreach (var (assemblyName, typeName) in providerTypes)
        {
            var type = Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
            if (type != null && typeof(DbConnection).IsAssignableFrom(type))
                return type;

            try
            {
                type = Assembly.Load(new AssemblyName(assemblyName)).GetType(typeName, throwOnError: false);
                if (type != null && typeof(DbConnection).IsAssignableFrom(type))
                    return type;
            }
            catch
            {
                // Provider is optional; connection tests surface a clear message when it is absent.
            }
        }

        return null;
    }

    private async Task<ExternalUserDbSettingsResponse> GetCachedSettingsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue("external-user-db:settings", out ExternalUserDbSettingsResponse? cached) && cached != null)
            return cached;

        var settings = await GetSettingsAsync(ct);
        _cache.Set("external-user-db:settings", settings, TimeSpan.FromMinutes(5));
        return settings;
    }

    private async Task<Dictionary<string, SystemSetting>> LoadAsync(CancellationToken ct)
    {
        return await _context.SystemSettings
            .AsNoTracking()
            .Where(s => s.Key.StartsWith(Prefix))
            .ToDictionaryAsync(s => s.Key, s => s, ct);
    }

    private async Task UpsertAsync(string key, string value, CancellationToken ct)
    {
        var entity = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (entity == null)
        {
            _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            entity.Value = value;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<string?> GetSecretAsync(CancellationToken ct)
    {
        var setting = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == PasswordKey, ct);
        if (setting == null || string.IsNullOrWhiteSpace(setting.Value)) return null;

        try
        {
            return _protector.Unprotect(setting.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not decrypt external user DB password");
            return null;
        }
    }

    private static ExternalUserDbSettingsResponse BuildResponse(Dictionary<string, SystemSetting> dict)
    {
        var host = Get(dict, HostKey);
        var database = Get(dict, DatabaseKey);
        var username = Get(dict, UsernameKey);
        var tableName = Get(dict, TableNameKey);
        var matchColumn = Get(dict, MatchColumnKey, "username");
        var emailColumn = Get(dict, EmailColumnKey, "email");
        var passwordSet = dict.ContainsKey(PasswordKey) && !string.IsNullOrWhiteSpace(dict[PasswordKey].Value);

        return new ExternalUserDbSettingsResponse
        {
            Enabled = Bool(Get(dict, EnabledKey), false),
            Host = host,
            Port = Int(Get(dict, PortKey), 1433),
            Database = database,
            Username = username,
            Password = null,
            PasswordSet = passwordSet,
            Encrypt = Bool(Get(dict, EncryptKey), true),
            TrustServerCertificate = Bool(Get(dict, TrustServerCertificateKey), true),
            TableName = tableName,
            MatchColumn = matchColumn,
            FirstNameColumn = Get(dict, FirstNameColumnKey),
            LastNameColumn = Get(dict, LastNameColumnKey),
            FullNameColumn = Get(dict, FullNameColumnKey),
            EmailColumn = emailColumn,
            DepartmentColumn = Get(dict, DepartmentColumnKey),
            WhereClause = Get(dict, WhereClauseKey),
            IsConfigured = !string.IsNullOrWhiteSpace(host) &&
                           !string.IsNullOrWhiteSpace(database) &&
                           !string.IsNullOrWhiteSpace(username) &&
                           passwordSet &&
                           !string.IsNullOrWhiteSpace(tableName) &&
                           !string.IsNullOrWhiteSpace(matchColumn) &&
                           !string.IsNullOrWhiteSpace(emailColumn),
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };
    }

    private static void ValidateSettings(ExternalUserDbSettingsRequest request, bool allowEmptyPassword)
    {
        if (string.IsNullOrWhiteSpace(request.Host)) throw new ArgumentException("MSSQL sunucu adresi zorunludur");
        if (request.Port is < 1 or > 65535) throw new ArgumentException("MSSQL port 1 ile 65535 arasinda olmalidir");
        if (string.IsNullOrWhiteSpace(request.Database)) throw new ArgumentException("MSSQL database adi zorunludur");
        if (string.IsNullOrWhiteSpace(request.Username)) throw new ArgumentException("MSSQL kullanici adi zorunludur");
        if (!allowEmptyPassword && string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("MSSQL sifresi zorunludur");
        if (string.IsNullOrWhiteSpace(request.TableName)) throw new ArgumentException("Tablo veya view adi zorunludur");
        if (string.IsNullOrWhiteSpace(request.MatchColumn)) throw new ArgumentException("Eslesme kolonu zorunludur");
        if (string.IsNullOrWhiteSpace(request.EmailColumn)) throw new ArgumentException("Email kolonu zorunludur");

        _ = QuoteMultipartIdentifier(request.TableName);
        _ = QuoteIdentifier(request.MatchColumn);
        ValidateOptionalIdentifier(request.FirstNameColumn);
        ValidateOptionalIdentifier(request.LastNameColumn);
        ValidateOptionalIdentifier(request.FullNameColumn);
        ValidateOptionalIdentifier(request.EmailColumn);
        ValidateOptionalIdentifier(request.DepartmentColumn);
        _ = SafeWhereClause(request.WhereClause);
    }

    private static void ValidateOptionalIdentifier(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) _ = QuoteIdentifier(value);
    }

    private static string QuoteMultipartIdentifier(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 3)
            throw new ArgumentException("Tablo/view adi gecersiz");
        return string.Join(".", parts.Select(QuoteIdentifier));
    }

    private static string QuoteIdentifier(string value)
    {
        var clean = value.Trim().Trim('[', ']');
        if (!IdentifierPart.IsMatch(clean))
            throw new ArgumentException($"Kolon veya tablo adi gecersiz: {value}");
        return $"[{clean.Replace("]", "]]")}]";
    }

    private static string SafeWhereClause(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Contains(';') || trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/"))
            throw new ArgumentException("WHERE filtresi tek bir kosul olmali; ; veya yorum karakterleri kullanilamaz");
        return trimmed;
    }

    private static string? NormalizeUserName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        var slash = normalized.LastIndexOf('\\');
        if (slash >= 0 && slash + 1 < normalized.Length) normalized = normalized[(slash + 1)..];
        var at = normalized.IndexOf('@');
        if (at > 0) normalized = normalized[..at];
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? Read(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
    }

    private static ExternalUserLookupResult Result(bool success, string message, ExternalUserProfileDto? user = null) => new()
    {
        Success = success,
        Message = message,
        User = user,
        TestedAt = DateTime.UtcNow
    };

    private static string Get(Dictionary<string, SystemSetting> dict, string key, string fallback = "") =>
        dict.TryGetValue(key, out var value) ? value.Value : fallback;

    private static bool Bool(string value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static int Int(string value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;
}
