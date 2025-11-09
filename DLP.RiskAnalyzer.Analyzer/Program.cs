using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using DLP.RiskAnalyzer.Shared.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AnalyzerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();

// Root endpoint - return API info with link to Swagger
app.MapGet("/", () => Results.Ok(new 
{ 
    message = "DLP Risk Analyzer API",
    version = "1.0.0",
    documentation = "/swagger",
    health = "/health",
    api_info = "/api",
    endpoints = new
    {
        swagger = "/swagger",
        health = "/health",
        api_info = "/api",
        auth = "/api/auth",
        incidents = "/api/incidents",
        reports = "/api/reports",
        settings = "/api/settings",
        users = "/api/users"
    }
}));

app.MapControllers();

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

var port = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").FirstOrDefault()?.Split(":").LastOrDefault() ?? "8000";
app.Urls.Add($"http://localhost:{port}");
app.Run();

