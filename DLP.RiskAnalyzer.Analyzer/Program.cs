using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
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

// Database
builder.Services.AddDbContext<AnalyzerDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Enable retry on failure
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
    
    // Don't fail on startup if database is not available
    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(false);
});

// Redis
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
{
    var redisHost = builder.Configuration["Redis:Host"] ?? "localhost";
    var redisPort = builder.Configuration.GetValue<int>("Redis:Port", 6379);
    return StackExchange.Redis.ConnectionMultiplexer.Connect($"{redisHost}:{redisPort}");
});

// Services
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<RiskAnalyzerService>();
builder.Services.AddScoped<ReportGeneratorService>();
builder.Services.AddScoped<AnomalyDetector>();
builder.Services.AddScoped<ClassificationService>();
builder.Services.AddScoped<DLP.RiskAnalyzer.Shared.Services.RiskAnalyzer>();
builder.Services.AddScoped<EmailService>();

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

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition"); // For file downloads
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseRouting();

app.UseCors();
app.UseAuthorization();

// Swagger must be configured after UseRouting but before MapControllers
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLP Risk Analyzer API v1");
    c.RoutePrefix = "swagger"; // Swagger UI at /swagger
});

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
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DLP Risk Analyzer API v1");
    c.RoutePrefix = "swagger"; // Swagger UI at /swagger
});

// Health check endpoint
app.MapGet("/health", () =>
{
    var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
    return Results.Ok(new { status = "healthy", timestamp = istTime.ToString("O") });
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

// Configure URL binding - similar to Next.js, works with both localhost and network IP
var urlsEnv = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
string defaultUrl = "http://0.0.0.0:5001"; // 0.0.0.0 allows both localhost and network IP access

if (!string.IsNullOrEmpty(urlsEnv))
{
    // If ASPNETCORE_URLS is set, use it (may contain multiple URLs separated by ;)
    var urls = urlsEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);
    foreach (var url in urls)
    {
        var trimmedUrl = url.Trim();
        if (!string.IsNullOrEmpty(trimmedUrl))
        {
            app.Urls.Add(trimmedUrl);
        }
    }
}
else
{
    // If not set, use default 0.0.0.0:5001 (works like Next.js - accessible via both localhost and network IP)
    app.Urls.Add(defaultUrl);
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

