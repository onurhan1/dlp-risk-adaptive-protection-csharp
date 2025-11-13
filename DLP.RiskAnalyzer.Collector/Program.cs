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
                
                // Get DLP configuration for base address
                var dlpManagerIP = configuration["DLP:ManagerIP"] ?? "localhost";
                var dlpManagerPort = configuration.GetValue<int>("DLP:ManagerPort", 8443);
                var useHttps = configuration.GetValue<bool>("DLP:UseHttps", true);
                var timeout = configuration.GetValue<int>("DLP:Timeout", 30);
                var baseUrl = useHttps 
                    ? $"https://{dlpManagerIP}:{dlpManagerPort}"
                    : $"http://{dlpManagerIP}:{dlpManagerPort}";
                
                // HttpClient with SSL certificate bypass (for self-signed DLP certs)
                // Create handler with SSL bypass - same approach as DLPTestController
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                
                // Create HttpClient directly (same approach as DLPTestController which works)
                var httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl),
                    Timeout = TimeSpan.FromSeconds(timeout)
                };
                
                // Register HttpClient as singleton
                services.AddSingleton(httpClient);
                
                // Register DLPCollectorService with the pre-configured HttpClient
                services.AddSingleton<DLPCollectorService>();
                
                // Redis - Docker Desktop on Windows compatibility
                var redisHost = configuration["Redis:Host"] ?? "localhost";
                var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
                
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
                
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig));
                
                // Register services
                services.AddSingleton<DLPCollectorService>();
                services.AddHostedService<CollectorBackgroundService>();
                
                // Configuration
                services.Configure<DLPConfig>(configuration.GetSection("DLP"));
                services.Configure<RedisConfig>(configuration.GetSection("Redis"));
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
}

/// <summary>
/// Redis Configuration
/// </summary>
public class RedisConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
}

