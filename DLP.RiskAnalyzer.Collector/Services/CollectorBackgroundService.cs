using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Background service that collects incidents from DLP API
/// Now with PAGINATION support to fetch ALL incidents, not just first 1000
/// </summary>
public class CollectorBackgroundService : BackgroundService
{
    private readonly DLPCollectorService _collectorService;
    private readonly ILogger<CollectorBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _collectionInterval;
    private readonly int _lookbackHours;
    private readonly int _pageSize;

    public CollectorBackgroundService(
        DLPCollectorService collectorService,
        ILogger<CollectorBackgroundService> logger,
        IConfiguration configuration)
    {
        _collectorService = collectorService;
        _logger = logger;
        _configuration = configuration;
        
        // Read settings from configuration
        var intervalMinutes = _configuration.GetValue<int>("Collector:IntervalMinutes", 60);
        _collectionInterval = TimeSpan.FromMinutes(intervalMinutes);
        _lookbackHours = _configuration.GetValue<int>("Collector:LookbackHours", 24);
        _pageSize = _configuration.GetValue<int>("Collector:BatchSize", 1000);
        
        _logger.LogInformation("Collector configured: Interval={Interval}min, Lookback={Lookback}h, PageSize={PageSize}", 
            intervalMinutes, _lookbackHours, _pageSize);
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

        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-_lookbackHours);
        
        _logger.LogInformation("Fetching incidents from {StartTime} to {EndTime} ({LookbackHours} hour lookback)", 
            startTime, endTime, _lookbackHours);

        try
        {
            // IMPORTANT: Forcepoint DLP API ignores pagination parameters (start/limit)
            // It returns ALL incidents in the date range in a single response
            // Therefore, we only make one request
            List<DLPIncident> allIncidents = new();
            int maxRetries = 3;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Fetching incidents from DLP API (Attempt {Attempt}/{MaxRetries})", 
                        attempt, maxRetries);
                    
                    allIncidents = await _collectorService.FetchIncidentsAsync(startTime, endTime, 1, _pageSize);
                    
                    _logger.LogInformation("Successfully fetched {Count} incidents from Forcepoint DLP API", 
                        allIncidents.Count);
                    break; // Success, exit retry loop
                }
                catch (TaskCanceledException) when (attempt < maxRetries)
                {
                    _logger.LogWarning("DLP API request timeout on attempt {Attempt}. Retrying in 10 seconds...", 
                        attempt);
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (HttpRequestException) when (attempt < maxRetries)
                {
                    _logger.LogWarning("DLP API connection error on attempt {Attempt}. Retrying in 10 seconds...", 
                        attempt);
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            
            _logger.LogInformation("Total {Count} incidents retrieved from DLP API for {LookbackHours}h lookback", 
                allIncidents.Count, _lookbackHours);

            if (allIncidents.Count == 0)
            {
                _logger.LogInformation("No incidents found in the time range {StartTime} to {EndTime}", startTime, endTime);
                return;
            }

            // Push incidents to Redis
            var pushedCount = 0;
            var errorCount = 0;
            
            foreach (var dlpIncident in allIncidents)
            {
                try
                {
                    var incident = new DLP.RiskAnalyzer.Shared.Models.Incident
                    {
                        Id = dlpIncident.Id,  // DLP API'den gelen orijinal ID
                        UserEmail = dlpIncident.User ?? "unknown",
                        Department = dlpIncident.Department,
                        Severity = dlpIncident.Severity,
                        DataType = dlpIncident.DataType,
                        Timestamp = dlpIncident.Timestamp,
                        Policy = dlpIncident.Policy,
                        Channel = dlpIncident.Channel,
                        // New fields
                        Action = dlpIncident.Action,
                        Destination = dlpIncident.Destination,
                        FileName = dlpIncident.FileName,
                        LoginName = dlpIncident.LoginName,
                        EmailAddress = dlpIncident.EmailAddress,
                        ViolationTriggers = dlpIncident.ViolationTriggers != null 
                            ? System.Text.Json.JsonSerializer.Serialize(dlpIncident.ViolationTriggers) 
                            : null
                    };

                    await _collectorService.PushToRedisStreamAsync(incident);
                    pushedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "Failed to push incident: User={User}", dlpIncident.User);
                }
            }

            _logger.LogInformation("Successfully pushed {PushedCount} incidents to Redis (Errors: {ErrorCount})", 
                pushedCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect incidents from Forcepoint DLP API");
            throw;
        }
    }
}