using DLP.RiskAnalyzer.Collector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DLP.RiskAnalyzer.Collector;

class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Redis - Docker Desktop on Windows compatibility
                var redisHost = configuration["Redis:Host"] ?? "localhost";
                var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
                var redisPassword = configuration["Redis:Password"]; // Optional password
                
                var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
                
                if (isDocker && redisHost == "localhost")
                {
                    // If running inside Docker container, use host.docker.internal
                    redisHost = "host.docker.internal";
                }
                else if (!isDocker && redisHost == "localhost" && 
                         RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // If running on Windows host, use 127.0.0.1 for better reliability with Docker Desktop
                    redisHost = "127.0.0.1";
                }
                
                var redisConnectionString = $"{redisHost}:{redisPort}";
                
                // Configure Redis connection with retry for Docker Desktop
                var redisConfig = new StackExchange.Redis.ConfigurationOptions
                {
                    EndPoints = { redisConnectionString },
                    ConnectTimeout = 10000, // 10 seconds
                    SyncTimeout = 5000,     // 5 seconds
                    AbortOnConnectFail = false, // Don't fail on first connection attempt
                    ReconnectRetryPolicy = new StackExchange.Redis.ExponentialRetry(1000), // Retry with exponential backoff
                    ConnectRetry = 3 // Retry connection 3 times
                };
                
                // Add password if configured (check for null, empty, or whitespace)
                // Note: Empty string ("") is treated as "no password" - only non-empty values are used
                if (!string.IsNullOrWhiteSpace(redisPassword))
                {
                    redisConfig.Password = redisPassword;
                }
                
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig));
                
                // Register services
                services.Configure<DLPConfig>(configuration.GetSection("DLP"));
                services.Configure<RedisConfig>(configuration.GetSection("Redis"));
                services.Configure<AnalyzerBridgeOptions>(configuration.GetSection("Analyzer"));
                
                services.AddSingleton<DlpRuntimeConfigProvider>();
                services.AddSingleton<AnalyzerConfigClient>();
                services.AddHostedService<DlpConfigurationSyncService>();
                services.AddSingleton<CollectorLogService>();
                services.AddSingleton<DLPCollectorService>();
                services.AddHostedService<CollectorBackgroundService>();
                
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await host.RunAsync();
    }
}

/// <summary>
/// DLP API Configuration
/// </summary>
public class DLPConfig
{
    public string ManagerIP { get; set; } = "localhost";
    public int ManagerPort { get; set; } = 8443;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseHttps { get; set; } = true;
    public int Timeout { get; set; } = 30;
}

/// <summary>
/// Redis Configuration
/// </summary>
public class RedisConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string? Password { get; set; }
}