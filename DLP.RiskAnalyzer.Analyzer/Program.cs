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
              .AllowAnyHeader();
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
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () =>
{
    var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    var istTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
    return Results.Ok(new { status = "healthy", timestamp = istTime.ToString("O") });
});

var port = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split(";").FirstOrDefault()?.Split(":").LastOrDefault() ?? "8000";
app.Urls.Add($"http://localhost:{port}");
app.Run();

