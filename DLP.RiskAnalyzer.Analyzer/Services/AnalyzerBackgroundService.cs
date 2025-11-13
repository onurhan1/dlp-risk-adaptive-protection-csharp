using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Background service to continuously process Redis stream and save incidents to database
/// </summary>
public class AnalyzerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyzerBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10); // Process every 10 seconds

    public AnalyzerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AnalyzerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analyzer Background Service started. Processing Redis stream every {Interval} seconds", 
            _processingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                    var riskAnalyzerService = scope.ServiceProvider.GetRequiredService<RiskAnalyzerService>();

                    // Process Redis stream and calculate risk scores
                    var processedCount = await riskAnalyzerService.ProcessRedisStreamAsync(dbService);
                    
                    if (processedCount > 0)
                    {
                        _logger.LogInformation("Processed {Count} incidents from Redis stream and calculated risk scores", 
                            processedCount);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Redis stream in background service");
            }

            // Wait before next processing cycle
            await Task.Delay(_processingInterval, stoppingToken);
        }

        _logger.LogInformation("Analyzer Background Service stopped");
    }
}

