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
        _logger.LogInformation("Starting incident collection from Forcepoint DLP REST API v1 with TIME CHUNKING...");

        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-_lookbackHours);
        
        // Time chunk size in hours (smaller chunks = less chance of missing incidents)
        var chunkSizeHours = 4;
        var totalChunks = (int)Math.Ceiling((double)_lookbackHours / chunkSizeHours);
        
        _logger.LogInformation("Fetching incidents from {StartTime} to {EndTime} ({LookbackHours}h lookback) in {TotalChunks} chunks of {ChunkSize}h each", 
            startTime, endTime, _lookbackHours, totalChunks, chunkSizeHours);

        try
        {
            List<DLPIncident> allIncidents = new();
            int successfulChunks = 0;
            int failedChunks = 0;
            
            // Process each time chunk sequentially
            var chunkStart = startTime;
            int chunkIndex = 0;
            
            while (chunkStart < endTime)
            {
                chunkIndex++;
                var chunkEnd = chunkStart.AddHours(chunkSizeHours);
                if (chunkEnd > endTime) chunkEnd = endTime;
                
                _logger.LogInformation("Fetching chunk {ChunkIndex}/{TotalChunks}: {ChunkStart} to {ChunkEnd}", 
                    chunkIndex, totalChunks, chunkStart, chunkEnd);
                
                int maxRetries = 3;
                List<DLPIncident> chunkIncidents = new();
                bool chunkSuccess = false;
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        chunkIncidents = await _collectorService.FetchIncidentsAsync(chunkStart, chunkEnd, 1, _pageSize);
                        _logger.LogInformation("Chunk {ChunkIndex}: Fetched {Count} incidents", chunkIndex, chunkIncidents.Count);
                        chunkSuccess = true;
                        break;
                    }
                    catch (TaskCanceledException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("Chunk {ChunkIndex} timeout on attempt {Attempt}. Retrying in 5 seconds...", 
                            chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (HttpRequestException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("Chunk {ChunkIndex} connection error on attempt {Attempt}. Retrying in 5 seconds...", 
                            chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "Chunk {ChunkIndex} error on attempt {Attempt}. Retrying in 5 seconds...", 
                            chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                }
                
                if (chunkSuccess)
                {
                    allIncidents.AddRange(chunkIncidents);
                    successfulChunks++;
                }
                else
                {
                    _logger.LogError("Failed to fetch chunk {ChunkIndex} after {MaxRetries} attempts. Skipping this chunk.", 
                        chunkIndex, maxRetries);
                    failedChunks++;
                }
                
                // Move to next chunk
                chunkStart = chunkEnd;
                
                // Small delay between chunks to avoid overwhelming the API
                if (chunkStart < endTime)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }
            
            _logger.LogInformation("Total {Count} incidents retrieved from DLP API for {LookbackHours}h lookback. Successful chunks: {Success}, Failed: {Failed}", 
                allIncidents.Count, _lookbackHours, successfulChunks, failedChunks);

            if (allIncidents.Count == 0)
            {
                _logger.LogInformation("No incidents found in the time range {StartTime} to {EndTime}", startTime, endTime);
                return;
            }

            // Remove duplicates based on incident ID
            var uniqueIncidents = allIncidents
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .ToList();
            
            _logger.LogInformation("After deduplication: {UniqueCount} unique incidents (removed {DuplicateCount} duplicates)", 
                uniqueIncidents.Count, allIncidents.Count - uniqueIncidents.Count);

            // Push incidents to Redis
            var pushedCount = 0;
            var errorCount = 0;
            
            foreach (var dlpIncident in uniqueIncidents)
            {
                try
                {
                    // Calculate MaxMatches from ViolationTriggers
                    var maxMatches = 0;
                    if (dlpIncident.ViolationTriggers != null)
                    {
                        maxMatches = dlpIncident.ViolationTriggers
                            .Where(t => t.Classifiers != null)
                            .SelectMany(t => t.Classifiers!)
                            .Select(c => c.NumberMatches)
                            .DefaultIfEmpty(0)
                            .Max();
                    }
                    
                    var incident = new DLP.RiskAnalyzer.Shared.Models.Incident
                    {
                        Id = dlpIncident.Id,
                        UserEmail = dlpIncident.User ?? "unknown",
                        Department = dlpIncident.Department,
                        Severity = dlpIncident.Severity,
                        DataType = dlpIncident.DataType,
                        Timestamp = dlpIncident.Timestamp,
                        Policy = dlpIncident.Policy,
                        Channel = dlpIncident.Channel,
                        MaxMatches = maxMatches,
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