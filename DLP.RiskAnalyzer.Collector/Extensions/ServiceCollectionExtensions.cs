using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Runtime.InteropServices;

namespace DLP.RiskAnalyzer.Collector.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisHost = configuration["Redis:Host"] ?? "localhost";
        var redisPort = configuration.GetValue<int>("Redis:Port", 6379);
        var redisPassword = configuration["Redis:Password"];
        
        var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        
        if (isDocker && redisHost == "localhost")
        {
            redisHost = "host.docker.internal";
        }
        else if (!isDocker && redisHost == "localhost" && 
                 RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            redisHost = "127.0.0.1";
        }
        
        var redisConnectionString = $"{redisHost}:{redisPort}";
        
        var redisConfig = new ConfigurationOptions
        {
            EndPoints = { redisConnectionString },
            ConnectTimeout = 10000,
            SyncTimeout = 5000,
            AbortOnConnectFail = false,
            ReconnectRetryPolicy = new ExponentialRetry(1000),
            ConnectRetry = 3
        };
        
        if (!string.IsNullOrWhiteSpace(redisPassword))
        {
            redisConfig.Password = redisPassword;
        }
        
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConfig));

        return services;
    }
}
