using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Options;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Helpers;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Text;

namespace DLP.RiskAnalyzer.Analyzer.Extensions;

/// <summary>
/// Extension methods for organizing DI registrations in Program.cs
/// Groups related service registrations for better readability and maintainability.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all repository implementations
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<Repositories.Interfaces.IIncidentRepository,
            Repositories.Implementations.IncidentRepository>();
        services.AddScoped<Repositories.Interfaces.IAIAnalysisRepository,
            Repositories.Implementations.AIAnalysisRepository>();
        services.AddScoped<Repositories.Interfaces.IUserDailyRiskScoreRepository,
            Repositories.Implementations.UserDailyRiskScoreRepository>();

        return services;
    }

    /// <summary>
    /// Register all domain/business services
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Core analysis services
        services.AddScoped<IBehaviorMetricsCalculator, BehaviorMetricsCalculator>();
        services.AddScoped<IBehaviorAIExplanationService, BehaviorAIExplanationService>();
        services.AddScoped<IBehaviorEngineService, BehaviorEngineService>();
        services.AddScoped<IRiskAnalyzerService, RiskAnalyzerService>();
        services.AddScoped<IUserInsightsService, UserInsightsService>();
        services.AddScoped<IAnomalyDetector, AnomalyDetector>();
        services.AddScoped<DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer>();

        // Stream processors
        services.AddScoped<IRedisStreamProcessor, RedisStreamProcessor>();
        services.AddScoped<IReleasedIncidentProcessor, ReleasedIncidentProcessor>();

        // Data services
        services.AddScoped<IDatabaseService, DatabaseService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IReportGeneratorService, ReportGeneratorService>();
        services.AddScoped<ClassificationService>();

        // Sync services
        services.AddScoped<PolicyExceptionSyncService>();
        services.AddScoped<ReleasedIncidentSyncService>();

        // Configuration services
        services.AddScoped<DlpConfigurationService>();
        services.AddScoped<EmailConfigurationService>();
        services.AddScoped<EmailService>();
        services.AddScoped<AuditLogService>();

        // Dev-only seeder — harmless singleton when SeedData:Enabled = false
        services.AddScoped<DevDataSeeder>();

        return services;
    }

    /// <summary>
    /// Register HTTP clients for external API integrations
    /// </summary>
    public static IServiceCollection AddExternalHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<SplunkService>();
        services.AddHttpClient<RemediationService>();
        services.AddHttpClient<OpenAIService>();
        services.AddHttpClient<CopilotService>();
        services.AddHttpClient<AzureOpenAIService>();
        services.AddHttpClient<PolicyService>(client => { });
        services.AddHttpClient<RemediationService>(client => { });

        return services;
    }

    /// <summary>
    /// Register infrastructure services: Database, Redis, DataProtection, MemoryCache
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database (PostgreSQL via EF Core)
        services.AddDbContext<AnalyzerDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            connectionString = EnvironmentHelper.GetDatabaseConnectionString(connectionString ?? string.Empty);

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(60);
            });

            options.EnableServiceProviderCaching();
            options.EnableSensitiveDataLogging(false);
        });

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisHost = configuration["Redis:Host"] ?? "localhost";
            var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
            var redisPassword = configuration["Redis:Password"];

            var connectionString = EnvironmentHelper.GetRedisConnectionString(redisHost, redisPort);

            var config = new ConfigurationOptions
            {
                EndPoints = { connectionString },
                ConnectTimeout = 10000,
                SyncTimeout = 5000,
                AbortOnConnectFail = false,
                ReconnectRetryPolicy = new ExponentialRetry(1000),
                ConnectRetry = 3
            };

            if (!string.IsNullOrEmpty(redisPassword))
            {
                config.Password = redisPassword;
            }

            return ConnectionMultiplexer.Connect(config);
        });

        // DataProtection & Caching
        services.AddDataProtection();
        services.AddMemoryCache();

        // ── Health Checks ────────────────────────────────────────────────────────
        var dbConnectionString = configuration.GetConnectionString("DefaultConnection");
        dbConnectionString = EnvironmentHelper.GetDatabaseConnectionString(dbConnectionString ?? string.Empty);

        var redisHost = configuration["Redis:Host"] ?? "localhost";
        var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
        var redisPassword = configuration["Redis:Password"];
        var redisConnectionString = EnvironmentHelper.GetRedisConnectionString(redisHost, redisPort);
        
        if (!string.IsNullOrEmpty(redisPassword))
        {
            redisConnectionString += $",password={redisPassword}";
        }

        services.AddHealthChecks()
            .AddNpgSql(dbConnectionString, name: "Database")
            .AddRedis(redisConnectionString, name: "Redis");

        return services;
    }

    /// <summary>
    /// Configure JWT Authentication
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSecretKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SecretKey configuration is required. " +
                "Set it in appsettings.json or as an environment variable: Jwt__SecretKey");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "DLP-RiskAnalyzer";
        var jwtAudience = configuration["Jwt:Audience"] ?? "DLP-RiskAnalyzer-Client";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };
        });

        return services;
    }

    /// <summary>
    /// Configure CORS policy with internal network support
    /// </summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            allowedOrigins = new[]
            {
                "http://localhost:3000",
                "http://localhost:3001",
                "http://localhost:3002"
            };
        }

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowInternalNetwork = configuration.GetValue<bool>("Cors:AllowInternalNetwork", false);

                if (environment.IsDevelopment() || allowInternalNetwork)
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        try
                        {
                            if (origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
                                return true;

                            var uri = new Uri(origin);
                            var port = uri.Port;
                            if (port == 3000 || port == 3001 || port == 3002)
                            {
                                var host = uri.Host;
                                if (System.Net.IPAddress.TryParse(host, out _))
                                    return true;
                                if (host == "localhost" || host == "127.0.0.1")
                                    return true;
                            }
                            return false;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()
                      .WithExposedHeaders("Content-Disposition");
            });
        });

        return services;
    }
}
