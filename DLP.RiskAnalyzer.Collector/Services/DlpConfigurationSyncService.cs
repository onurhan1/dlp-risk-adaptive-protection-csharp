using System.Text.Json;
using DLP.RiskAnalyzer.Shared.Constants;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace DLP.RiskAnalyzer.Collector.Services;

public class DlpConfigurationSyncService : BackgroundService
{
    private readonly AnalyzerConfigClient _configClient;
    private readonly DlpRuntimeConfigProvider _configProvider;
    private readonly IConnectionMultiplexer _redis;
    private readonly AnalyzerBridgeOptions _options;
    private readonly ILogger<DlpConfigurationSyncService> _logger;
    private ISubscriber? _subscriber;

    public DlpConfigurationSyncService(
        AnalyzerConfigClient configClient,
        DlpRuntimeConfigProvider configProvider,
        IConnectionMultiplexer redis,
        IOptions<AnalyzerBridgeOptions> options,
        ILogger<DlpConfigurationSyncService> logger)
    {
        _configClient = configClient;
        _configProvider = configProvider;
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LoadInitialConfig(stoppingToken);
        await SubscribeToUpdatesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.ConfigPollIntervalSeconds, 60)), stoppingToken);
                await RefreshFromAnalyzerAsync("scheduled poll", stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled configuration refresh.");
            }
        }
    }

    private async Task LoadInitialConfig(CancellationToken cancellationToken)
    {
        var remoteConfig = await _configClient.FetchConfigAsync(cancellationToken);
        if (remoteConfig != null)
        {
            // Validate that config is actually configured (not placeholder values)
            if (remoteConfig.ManagerIP == "YOUR_DLP_MANAGER_IP" || 
                remoteConfig.ManagerIP == "localhost" && string.IsNullOrWhiteSpace(remoteConfig.Username))
            {
                _logger.LogWarning("DLP API settings are not configured. Collector will not fetch incidents until settings are configured via Settings page.");
                _logger.LogWarning("Please configure DLP API settings in the dashboard: Settings → DLP API Configuration");
                return; // Don't update config with placeholder values
            }
            
            _configProvider.Update(remoteConfig, "Analyzer API (initial load)");
        }
        else
        {
            _logger.LogWarning("Failed to load DLP configuration from Analyzer API. Collector will use appsettings.json defaults.");
            _logger.LogWarning("Please ensure DLP API settings are configured via Settings page in the dashboard.");
        }
    }

    private async Task SubscribeToUpdatesAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        await _subscriber.SubscribeAsync(RedisChannel.Literal(DlpConstants.DlpConfigChannel), (channel, message) =>
        {
            try
            {
                var payload = JsonSerializer.Deserialize<DlpConfigBroadcastMessage>(message!);
                if (payload == null)
                {
                    _logger.LogWarning("Received empty config payload on channel {Channel}", channel);
                    return;
                }

                var newConfig = new DLPConfig
                {
                    ManagerIP = payload.ManagerIp ?? "localhost",
                    ManagerPort = payload.ManagerPort,
                    Username = payload.Username ?? string.Empty,
                    Password = payload.Password ?? string.Empty,
                    UseHttps = payload.UseHttps,
                    Timeout = payload.TimeoutSeconds
                };

                _configProvider.Update(newConfig, "Redis broadcast");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process configuration update message.");
            }
        });

        _logger.LogInformation("Subscribed to DLP config updates via Redis channel {Channel}", DlpConstants.DlpConfigChannel);
    }

    private async Task RefreshFromAnalyzerAsync(string reason, CancellationToken cancellationToken)
    {
        var remoteConfig = await _configClient.FetchConfigAsync(cancellationToken);
        if (remoteConfig != null)
        {
            // Validate that config is actually configured (not placeholder values)
            if (remoteConfig.ManagerIP == "YOUR_DLP_MANAGER_IP" || 
                remoteConfig.ManagerIP == "localhost" && string.IsNullOrWhiteSpace(remoteConfig.Username))
            {
                _logger.LogWarning("DLP API settings are not configured. Skipping config update.");
                return; // Don't update config with placeholder values
            }
            
            _configProvider.Update(remoteConfig, $"Analyzer API ({reason})");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscriber != null)
        {
            await _subscriber.UnsubscribeAllAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private class DlpConfigBroadcastMessage
    {
        public string? ManagerIp { get; set; }
        public int ManagerPort { get; set; }
        public bool UseHttps { get; set; }
        public int TimeoutSeconds { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}

