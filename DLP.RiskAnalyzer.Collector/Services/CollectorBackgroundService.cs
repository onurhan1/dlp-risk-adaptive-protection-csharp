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
        _logger.LogInformation("Starting incident collection from Forcepoint DLP REST API v1...");

        // Step 1: Calculate time range (last 24 hours by default, configurable via appsettings.json)
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-24); // Last 24 hours

        try
        {
            // Step 2: Fetch incidents from Forcepoint DLP API
            // Authentication is handled automatically in FetchIncidentsAsync
            // According to Forcepoint DLP REST API v1:
            // 1. First authenticate: POST /dlp/rest/v1/auth/access-token
            // 2. Then fetch incidents: GET /dlp/rest/v1/incidents?startTime=...&endTime=...
            var incidents = await _collectorService.FetchIncidentsAsync(startTime, endTime);
            _logger.LogInformation("Fetched {Count} incidents from Forcepoint DLP API", incidents.Count);

            // Step 3: Convert DLP API incidents to internal model and push to Redis
            var pushedCount = 0;
            var errorCount = 0;
            
            foreach (var dlpIncident in incidents)
            {
                try
                {
                    _logger.LogDebug("Processing incident: User={User}, Timestamp={Timestamp}, Severity={Severity}", 
                        dlpIncident.User, dlpIncident.Timestamp, dlpIncident.Severity);
                    
                    var incident = new DLP.RiskAnalyzer.Shared.Models.Incident
                    {
                        UserEmail = dlpIncident.User ?? "unknown",
                        Department = dlpIncident.Department,
                        Severity = dlpIncident.Severity,
                        DataType = dlpIncident.DataType,
                        Timestamp = dlpIncident.Timestamp,
                        Policy = dlpIncident.Policy,
                        Channel = dlpIncident.Channel
                    };

                    await _collectorService.PushToRedisStreamAsync(incident);
                    pushedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Failed to process and push incident: User={User}, Timestamp={Timestamp}", 
                        dlpIncident.User, dlpIncident.Timestamp);
                }
            }

            _logger.LogInformation("Successfully collected and pushed {PushedCount} incidents to Redis (Errors: {ErrorCount}, Total: {TotalCount})", 
                pushedCount, errorCount, incidents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect incidents from Forcepoint DLP API");
            throw;
        }
    }
}

