using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Background service that collects incidents from DLP API
/// DUAL MODE: Regular (every 6h, 6h lookback) + Daily (23:00, 24h lookback)
/// </summary>
public class CollectorBackgroundService : BackgroundService
{
    private readonly DLPCollectorService _collectorService;
    private readonly ILogger<CollectorBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    
    // Regular collection settings
    private readonly int _regularIntervalHours;
    private readonly int _regularLookbackHours;
    
    // Daily collection settings
    private readonly TimeSpan _dailyRunTime;
    private readonly int _dailyLookbackHours;
    
    private readonly int _pageSize;
    private DateTime _lastDailyRun = DateTime.MinValue;
    private DateTime _lastRegularRun = DateTime.MinValue;

    public CollectorBackgroundService(
        DLPCollectorService collectorService,
        ILogger<CollectorBackgroundService> logger,
        IConfiguration configuration)
    {
        _collectorService = collectorService;
        _logger = logger;
        _configuration = configuration;
        
        // Read regular collection settings
        _regularIntervalHours = _configuration.GetValue<int>("Collector:RegularIntervalHours", 6);
        _regularLookbackHours = _configuration.GetValue<int>("Collector:RegularLookbackHours", 6);
        
        // Read daily collection settings
        var dailyRunTimeStr = _configuration["Collector:DailyRunTime"] ?? "23:00";
        _dailyRunTime = TimeSpan.Parse(dailyRunTimeStr);
        _dailyLookbackHours = _configuration.GetValue<int>("Collector:DailyLookbackHours", 24);
        
        _pageSize = _configuration.GetValue<int>("Collector:BatchSize", 1000);
        
        _logger.LogInformation(
            "Collector DUAL MODE configured:\n" +
            "  - Regular: Every {RegularInterval}h, {RegularLookback}h lookback\n" +
            "  - Daily: At {DailyTime}, {DailyLookback}h lookback\n" +
            "  - PageSize: {PageSize}", 
            _regularIntervalHours, _regularLookbackHours,
            _dailyRunTime, _dailyLookbackHours, _pageSize);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DLP Collector Service started (DUAL MODE)");

        // Run initial collection immediately
        await CollectIncidentsAsync(_regularLookbackHours, "Initial", stoppingToken);
        _lastRegularRun = DateTime.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                
                // Check for daily run (23:00)
                if (ShouldRunDaily(now))
                {
                    _logger.LogInformation("=== DAILY COLLECTION TRIGGERED (24h lookback) ===");
                    await CollectIncidentsAsync(_dailyLookbackHours, "Daily", stoppingToken);
                    _lastDailyRun = now.Date; // Mark that we ran today
                }
                
                // Check for regular run (every 6 hours)
                if (ShouldRunRegular(now))
                {
                    _logger.LogInformation("=== REGULAR COLLECTION TRIGGERED ({LookbackHours}h lookback) ===", _regularLookbackHours);
                    await CollectIncidentsAsync(_regularLookbackHours, "Regular", stoppingToken);
                    _lastRegularRun = now;
                }
                
                // Check every minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in collector background service");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("DLP Collector Service stopped");
    }

    private bool ShouldRunDaily(DateTime now)
    {
        // Check if it's time for daily run and we haven't run today
        var currentTime = now.TimeOfDay;
        var timeDiff = (currentTime - _dailyRunTime).TotalMinutes;
        
        // Run if within 2 minutes of scheduled time and haven't run today
        return timeDiff >= 0 && timeDiff < 2 && _lastDailyRun.Date != now.Date;
    }

    private bool ShouldRunRegular(DateTime now)
    {
        // Run if enough time has passed since last regular run
        var hoursSinceLastRun = (now - _lastRegularRun).TotalHours;
        return hoursSinceLastRun >= _regularIntervalHours;
    }

    private async Task CollectIncidentsAsync(int lookbackHours, string runType, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{RunType}] Starting incident collection with {LookbackHours}h lookback...", 
            runType, lookbackHours);

        var endTime = DateTime.Now;
        var startTime = endTime.AddHours(-lookbackHours);
        
        // Time chunk size in hours
        var chunkSizeHours = 4;
        var totalChunks = (int)Math.Ceiling((double)lookbackHours / chunkSizeHours);
        
        _logger.LogInformation("[{RunType}] Fetching incidents from {StartTime} to {EndTime} in {TotalChunks} chunks", 
            runType, startTime, endTime, totalChunks);

        try
        {
            List<DLPIncident> allIncidents = new();
            int successfulChunks = 0;
            int failedChunks = 0;
            
            var chunkStart = startTime;
            int chunkIndex = 0;
            
            while (chunkStart < endTime)
            {
                chunkIndex++;
                var chunkEnd = chunkStart.AddHours(chunkSizeHours);
                if (chunkEnd > endTime) chunkEnd = endTime;
                
                _logger.LogInformation("[{RunType}] Fetching chunk {ChunkIndex}/{TotalChunks}: {ChunkStart} to {ChunkEnd}", 
                    runType, chunkIndex, totalChunks, chunkStart, chunkEnd);
                
                int maxRetries = 3;
                List<DLPIncident> chunkIncidents = new();
                bool chunkSuccess = false;
                
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        chunkIncidents = await _collectorService.FetchIncidentsAsync(chunkStart, chunkEnd, 1, _pageSize);
                        _logger.LogInformation("[{RunType}] Chunk {ChunkIndex}: Fetched {Count} incidents", 
                            runType, chunkIndex, chunkIncidents.Count);
                        chunkSuccess = true;
                        break;
                    }
                    catch (TaskCanceledException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("[{RunType}] Chunk {ChunkIndex} timeout on attempt {Attempt}. Retrying...", 
                            runType, chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (HttpRequestException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("[{RunType}] Chunk {ChunkIndex} connection error on attempt {Attempt}. Retrying...", 
                            runType, chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "[{RunType}] Chunk {ChunkIndex} error on attempt {Attempt}. Retrying...", 
                            runType, chunkIndex, attempt);
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
                    _logger.LogError("[{RunType}] Failed to fetch chunk {ChunkIndex} after {MaxRetries} attempts. Skipping.", 
                        runType, chunkIndex, maxRetries);
                    failedChunks++;
                }
                
                chunkStart = chunkEnd;
                
                if (chunkStart < endTime)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                }
            }
            
            _logger.LogInformation("[{RunType}] Total {Count} incidents retrieved ({LookbackHours}h). Success: {Success}, Failed: {Failed}", 
                runType, allIncidents.Count, lookbackHours, successfulChunks, failedChunks);

            if (allIncidents.Count == 0)
            {
                _logger.LogInformation("[{RunType}] No incidents found in the time range", runType);
                return;
            }

            // Remove duplicates
            var uniqueIncidents = allIncidents
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .ToList();
            
            _logger.LogInformation("[{RunType}] After deduplication: {UniqueCount} unique incidents", 
                runType, uniqueIncidents.Count);

            // Push to Redis
            var pushedCount = 0;
            var errorCount = 0;
            
            foreach (var dlpIncident in uniqueIncidents)
            {
                try
                {
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
                    _logger.LogError(ex, "[{RunType}] Failed to push incident: User={User}", runType, dlpIncident.User);
                }
            }

            _logger.LogInformation("[{RunType}] Successfully pushed {PushedCount} incidents to Redis (Errors: {ErrorCount})", 
                runType, pushedCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RunType}] Failed to collect incidents from Forcepoint DLP API", runType);
            throw;
        }
    }
}