using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Background service that collects incidents from DLP API
/// </summary>
public class CollectorBackgroundService : BackgroundService
{
    private readonly DLPCollectorService _collectorService;
    private readonly ILogger<CollectorBackgroundService> _logger;
    private readonly TimeSpan _collectionInterval = TimeSpan.FromHours(1); // Collect every hour

    public CollectorBackgroundService(
        DLPCollectorService collectorService,
        ILogger<CollectorBackgroundService> logger)
    {
        _collectorService = collectorService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DLP Collector Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectIncidentsAsync(stoppingToken);
                await Task.Delay(_collectionInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in collector background service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Retry after 5 minutes
            }
        }

        _logger.LogInformation("DLP Collector Service stopped");
    }

    private async Task CollectIncidentsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting incident collection...");

        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-24); // Last 24 hours

        try
        {
            var incidents = await _collectorService.FetchIncidentsAsync(startTime, endTime);
            _logger.LogInformation("Fetched {Count} incidents from DLP API", incidents.Count);

            // Convert and push to Redis
            foreach (var dlpIncident in incidents)
            {
                var incident = new DLP.RiskAnalyzer.Shared.Models.Incident
                {
                    UserEmail = dlpIncident.User,
                    Department = dlpIncident.Department,
                    Severity = dlpIncident.Severity,
                    DataType = dlpIncident.DataType,
                    Timestamp = dlpIncident.Timestamp,
                    Policy = dlpIncident.Policy,
                    Channel = dlpIncident.Channel
                };

                await _collectorService.PushToRedisStreamAsync(incident);
            }

            _logger.LogInformation("Successfully collected and pushed {Count} incidents", incidents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect incidents");
            throw;
        }
    }
}

