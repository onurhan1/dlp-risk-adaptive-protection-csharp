using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ExternalUserDirectoryService : IExternalUserDirectoryService
{
    private const string Prefix = "external_user_db_";
    private const string EnabledKey = Prefix + "enabled";
    private const string ProviderKey = Prefix + "provider";
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
    private const string LookupSqlKey = Prefix + "lookup_sql";

    private static readonly Regex IdentifierPart = new(@"^[A-Za-z_][A-Za-z0-9_ ]*$", RegexOptions.Compiled);
    private const string MssqlManagedUserLookupSql = """
WITH MDR AS (
    SELECT DISTINCT
        P.PersonId,
        P.EmployeeNumber,
        P.FirstName,
        P.LastName,
        P.FirstName + ' ' + P.LastName AS AdSoyad,
        P.CorporateEmail,
        P.Sex,
        BU.UserCode,
        P.PersonTypeId,
        P.EmployeeTypeId,
        O.OrganizationId,
        O.OrganizationName,

        PSupervisor.PersonId AS SupervisorPersonId,
        PSupervisor.EmployeeNumber AS SupervisorEmployeeNumber,
        PSupervisor.FirstName + ' ' + PSupervisor.LastName AS SupervisorFullName,
        BUSupervisor.UserCode AS SupervisorUserCode,
        PSupervisor.CorporateEmail AS SupervisorMail,

        CASE
            WHEN J2.Level >= 70 THEN PSupervisor.PersonId
            ELSE PManager.PersonId
        END AS ManagerPersonId,

        CASE
            WHEN J2.Level >= 70 THEN PSupervisor.EmployeeNumber
            ELSE PManager.EmployeeNumber
        END AS ManagerEmployeeNumber,

        CASE
            WHEN J2.Level >= 70 THEN PSupervisor.FirstName + ' ' + PSupervisor.LastName
            ELSE PManager.FirstName + ' ' + PManager.LastName
        END AS ManagerFullName

    FROM [Veritabani].[sema].[PersonTable] P WITH (NOLOCK)

    INNER JOIN [Veritabani].[sema].[AssignmentTable] A WITH (NOLOCK)
        ON A.PersonID = P.PersonId

    INNER JOIN [Veritabani].[sema].[OrganizationTable] O WITH (NOLOCK)
        ON O.OrganizationId = A.OrganizationId

    LEFT JOIN [Veritabani].[sema].[BusinessUserTable] BU WITH (NOLOCK)
        ON BU.USerId = P.EmployeeNumber

    LEFT JOIN [Veritabani].[sema].[PersonTable] PSupervisor WITH (NOLOCK)
        ON PSupervisor.PersonId = A.SuperVisorId
        AND CAST(GETDATE() AS DATE) BETWEEN PSupervisor.EffectiveStartDate AND PSupervisor.EffectiveEndDate

    LEFT JOIN [Veritabani].[sema].[AssignmentTable] A2 WITH (NOLOCK)
        ON A2.PersonID = A.SuperVisorId
        AND A2.IsPrimary = 1
        AND A2.AssignmentStatusTypeId IN (1,76)
        AND A2.AssignmentType IN ('E', 'C')
        AND CAST(GETDATE() AS DATE) BETWEEN A2.EffectiveStartDate AND A2.EffectiveEndDate

    LEFT JOIN [Veritabani].[sema].[JobTable] J2 WITH (NOLOCK)
        ON J2.JobId = A2.JobId

    LEFT JOIN [Veritabani].[sema].[BusinessUserTable] BUSupervisor WITH (NOLOCK)
        ON BUSupervisor.USerId = PSupervisor.EmployeeNumber

    LEFT JOIN [Veritabani].[sema].[PersonTable] PManager WITH (NOLOCK)
        ON PManager.PersonId = A2.SuperVisorId
        AND CAST(GETDATE() AS DATE) BETWEEN PManager.EffectiveStartDate AND PManager.EffectiveEndDate

    WHERE
        P.PersonTypeId IN (1,2)
        AND CAST(GETDATE() AS DATE) BETWEEN P.EffectiveStartDate AND P.EffectiveEndDate
        AND A.IsPrimary = 1
        AND A.AssignmentStatusTypeId IN (1,76)
        AND A.AssignmentType IN ('E', 'C')
        AND CAST(GETDATE() AS DATE) BETWEEN A.EffectiveStartDate AND A.EffectiveEndDate
)

SELECT TOP (1)
    MDR.UserCode AS user_name,
    MDR.EmployeeNumber AS employee_number,
    MDR.FirstName AS first_name,
    MDR.LastName AS last_name,
    MDR.AdSoyad AS full_name,
    MDR.CorporateEmail AS email,
    MDR.Sex AS gender,
    MDR.OrganizationName AS department,
    MDR.OrganizationName AS organization_name,
    MDR.SupervisorUserCode AS supervisor_user_name,
    MDR.SupervisorFullName AS supervisor_full_name,
    MDR.SupervisorMail AS supervisor_email,
    BUManager.UserCode AS manager_user_name,
    MDR.ManagerFullName AS manager_full_name,
    PManager.CorporateEmail AS manager_email
FROM MDR
LEFT JOIN [Veritabani].[sema].[BusinessUserTable] BUManager WITH (NOLOCK)
    ON BUManager.USerId = MDR.ManagerEmployeeNumber
LEFT JOIN [Veritabani].[sema].[PersonTable] PManager WITH (NOLOCK)
    ON PManager.PersonId = MDR.ManagerPersonId
    AND CAST(GETDATE() AS DATE) BETWEEN PManager.EffectiveStartDate AND PManager.EffectiveEndDate
WHERE
    MDR.UserCode = @username
    OR MDR.EmployeeNumber = @username
""";

    private readonly AnalyzerDbContext _context;
    private readonly IDataProtector _protector;
    private readonly IMemoryCache _cache;
    private readonly IDirectorySettingsService _directorySettingsService;
    private readonly ILogger<ExternalUserDirectoryService> _logger;

    public ExternalUserDirectoryService(
        AnalyzerDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        IMemoryCache cache,
        IDirectorySettingsService directorySettingsService,
        ILogger<ExternalUserDirectoryService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("ExternalUserDb.SettingsProtector");
        _cache = cache;
        _directorySettingsService = directorySettingsService;
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
        await UpsertAsync(ProviderKey, NormalizeProvider(request.Provider), ct);
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
        await UpsertAsync(LookupSqlKey, request.LookupSql.Trim(), ct);

        if (!string.IsNullOrWhiteSpace(request.Password))
            await UpsertAsync(PasswordKey, _protector.Protect(request.Password), ct);

        _cache.Remove("external-user-db:settings");
        return await GetSettingsAsync(ct);
    }

    public async Task<ExternalUserLookupResult> TestConnectionAsync(ExternalUserDbSettingsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            request.Password = await GetSecretAsync(ct);

        ValidateSettings(request, allowEmptyPassword: false, validateLookupSql: false);

        try
        {
            await using var connection = CreateConnection(request);
            await connection.OpenAsync(ct);

            return Result(true, $"{DbDisplayName(request)} baglantisi basarili");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External user DB connection test failed");
            return Result(false, $"{DbDisplayName(request)} baglanti testi basarisiz: {UserFacingExceptionMessage(ex)}");
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
            return Result(false, $"{DbDisplayName(request)} kullanici testi basarisiz: {UserFacingExceptionMessage(ex)}");
        }
    }

    public async Task<ExternalUserProfileDto?> ResolveUserAsync(string? userName, CancellationToken ct = default)
    {
        var normalized = NormalizeUserName(userName);
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var cacheKey = $"external-user-directory:user:{normalized.ToLowerInvariant()}";
        if (_cache.TryGetValue(cacheKey, out ExternalUserProfileDto? cached)) return cached;

        ExternalUserProfileDto? user = null;
        try
        {
            var settings = await GetCachedSettingsAsync(ct);
            if (settings.Enabled && settings.IsConfigured)
            {
                settings.Password = await GetSecretAsync(ct);
                user = await LookupAsync(settings, normalized, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External user DB lookup failed for {UserName}", normalized);
        }

        user ??= await ResolveLdapUserAsync(normalized, ct);
        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(user == null ? 5 : 30));
        return user;
    }

    private async Task<ExternalUserProfileDto?> ResolveLdapUserAsync(string userName, CancellationToken ct)
    {
        try
        {
            var ldap = await _directorySettingsService.LookupLdapUserAsync(userName, ct);
            if (!ldap.Success) return null;

            return new ExternalUserProfileDto
            {
                UserName = ldap.Username,
                FirstName = ldap.FirstName,
                LastName = ldap.LastName,
                FullName = ldap.FullName,
                Email = ldap.Email,
                Department = ldap.Department,
                Gender = ldap.Gender
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LDAP user lookup fallback failed for {UserName}", userName);
            return null;
        }
    }

    private async Task<ExternalUserProfileDto?> LookupAsync(ExternalUserDbSettingsRequest settings, string userName, CancellationToken ct)
    {
        await using var connection = CreateConnection(settings);
        await connection.OpenAsync(ct);

        return await ReadUserProfileAsync(connection, settings, userName, ct);
    }

    private static async Task<ExternalUserProfileDto?> ReadUserProfileAsync(DbConnection connection, ExternalUserDbSettingsRequest settings, string userName, CancellationToken ct)
    {
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

        var firstName = TryRead(reader, "first_name");
        var lastName = TryRead(reader, "last_name");
        var fullName = TryRead(reader, "full_name");
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = string.Join(" ", new[] { firstName, lastName }.Where(v => !string.IsNullOrWhiteSpace(v)));

        return new ExternalUserProfileDto
        {
            UserName = TryRead(reader, "user_name") ?? userName,
            EmployeeNumber = TryRead(reader, "employee_number"),
            FirstName = firstName,
            LastName = lastName,
            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Email = TryRead(reader, "email"),
            Department = TryRead(reader, "department"),
            Gender = FirstNonEmpty(TryRead(reader, "gender"), TryRead(reader, "sex")),
            OrganizationName = TryRead(reader, "organization_name"),
            ManagerUserName = TryRead(reader, "manager_user_name"),
            ManagerFullName = TryRead(reader, "manager_full_name"),
            ManagerEmail = TryRead(reader, "manager_email"),
            SupervisorUserName = TryRead(reader, "supervisor_user_name"),
            SupervisorFullName = TryRead(reader, "supervisor_full_name"),
            SupervisorEmail = TryRead(reader, "supervisor_email")
        };
    }

    private static string BuildLookupSql(ExternalUserDbSettingsRequest settings)
    {
        var provider = NormalizeProvider(settings.Provider);
        if (provider == "mssql")
        {
            return string.IsNullOrWhiteSpace(settings.LookupSql)
                ? MssqlManagedUserLookupSql
                : settings.LookupSql.Trim();
        }

        var projections = new List<string>
        {
            $"{QuoteIdentifier(settings.MatchColumn, provider)} AS {QuoteIdentifier("user_name", provider)}"
        };
        AddProjection(projections, settings.FirstNameColumn, "first_name", provider);
        AddProjection(projections, settings.LastNameColumn, "last_name", provider);
        AddProjection(projections, settings.FullNameColumn, "full_name", provider);
        AddProjection(projections, settings.EmailColumn, "email", provider);
        AddProjection(projections, settings.DepartmentColumn, "department", provider);

        var where = $"{QuoteIdentifier(settings.MatchColumn, provider)} = @username";
        if (!string.IsNullOrWhiteSpace(settings.WhereClause))
            where += $" AND ({SafeWhereClause(settings.WhereClause)})";

        var tableName = QuoteMultipartIdentifier(settings.TableName, provider);
        return $"SELECT {string.Join(", ", projections)} FROM {tableName} WHERE {where} LIMIT 1";
    }

    private static void AddProjection(List<string> projections, string column, string alias, string provider)
    {
        if (!string.IsNullOrWhiteSpace(column))
            projections.Add($"{QuoteIdentifier(column, provider)} AS {QuoteIdentifier(alias, provider)}");
        else
            projections.Add(provider == "mssql"
                ? $"CAST(NULL AS nvarchar(4000)) AS {QuoteIdentifier(alias, provider)}"
                : $"NULL::text AS {QuoteIdentifier(alias, provider)}");
    }

    private static string BuildConnectionString(ExternalUserDbSettingsRequest settings)
    {
        if (NormalizeProvider(settings.Provider) == "postgresql")
        {
            var npgsqlBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = settings.Host.Trim(),
                Port = settings.Port,
                Database = settings.Database.Trim(),
                Username = settings.Username.Trim(),
                Password = settings.Password ?? string.Empty,
                SslMode = settings.Encrypt ? SslMode.Require : SslMode.Disable,
                Timeout = 15
            };
            npgsqlBuilder["Trust Server Certificate"] = settings.TrustServerCertificate;
            return npgsqlBuilder.ConnectionString;
        }

        var builder = new DbConnectionStringBuilder
        {
            ["Data Source"] = $"{settings.Host.Trim()},{settings.Port}",
            ["User ID"] = settings.Username.Trim(),
            ["Password"] = settings.Password ?? string.Empty,
            ["Encrypt"] = settings.Encrypt ? "True" : "False",
            ["Trust Server Certificate"] = settings.TrustServerCertificate ? "True" : "False",
            ["Connect Timeout"] = 15
        };

        if (!string.IsNullOrWhiteSpace(settings.Database))
            builder["Initial Catalog"] = settings.Database.Trim();
        return builder.ConnectionString;
    }

    private static DbConnection CreateConnection(ExternalUserDbSettingsRequest settings)
    {
        var provider = NormalizeProvider(settings.Provider);
        var connectionString = BuildConnectionString(settings);
        return provider == "postgresql"
            ? new NpgsqlConnection(connectionString)
            : CreateSqlServerConnection(connectionString);
    }

    private static DbConnection CreateSqlServerConnection(string connectionString)
    {
        var errors = new List<string>();

        foreach (var (connectionType, source) in ResolveSqlConnectionTypes())
        {
            try
            {
                if (Activator.CreateInstance(connectionType) is DbConnection connection)
                {
                    connection.ConnectionString = connectionString;
                    return connection;
                }

                if (Activator.CreateInstance(connectionType, connectionString) is DbConnection fallback)
                    return fallback;

                errors.Add($"{source}: DbConnection olusturulamadi");
            }
            catch (Exception ex) when (IsPlatformNotSupported(ex))
            {
                errors.Add($"{source}: {UserFacingExceptionMessage(ex)}");
            }
            catch (Exception ex) when (IsMissingAssembly(ex))
            {
                errors.Add($"{source}: eksik assembly - {UserFacingExceptionMessage(ex)}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"MSSQL provider yuklendi ancak baglanti olusturulamadi: {UserFacingExceptionMessage(ex)}", UnwrapException(ex));
            }
        }

        var detail = errors.Count > 0 ? " Denenen adaylar: " + string.Join(" | ", errors.Distinct()) : "";
        throw new InvalidOperationException(
            "MSSQL provider bulunamadi veya bu platform icin uygun SqlClient assembly'si yuklenemedi. " +
            "Microsoft.Data.SqlClient.dll ile runtimes\\win\\lib\\net8.0\\Microsoft.Data.SqlClient.dll ve " +
            "runtimes\\win-x64\\native\\Microsoft.Data.SqlClient.SNI.dll dosyalarinin calisan uygulama klasorunde oldugundan emin olun." +
            detail);
    }

    private static IEnumerable<(Type Type, string Source)> ResolveSqlConnectionTypes()
    {
        var providerTypes = new[]
        {
            ("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlConnection"),
            ("System.Data.SqlClient", "System.Data.SqlClient.SqlConnection")
        };

        foreach (var (assemblyName, typeName) in providerTypes)
        {
            foreach (var assembly in LoadSqlClientAssemblies(assemblyName))
            {
                var type = assembly.GetType(typeName, throwOnError: false);
                if (type != null && typeof(DbConnection).IsAssignableFrom(type))
                    yield return (type, assembly.Location);
            }

            Type? loadedType = null;
            try
            {
                loadedType = Type.GetType($"{typeName}, {assemblyName}", throwOnError: false);
            }
            catch
            {
                // Provider is optional; connection tests surface a clear message when it is absent.
            }

            if (loadedType != null && typeof(DbConnection).IsAssignableFrom(loadedType))
                yield return (loadedType, assemblyName);

            loadedType = null;
            try
            {
                loadedType = Assembly.Load(new AssemblyName(assemblyName)).GetType(typeName, throwOnError: false);
            }
            catch
            {
                // Provider is optional; connection tests surface a clear message when it is absent.
            }

            if (loadedType != null && typeof(DbConnection).IsAssignableFrom(loadedType))
                yield return (loadedType, assemblyName);
        }
    }

    private static IEnumerable<Assembly> LoadSqlClientAssemblies(string assemblyName)
    {
        foreach (var path in SqlClientAssemblyCandidates(assemblyName))
        {
            if (!File.Exists(path)) continue;

            Assembly? assembly = null;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch
            {
                // Keep probing; the final connection test returns the actionable error.
            }

            if (assembly != null) yield return assembly;
        }
    }

    private static IEnumerable<string> SqlClientAssemblyCandidates(string assemblyName)
    {
        var baseDir = AppContext.BaseDirectory;
        var runtimesDir = Path.Combine(baseDir, "runtimes");
        var preferredRuntime = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"{Path.DirectorySeparatorChar}win{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}"
            : $"{Path.DirectorySeparatorChar}unix{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}";

        if (Directory.Exists(runtimesDir))
        {
            foreach (var path in Directory.GetFiles(runtimesDir, $"{assemblyName}.dll", SearchOption.AllDirectories)
                         .Where(p => p.Contains(preferredRuntime, StringComparison.OrdinalIgnoreCase) ||
                                     (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                                      p.Contains($"{Path.DirectorySeparatorChar}win-", StringComparison.OrdinalIgnoreCase) &&
                                      p.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
                         .OrderByDescending(p => p.Contains("net8.0", StringComparison.OrdinalIgnoreCase))
                         .ThenBy(p => p.Length))
            {
                yield return path;
            }
        }

        yield return Path.Combine(baseDir, $"{assemblyName}.dll");

        if (Directory.Exists(runtimesDir))
        {
            foreach (var path in Directory.GetFiles(runtimesDir, $"{assemblyName}.dll", SearchOption.AllDirectories)
                         .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                     !p.Contains($"{Path.DirectorySeparatorChar}unix{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                yield return path;
            }
        }
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
        var provider = NormalizeProvider(Get(dict, ProviderKey, "postgresql"));
        var database = Get(dict, DatabaseKey);
        var username = Get(dict, UsernameKey);
        var tableName = Get(dict, TableNameKey);
        var matchColumn = Get(dict, MatchColumnKey, "username");
        var emailColumn = Get(dict, EmailColumnKey, "email");
        var lookupSql = Get(dict, LookupSqlKey);
        var passwordSet = dict.ContainsKey(PasswordKey) && !string.IsNullOrWhiteSpace(dict[PasswordKey].Value);
        var hasConnectionSettings = !string.IsNullOrWhiteSpace(host) &&
                                    (provider == "mssql" || !string.IsNullOrWhiteSpace(database)) &&
                                    !string.IsNullOrWhiteSpace(username) &&
                                    passwordSet;
        var hasLookupSettings = provider == "mssql" ||
                                (!string.IsNullOrWhiteSpace(tableName) &&
                                 !string.IsNullOrWhiteSpace(matchColumn) &&
                                 !string.IsNullOrWhiteSpace(emailColumn));

        return new ExternalUserDbSettingsResponse
        {
            Enabled = Bool(Get(dict, EnabledKey), false),
            Provider = provider,
            Host = host,
            Port = Int(Get(dict, PortKey), provider == "mssql" ? 1433 : 5432),
            Database = database,
            Username = username,
            Password = null,
            PasswordSet = passwordSet,
            Encrypt = Bool(Get(dict, EncryptKey), provider == "mssql"),
            TrustServerCertificate = Bool(Get(dict, TrustServerCertificateKey), true),
            TableName = tableName,
            MatchColumn = matchColumn,
            FirstNameColumn = Get(dict, FirstNameColumnKey),
            LastNameColumn = Get(dict, LastNameColumnKey),
            FullNameColumn = Get(dict, FullNameColumnKey),
            EmailColumn = emailColumn,
            DepartmentColumn = Get(dict, DepartmentColumnKey),
            WhereClause = Get(dict, WhereClauseKey),
            LookupSql = lookupSql,
            IsConfigured = hasConnectionSettings && hasLookupSettings,
            UpdatedAt = dict.Values.OrderByDescending(s => s.UpdatedAt).FirstOrDefault()?.UpdatedAt
        };
    }

    private static void ValidateSettings(
        ExternalUserDbSettingsRequest request,
        bool allowEmptyPassword,
        bool validateLookupSql = true)
    {
        var provider = NormalizeProvider(request.Provider);
        if (string.IsNullOrWhiteSpace(request.Host)) throw new ArgumentException($"{DbDisplayName(provider)} sunucu adresi zorunludur");
        if (request.Port is < 1 or > 65535) throw new ArgumentException($"{DbDisplayName(provider)} port 1 ile 65535 arasinda olmalidir");
        if (provider == "postgresql" && string.IsNullOrWhiteSpace(request.Database)) throw new ArgumentException($"{DbDisplayName(provider)} database adi zorunludur");
        if (string.IsNullOrWhiteSpace(request.Username)) throw new ArgumentException($"{DbDisplayName(provider)} kullanici adi zorunludur");
        if (!allowEmptyPassword && string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException($"{DbDisplayName(provider)} sifresi zorunludur");
        if (provider == "mssql")
        {
            if (validateLookupSql) ValidateLookupSql(request.LookupSql);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.TableName)) throw new ArgumentException("Tablo veya view adi zorunludur");
        if (string.IsNullOrWhiteSpace(request.MatchColumn)) throw new ArgumentException("Eslesme kolonu zorunludur");
        if (string.IsNullOrWhiteSpace(request.EmailColumn)) throw new ArgumentException("Email kolonu zorunludur");

        _ = QuoteMultipartIdentifier(request.TableName, provider);
        _ = QuoteIdentifier(request.MatchColumn, provider);
        ValidateOptionalIdentifier(request.FirstNameColumn, provider);
        ValidateOptionalIdentifier(request.LastNameColumn, provider);
        ValidateOptionalIdentifier(request.FullNameColumn, provider);
        ValidateOptionalIdentifier(request.EmailColumn, provider);
        ValidateOptionalIdentifier(request.DepartmentColumn, provider);
        _ = SafeWhereClause(request.WhereClause);
    }

    private static void ValidateOptionalIdentifier(string value, string provider)
    {
        if (!string.IsNullOrWhiteSpace(value)) _ = QuoteIdentifier(value, provider);
    }

    private static string QuoteMultipartIdentifier(string value, string provider)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || parts.Length > 3)
            throw new ArgumentException("Tablo/view adi gecersiz");
        return string.Join(".", parts.Select(part => QuoteIdentifier(part, provider)));
    }

    private static string QuoteIdentifier(string value, string provider)
    {
        var clean = value.Trim().Trim('[', ']', '"');
        if (!IdentifierPart.IsMatch(clean))
            throw new ArgumentException($"Kolon veya tablo adi gecersiz: {value}");
        return provider == "mssql"
            ? $"[{clean.Replace("]", "]]")}]"
            : $"\"{clean.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizeProvider(string? value)
    {
        var provider = (value ?? "postgresql").Trim().ToLowerInvariant();
        return provider switch
        {
            "postgres" or "postgresql" or "pgsql" => "postgresql",
            "sqlserver" or "sql-server" or "mssql" => "mssql",
            _ => throw new ArgumentException("Veritabani tipi postgresql veya mssql olmalidir")
        };
    }

    private static string DbDisplayName(ExternalUserDbSettingsRequest settings) => DbDisplayName(NormalizeProvider(settings.Provider));

    private static string DbDisplayName(string provider) => provider == "mssql" ? "MSSQL" : "PostgreSQL";

    private static string SafeWhereClause(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Contains(';') || trimmed.Contains("--") || trimmed.Contains("/*") || trimmed.Contains("*/"))
            throw new ArgumentException("WHERE filtresi tek bir kosul olmali; ; veya yorum karakterleri kullanilamaz");
        return trimmed;
    }

    private static void ValidateLookupSql(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var trimmed = value.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MSSQL kullanici sorgusu SELECT veya WITH ile baslamalidir");

        if (!Regex.IsMatch(value, @"@\busername\b", RegexOptions.IgnoreCase))
            throw new ArgumentException("MSSQL kullanici sorgusunda @username parametresi bulunmalidir");
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

    private static string? TryRead(DbDataReader reader, string name)
    {
        int ordinal;
        try
        {
            ordinal = reader.GetOrdinal(name);
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }

        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string UserFacingExceptionMessage(Exception ex)
    {
        var root = UnwrapException(ex);
        return string.IsNullOrWhiteSpace(root.Message) ? ex.Message : root.Message;
    }

    private static bool IsPlatformNotSupported(Exception ex)
    {
        while (true)
        {
            if (ex is PlatformNotSupportedException) return true;
            if (ex.InnerException == null) return false;
            ex = ex.InnerException;
        }
    }

    private static bool IsMissingAssembly(Exception ex)
    {
        while (true)
        {
            if (ex is FileNotFoundException or FileLoadException) return true;
            if (ex.InnerException == null) return false;
            ex = ex.InnerException;
        }
    }

    private static Exception UnwrapException(Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;

        return ex.GetBaseException();
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
