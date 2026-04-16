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
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

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
        services.AddScoped<IRiskScoringService, RiskScoringService>();
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
        services.AddScoped<IClassificationService, ClassificationService>();

        // Sync services
        services.AddScoped<IPolicyExceptionSyncService, PolicyExceptionSyncService>();
        services.AddScoped<IReleasedIncidentSyncService, ReleasedIncidentSyncService>();

        // Configuration services
        services.AddScoped<IDlpConfigurationService, DlpConfigurationService>();
        services.AddScoped<IDlpTestService, DlpTestService>();
        services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Dev-only seeder — harmless singleton when SeedData:Enabled = false
        services.AddScoped<IDevDataSeeder, DevDataSeeder>();

        return services;
    }

    /// <summary>
    /// Register HTTP clients for external API integrations
    /// </summary>
    public static IServiceCollection AddExternalHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<ISplunkService, SplunkService>();
        services.AddHttpClient<IRemediationService, RemediationService>();
        services.AddHttpClient<IOpenAIService, OpenAIService>();
        services.AddHttpClient<ICopilotService, CopilotService>();
        services.AddHttpClient<IAzureOpenAIService, AzureOpenAIService>();
        services.AddHttpClient<IPolicyService, PolicyService>();

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

    /// <summary>
    /// Configure Rate Limiting policies for login and API endpoints (§40)
    /// </summary>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login endpoint: 5 requests per 5 minutes per IP
            options.AddFixedWindowLimiter("login", opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(5);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // General API: 100 requests per 1 minute per IP
            options.AddFixedWindowLimiter("api", opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 2;
            });

            // Global limiter fallback per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        return services;
    }
}
