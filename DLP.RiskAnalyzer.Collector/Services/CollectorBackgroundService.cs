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
            // PAGINATION: Fetch ALL incidents by iterating through pages
            List<DLPIncident> allIncidents = new();
            int page = 1;
            int maxRetries = 3;
            bool hasMorePages = true;
            
            while (hasMorePages)
            {
                List<DLPIncident> pageIncidents = new();
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        _logger.LogInformation("Fetching page {Page} from DLP API (Attempt {Attempt}/{MaxRetries})", 
                            page, attempt, maxRetries);
                        
                        pageIncidents = await _collectorService.FetchIncidentsAsync(startTime, endTime, page, _pageSize);
                        
                        _logger.LogInformation("Page {Page}: Fetched {Count} incidents", page, pageIncidents.Count);
                        break; // Success, exit retry loop
                    }
                    catch (TaskCanceledException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("DLP API request timeout on page {Page}, attempt {Attempt}. Retrying in 10 seconds...", 
                            page, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                    catch (HttpRequestException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("DLP API connection error on page {Page}, attempt {Attempt}. Retrying in 10 seconds...", 
                            page, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    }
                }
                
                allIncidents.AddRange(pageIncidents);
                
                // Check if there are more pages
                // If we received fewer incidents than page size, we've reached the end
                if (pageIncidents.Count < _pageSize)
                {
                    hasMorePages = false;
                    _logger.LogInformation("Reached last page (received {Count} < {PageSize})", 
                        pageIncidents.Count, _pageSize);
                }
                else
                {
                    page++;
                    // Safety limit: don't fetch more than 100 pages (100,000 incidents)
                    if (page > 100)
                    {
                        _logger.LogWarning("Reached safety limit of 100 pages. Stopping pagination.");
                        hasMorePages = false;
                    }
                }
            }
            
            _logger.LogInformation("Successfully fetched TOTAL {Count} incidents from DLP API across {Pages} pages", 
                allIncidents.Count, page);

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