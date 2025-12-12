using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Collector.Services;

public class DlpRuntimeConfigProvider
{
    private readonly object _sync = new();
    private DLPConfig _current;
    private readonly ILogger<DlpRuntimeConfigProvider> _logger;

    public event Action<DLPConfig>? ConfigChanged;

    public DlpRuntimeConfigProvider(IOptions<DLPConfig> defaults, ILogger<DlpRuntimeConfigProvider> logger)
    {
        _current = defaults.Value;
        _logger = logger;
        LogConfig("Initial configuration loaded from appsettings.json", _current);
    }

    public DLPConfig GetCurrent()
    {
        lock (_sync)
        {
            return Clone(_current);
        }
    }

    public void Update(DLPConfig newConfig, string source)
    {
        lock (_sync)
        {
            _current = Clone(newConfig);
            LogConfig($"DLP configuration updated via {source}", _current);
        }

        ConfigChanged?.Invoke(GetCurrent());
    }

    private static DLPConfig Clone(DLPConfig config)
    {
        return new DLPConfig
        {
            ManagerIP = config.ManagerIP,
            ManagerPort = config.ManagerPort,
            Username = config.Username,
            Password = config.Password,
            UseHttps = config.UseHttps,
            Timeout = config.Timeout
        };
    }

    private void LogConfig(string message, DLPConfig config)
    {
        _logger.LogInformation("{Message}: {Manager}::{Port}, HTTPS={Https}, Timeout={Timeout}s, Username={Username}",
            message, config.ManagerIP, config.ManagerPort, config.UseHttps, config.Timeout, config.Username);
    }
}

