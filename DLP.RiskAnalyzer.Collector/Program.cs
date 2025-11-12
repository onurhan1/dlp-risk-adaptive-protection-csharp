using DLP.RiskAnalyzer.Collector.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
                var baseUrl = useHttps 
                    ? $"https://{dlpManagerIP}:{dlpManagerPort}"
                    : $"http://{dlpManagerIP}:{dlpManagerPort}";
                
                // HttpClient with SSL certificate bypass (for self-signed DLP certs)
                // Create handler as singleton to ensure SSL bypass works correctly
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                
                services.AddHttpClient<DLPCollectorService>()
                    .ConfigurePrimaryHttpMessageHandler(() => handler)
                    .ConfigureHttpClient(client =>
                    {
                        client.BaseAddress = new Uri(baseUrl);
                        client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int>("DLP:Timeout", 30));
                    });
                
                // Redis
                var redisHost = configuration["Redis:Host"] ?? "localhost";
                var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
                services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                    StackExchange.Redis.ConnectionMultiplexer.Connect($"{redisHost}:{redisPort}"));
                
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

