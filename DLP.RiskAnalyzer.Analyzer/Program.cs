using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Options;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Helpers;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Ensure UTF-8 encoding for JSON responses
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddDataProtection();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DLP Risk Analyzer API",
        Version = "v1",
        Description = "Data Loss Prevention & Risk Analysis API"
    });
});

// Database - Using EnvironmentHelper for consistent configuration
builder.Services.AddDbContext<AnalyzerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Use EnvironmentHelper for Docker Desktop compatibility
    connectionString = EnvironmentHelper.GetDatabaseConnectionString(connectionString ?? string.Empty);
    
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Enable retry on failure (important for Docker Desktop where services may start before DB)
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
        
        // Increase command timeout for Docker Desktop (sometimes slower)
        npgsqlOptions.CommandTimeout(60);
    });
    
    // Don't fail on startup if database is not available
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(false);
});

// Redis - Using EnvironmentHelper for consistent configuration
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
{
    var redisHost = builder.Configuration["Redis:Host"] ?? "localhost";
    var redisPort = builder.Configuration.GetValue<int>("Redis:Port", 6379);
    
    // Use EnvironmentHelper for Docker Desktop compatibility
    var connectionString = EnvironmentHelper.GetRedisConnectionString(redisHost, redisPort);
    
    // Configure Redis connection with retry for Docker Desktop
    var config = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { connectionString },
        ConnectTimeout = 10000, // 10 seconds
        SyncTimeout = 5000,     // 5 seconds
        AbortOnConnectFail = false, // Don't fail on first connection attempt
        ReconnectRetryPolicy = new StackExchange.Redis.ExponentialRetry(1000), // Retry with exponential backoff
        ConnectRetry = 3 // Retry connection 3 times
    };
    
    return StackExchange.Redis.ConnectionMultiplexer.Connect(config);
});

// Services
// Repository Pattern
builder.Services.AddScoped<DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces.IIncidentRepository, 
    DLP.RiskAnalyzer.Analyzer.Repositories.Implementations.IncidentRepository>();

// Services
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<RiskAnalyzerService>();
builder.Services.AddScoped<ReportGeneratorService>();
builder.Services.AddScoped<AnomalyDetector>();
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DlpConfigurationService>();
builder.Services.AddScoped<EmailConfigurationService>();
builder.Services.AddScoped<BehaviorEngineService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddHttpClient<SplunkService>();

// OpenAI Service
builder.Services.AddHttpClient<OpenAIService>();

// GitHub Copilot Service
builder.Services.AddHttpClient<CopilotService>();

// Azure OpenAI Service
builder.Services.AddHttpClient<AzureOpenAIService>();

builder.Services.Configure<InternalApiOptions>(builder.Configuration.GetSection("InternalApi"));

// Background Services
builder.Services.AddHostedService<AnalyzerBackgroundService>();

// HTTP Clients for external APIs
builder.Services.AddHttpClient<PolicyService>(client =>
{
    // Configure in PolicyService constructor
});
builder.Services.AddHttpClient<RemediationService>(client =>
{
    // Configure in RemediationService constructor
});

// JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!ChangeThisInProduction!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DLP-RiskAnalyzer";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DLP-RiskAnalyzer-Client";

builder.Services.AddAuthentication(options =>
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

// CORS - Security: Restrict to specific origins in production
// For internal network, allow any IP address on port 3002 (dashboard)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    
if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    // Default: Allow localhost and common internal network IP ranges
    // Internal network için tüm IP'lerden erişime izin ver (sadece internal network)
    allowedOrigins = new[] 
    { 
        "http://localhost:3000", 
        "http://localhost:3001", 
        "http://localhost:3002",
        // Internal network IP ranges - will be validated by CORS policy
        // Note: For internal networks, you can use SetIsOriginAllowed to allow any IP
    };
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // For internal network, allow any origin from internal IP ranges
        // This allows access from any IP address on the internal network
        var allowInternalNetwork = builder.Configuration.GetValue<bool>("Cors:AllowInternalNetwork", false);
        
        if (builder.Environment.IsDevelopment() || allowInternalNetwork)
        {
            // Internal network için: Herhangi bir IP'den erişime izin ver
            // Sadece internal network olduğu için güvenli
            policy.SetIsOriginAllowed(origin =>
            {
                try
                {
                    // Allow localhost
                    if (origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
                        return true;
                    
                    // Allow any IP address on port 3000, 3001, or 3002 (internal network)
                    var uri = new Uri(origin);
                    var port = uri.Port;
                    if (port == 3000 || port == 3001 || port == 3002)
                    {
                        // Allow if it's an IP address (not a domain name)
                        var host = uri.Host;
                        if (System.Net.IPAddress.TryParse(host, out _))
                        {
                            return true; // It's an IP address, allow it
                        }
                        // Also allow if it's localhost or 127.0.0.1
                        if (host == "localhost" || host == "127.0.0.1")
                        {
                            return true;
                        }
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
            // Production: Use explicit origins from configuration
            policy.WithOrigins(allowedOrigins);
        }
        
        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition"); // For file downloads
    });
});

var app = builder.Build();

// Apply database migrations automatically on startup
// Can be disabled by setting "Database:AutoMigrate" to false in appsettings.json
var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate", true);
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

if (autoMigrate)
{
    startupLogger.LogInformation("=== AUTOMATIC MIGRATION ENABLED ===");
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<AnalyzerDbContext>();
            var logger = services.GetRequiredService<ILogger<Program>>();
            
            // Check database connection first
            logger.LogInformation("Checking database connection...");
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogError("Cannot connect to database. Please check connection string and ensure PostgreSQL is running.");
                throw new InvalidOperationException("Cannot connect to database. Check connection string and PostgreSQL service.");
            }
            logger.LogInformation("Database connection successful.");
            
            // Get pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Found {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), 
                    string.Join(", ", pendingMigrations));
            }
            else
            {
                logger.LogInformation("No pending migrations. Database is up to date.");
            }
            
            logger.LogInformation("Applying database migrations automatically...");
            await context.Database.MigrateAsync();
            logger.LogInformation("=== Database migrations applied successfully ===");
        }
        catch (Npgsql.NpgsqlException ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "PostgreSQL connection error during migration: {Message}. Please check: 1) PostgreSQL is running, 2) Connection string is correct, 3) Database 'dlp_analyzer' exists.", ex.Message);
            Console.WriteLine($"ERROR: Database migration failed - {ex.Message}");
            // Don't fail the application startup - allow it to continue
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while applying database migrations: {Message}. Stack trace: {StackTrace}", ex.Message, ex.StackTrace);
            Console.WriteLine($"ERROR: Database migration failed - {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't fail the application startup - allow it to continue
            // Migration errors will be visible in logs
        }
    }
}
else
{
    startupLogger.LogInformation("=== AUTOMATIC MIGRATION DISABLED ===");
    startupLogger.LogInformation("Automatic database migration is disabled. Migrations must be applied manually using 'dotnet ef database update'.");
}

// Initialize default admin user on application startup
// This ensures the user list is populated before any login attempts
// CRITICAL: This must be done before any HTTP requests are processed
startupLogger.LogInformation("=== INITIALIZING DEFAULT ADMIN USER ===");
try
{
    using (var scope = app.Services.CreateScope())
    {
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DLP.RiskAnalyzer.Analyzer.Controllers.UsersController>>();
        
        DLP.RiskAnalyzer.Analyzer.Controllers.UsersController.InitializeDefaultAdmin(configuration, logger);
        startupLogger.LogInformation("=== DEFAULT ADMIN USER INITIALIZED SUCCESSFULLY ===");
    }
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "Failed to initialize default admin user: {Message}", ex.Message);
    // Don't fail the application startup - allow it to continue
    // User initialization will be attempted when UsersController is first accessed
}

// Configure the HTTP request pipeline
app.UseRouting();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Global Exception Handling (must be early in pipeline)
app.UseMiddleware<DLP.RiskAnalyzer.Analyzer.Middleware.ExceptionHandlingMiddleware>();

// Audit logging middleware (must be after UseAuthentication to get user info)
app.UseMiddleware<DLP.RiskAnalyzer.Analyzer.Middleware.AuditLoggingMiddleware>();

// Security headers
app.Use(async (context, next) =>
{
    // Add security headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Only add CSP in production
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Content-Security-Policy", 
            "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline';");
    }
    
    await next();
});

// Swagger must be configured after UseRouting but before MapControllers
// SECURITY: Only enable Swagger in Development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLP Risk Analyzer API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

// Root endpoint - return professional HTML landing page
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>DLP Risk Analyzer API</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
            color: #333;
        }
        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 900px;
            width: 100%;
            padding: 48px;
            animation: fadeIn 0.5s ease-in;
        }
        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(20px); }
            to { opacity: 1; transform: translateY(0); }
        }
        .header {
            text-align: center;
            margin-bottom: 40px;
        }
        .logo {
            font-size: 48px;
            font-weight: 700;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 12px;
        }
        .subtitle {
            font-size: 18px;
            color: #666;
            margin-bottom: 8px;
        }
        .version {
            display: inline-block;
            background: #f0f0f0;
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 14px;
            color: #666;
            margin-top: 8px;
        }
        .status {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            background: #10b981;
            color: white;
            padding: 8px 16px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 500;
            margin-top: 16px;
        }
        .status::before {
            content: '';
            width: 8px;
            height: 8px;
            background: white;
            border-radius: 50%;
            animation: pulse 2s infinite;
        }
        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }
        .section {
            margin-top: 32px;
        }
        .section-title {
            font-size: 20px;
            font-weight: 600;
            color: #333;
            margin-bottom: 20px;
            padding-bottom: 12px;
            border-bottom: 2px solid #f0f0f0;
        }
        .endpoints {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 16px;
        }
        .endpoint-card {
            background: #f8f9fa;
            border: 1px solid #e9ecef;
            border-radius: 12px;
            padding: 20px;
            transition: all 0.3s ease;
            cursor: pointer;
            text-decoration: none;
            display: block;
            color: inherit;
        }
        .endpoint-card:hover {
            transform: translateY(-4px);
            box-shadow: 0 8px 20px rgba(0,0,0,0.1);
            border-color: #667eea;
            background: #f0f4ff;
        }
        .endpoint-card:active {
            transform: translateY(-2px);
        }
        .endpoint-method {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 600;
            margin-bottom: 8px;
        }
        .method-get { background: #10b981; color: white; }
        .method-post { background: #3b82f6; color: white; }
        .endpoint-path {
            font-family: 'Monaco', 'Courier New', monospace;
            font-size: 14px;
            color: #333;
            font-weight: 500;
            margin-bottom: 4px;
        }
        .endpoint-desc {
            font-size: 13px;
            color: #666;
        }
        .actions {
            margin-top: 32px;
            display: flex;
            gap: 16px;
            justify-content: center;
            flex-wrap: wrap;
        }
        .btn {
            padding: 14px 28px;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 600;
            text-decoration: none;
            display: inline-block;
            transition: all 0.3s ease;
            border: none;
            cursor: pointer;
        }
        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }
        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(102, 126, 234, 0.4);
        }
        .btn-secondary {
            background: #f8f9fa;
            color: #333;
            border: 1px solid #e9ecef;
        }
        .btn-secondary:hover {
            background: #e9ecef;
        }
        .footer {
            margin-top: 40px;
            text-align: center;
            color: #999;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">🛡️ DLP Risk Analyzer</div>
            <div class=""subtitle"">Data Loss Prevention & Risk Analysis API</div>
            <div class=""version"">Version 1.0.0</div>
            <div class=""status"">● System Operational</div>
        </div>

        <div class=""section"">
            <div class=""section-title"">API Endpoints</div>
            <div class=""endpoints"">
                <a href=""/swagger"" class=""endpoint-card"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/swagger</div>
                    <div class=""endpoint-desc"">Interactive API Documentation</div>
                </a>
                <a href=""/health"" class=""endpoint-card"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/health</div>
                    <div class=""endpoint-desc"">System Health Check</div>
                </a>
                <a href=""/api"" class=""endpoint-card"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/api</div>
                    <div class=""endpoint-desc"">API Information</div>
                </a>
                <a href=""/api"" class=""endpoint-card"" title=""Click to view API information"">
                    <span class=""endpoint-method method-post"">POST</span>
                    <div class=""endpoint-path"">/api/auth/login</div>
                    <div class=""endpoint-desc"">User Authentication</div>
                </a>
                <a href=""/api/incidents"" class=""endpoint-card"" title=""Click to view API endpoint"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/api/incidents</div>
                    <div class=""endpoint-desc"">Get Security Incidents</div>
                </a>
                <a href=""/api/reports"" class=""endpoint-card"" title=""Click to view API endpoint"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/api/reports</div>
                    <div class=""endpoint-desc"">Generate Reports</div>
                </a>
                <a href=""/api/settings"" class=""endpoint-card"" title=""Click to view API endpoint"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/api/settings</div>
                    <div class=""endpoint-desc"">System Settings</div>
                </a>
                <a href=""/api/users"" class=""endpoint-card"" title=""Click to view API endpoint"">
                    <span class=""endpoint-method method-get"">GET</span>
                    <div class=""endpoint-path"">/api/users</div>
                    <div class=""endpoint-desc"">User Management</div>
                </a>
            </div>
        </div>

        <div class=""actions"">
            <a href=""/api"" class=""btn btn-primary"">📚 View API Information</a>
            <a href=""/health"" class=""btn btn-secondary"">💚 Health Check</a>
        </div>

        <div class=""footer"">
            <p>DLP Risk Analyzer API - Protecting your data, analyzing risks</p>
        </div>
    </div>
</body>
</html>
", "text/html"));

app.MapControllers();

// Swagger must be configured after MapControllers in minimal API
// SECURITY: Only enable Swagger in Development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLP Risk Analyzer API v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

// Health check endpoint
app.MapGet("/health", () =>
{
    try
    {
        // Try to get Istanbul timezone (works on both Windows and Linux)
        TimeZoneInfo timezone;
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch
        {
            // Fallback for Windows or if timezone not found
            try
            {
                timezone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch
            {
                // Final fallback to UTC
                timezone = TimeZoneInfo.Utc;
            }
        }
        var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        return Results.Ok(new { status = "healthy", timestamp = istTime.ToString("O"), timezone = timezone.Id });
    }
    catch
    {
        // Fallback to UTC if all else fails
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow.ToString("O"), timezone = "UTC" });
    }
});

// API info endpoint
app.MapGet("/api", () => Results.Ok(new 
{ 
    name = "DLP Risk Analyzer API",
    version = "1.0.0",
    endpoints = new
    {
        swagger = "/swagger",
        health = "/health",
        auth = "/api/auth",
        incidents = "/api/incidents",
        reports = "/api/reports",
        settings = "/api/settings",
        users = "/api/users"
    }
}));

// Configure URL binding - CRITICAL: Must listen on 0.0.0.0 for network access
// This allows API to be accessible from other devices on the network
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
string defaultUrl = "http://0.0.0.0:5001"; // 0.0.0.0 allows both localhost and network IP access

// CRITICAL FIX: Always force 0.0.0.0 for network access, even if environment variable is set
// This ensures API is accessible from other devices on the network
bool forceNetworkAccess = true; // Set to false only if you specifically want localhost-only access

if (forceNetworkAccess)
{
    // Force 0.0.0.0:5001 for network access
    app.Urls.Clear();
    app.Urls.Add(defaultUrl);
    Console.WriteLine("INFO: API configured to listen on 0.0.0.0:5001 for network access");
}
else
{
    // Original logic (only if forceNetworkAccess is false)
    app.Urls.Clear();
    
    if (!string.IsNullOrEmpty(urlsEnv))
    {
        // If ASPNETCORE_URLS is set, parse it but ensure it uses 0.0.0.0 instead of localhost
        var urls = urlsEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var url in urls)
        {
            var trimmedUrl = url.Trim();
            if (!string.IsNullOrEmpty(trimmedUrl))
            {
                // Replace localhost with 0.0.0.0 to allow network access
                if (trimmedUrl.Contains("localhost") || trimmedUrl.Contains("127.0.0.1"))
                {
                    trimmedUrl = trimmedUrl.Replace("localhost", "0.0.0.0").Replace("127.0.0.1", "0.0.0.0");
                }
                app.Urls.Add(trimmedUrl);
            }
        }
        
        // If no valid URLs were added, use default
        if (app.Urls.Count == 0)
        {
            app.Urls.Add(defaultUrl);
        }
    }
    else
    {
        // If not set, use default 0.0.0.0:5001 (works like Next.js - accessible via both localhost and network IP)
        app.Urls.Add(defaultUrl);
    }
    
    // CRITICAL: Ensure we're listening on 0.0.0.0, not just localhost
    // This is required for other devices on the network to access the API
    if (!app.Urls.Any(url => url.Contains("0.0.0.0")))
    {
        // If no 0.0.0.0 URL found, add it
        app.Urls.Clear();
        app.Urls.Add(defaultUrl);
        Console.WriteLine("WARNING: Forced API to listen on 0.0.0.0:5001 for network access");
    }
}

// Log listening addresses
Console.WriteLine("API is listening on:");
foreach (var url in app.Urls)
{
    Console.WriteLine($"  - {url}");
    Console.WriteLine($"    Swagger UI: {url}/swagger");
    Console.WriteLine($"    Health Check: {url}/health");
}

app.Run();

