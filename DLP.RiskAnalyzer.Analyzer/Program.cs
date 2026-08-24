using DLP.RiskAnalyzer.Analyzer.Auth;
using DLP.RiskAnalyzer.Analyzer.Extensions;
using DLP.RiskAnalyzer.Analyzer.Services;

// Enable legacy timestamp behavior for Npgsql to handle DateTime with Kind=Unspecified
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ── Auth/config bootstrap ────────────────────────────────────────────────
// Load JWT + sensitive infra config (Redis/DLP/InternalApi) from the DB (auth schema),
// seeding from appsettings on first run. Must run BEFORE AddInfrastructure / InternalApi
// binding so those pick up the DB values. The DB values are layered over appsettings as an
// in-memory configuration source (highest priority); if the DB is unreachable, appsettings
// values are used unchanged (safe fallback).
var authBootstrap = AuthBootstrapper.EnsureAndLoad(builder.Configuration);
if (authBootstrap.ConfigOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(authBootstrap.ConfigOverrides);
}
builder.Services.AddSingleton(authBootstrap.Jwt);

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

// Behavioural surprisal model — estimator hyperparameters, tuned from the diagnostic report.
builder.Services.Configure<DLP.RiskAnalyzer.Analyzer.Services.Surprisal.SurprisalOptions>(
    builder.Configuration.GetSection(
        DLP.RiskAnalyzer.Analyzer.Services.Surprisal.SurprisalOptions.SectionName));

// Authentication (JWT) — settings loaded from the DB during the bootstrap above.
builder.Services.AddJwtAuthentication(authBootstrap.Jwt);

// CORS
builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);

// Rate Limiting (§40)
builder.Services.AddRateLimiting();

// Background Services
builder.Services.AddHostedService<AnalyzerBackgroundService>();
builder.Services.AddHostedService<PlaybookSchedulerService>();
builder.Services.AddHostedService<ScheduledJobBackgroundService>();
builder.Services.AddHostedService<InvestigationMailAutomationBackgroundService>();

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
