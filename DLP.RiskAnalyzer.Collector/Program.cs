using DLP.RiskAnalyzer.Collector.Extensions;
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
        using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });
        var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("CollectorRuntimeConfig");
        var preliminaryConfiguration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        var runtimeOverrides = await AnalyzerRuntimeConfigBootstrapper.FetchOverridesAsync(
            preliminaryConfiguration,
            bootstrapLogger);

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                if (runtimeOverrides.Count > 0)
                {
                    config.AddInMemoryCollection(runtimeOverrides);
                }
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Use the extension method for Redis configuration
                services.AddRedisServices(configuration);
                
                // Register services
                services.Configure<DLPConfig>(configuration.GetSection("DLP"));
                services.Configure<RedisConfig>(configuration.GetSection("Redis"));
                services.Configure<AnalyzerBridgeOptions>(configuration.GetSection("Analyzer"));
                
                services.AddSingleton<DlpRuntimeConfigProvider>();
                services.AddSingleton<AnalyzerConfigClient>();
                services.AddHostedService<DlpConfigurationSyncService>();
                services.AddSingleton<ICollectorLogService, CollectorLogService>();
                services.AddSingleton<ManualCollectQueue>();
                services.AddSingleton<IDLPCollectorService, DLPCollectorService>();
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
