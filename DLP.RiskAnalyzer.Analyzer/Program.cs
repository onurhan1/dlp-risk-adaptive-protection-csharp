using DLP.RiskAnalyzer.Analyzer.Extensions;
using DLP.RiskAnalyzer.Analyzer.Services;

// Enable legacy timestamp behavior for Npgsql to handle DateTime with Kind=Unspecified
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration ─────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
    });

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

// Infrastructure (Database, Redis, DataProtection, Cache)
builder.Services.AddInfrastructure(builder.Configuration);

// Repositories
builder.Services.AddRepositories();

// Domain Services
builder.Services.AddDomainServices();

// External HTTP Clients (OpenAI, Splunk, Azure, etc.)
builder.Services.AddExternalHttpClients();

// Options
builder.Services.Configure<DLP.RiskAnalyzer.Analyzer.Options.InternalApiOptions>(
    builder.Configuration.GetSection("InternalApi"));

// Authentication (JWT)
builder.Services.AddJwtAuthentication(builder.Configuration);

// CORS
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);

// Rate Limiting (§40)
builder.Services.AddRateLimiting();

// Background Services
builder.Services.AddHostedService<AnalyzerBackgroundService>();

// ── Application Pipeline ─────────────────────────────────────────────────
var app = builder.Build();

// Database migrations & admin seed
await app.ApplyMigrationsAndSeedAsync(builder.Configuration);

// Middleware pipeline (routing, auth, security headers, swagger)
app.ConfigurePipeline();

// ── Minimal API Endpoints ────────────────────────────────────────────────
app.MapControllers();

// Health check (Checks DB & Redis state)
app.MapHealthChecks("/health");

// API info
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

// URL binding & run
app.ConfigureUrls();
app.Run();
