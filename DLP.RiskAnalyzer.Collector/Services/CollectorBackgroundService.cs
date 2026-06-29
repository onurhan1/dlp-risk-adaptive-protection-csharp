using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using DLP.RiskAnalyzer.Shared.Constants;
using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Collector.Mappers;
using StackExchange.Redis;
using System.Text.Json;
using DLP.RiskAnalyzer.Collector.Constants;

namespace DLP.RiskAnalyzer.Collector.Services;

/// <summary>
/// Background service that collects incidents from DLP API
/// DUAL MODE: Regular (every 6h, 6h lookback) + Daily (23:00, 24h lookback)
/// </summary>
public class CollectorBackgroundService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<CollectorBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    
    // Regular collection settings
    private readonly int _regularIntervalHours;
    private readonly int _regularLookbackHours;
    
    // Daily collection settings
    private readonly TimeSpan _dailyRunTime;
    private readonly int _dailyLookbackHours;
    
    private readonly int _pageSize;
    private DateTime _lastDailyRun = DateTime.MinValue;
    private DateTime _lastRegularRun = DateTime.MinValue;


    private readonly IDLPCollectorService _collectorService;
    private readonly ICollectorLogService _logService;
    private readonly ManualCollectQueue _manualCollectQueue;
    
    public CollectorBackgroundService(
        IDLPCollectorService collectorService,
        ICollectorLogService logService,
        ManualCollectQueue manualCollectQueue,
        IConnectionMultiplexer redis,
        ILogger<CollectorBackgroundService> logger,
        IConfiguration configuration)
    {
        _collectorService = collectorService;
        _logService = logService;
        _manualCollectQueue = manualCollectQueue;
        _redis = redis;
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
                
                // Check for manual collection in queue
                while (_manualCollectQueue.TryDequeue(out var manualCommand))
                {
                    if (manualCommand != null)
                    {
                        _logger.LogInformation("=== MANUAL COLLECTION TRIGGERED: JobId={JobId}, {Start} to {End} ===",
                            manualCommand.JobId, manualCommand.StartDate, manualCommand.EndDate);
                        await CollectIncidentsByDateRangeAsync(manualCommand, stoppingToken);
                    }
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

    /// <summary>
    /// Manual collection by date range with progress tracking via Redis.
    /// </summary>
    private async Task CollectIncidentsByDateRangeAsync(ManualCollectCommand command, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var statusKey = $"{DlpConstants.ManualCollectJobKeyPrefix}{command.JobId}";

        try
        {
            var startTime = command.StartDate;
            var endTime = command.EndDate;
            var totalHours = (endTime - startTime).TotalHours;
            var chunkSizeHours = CollectorConstants.DefaultChunkSizeHours;
            var totalChunks = (int)Math.Ceiling(totalHours / chunkSizeHours);

            // Update status to Running
            await UpdateJobStatusAsync(db, statusKey, new ManualCollectStatus
            {
                JobId = command.JobId,
                Status = ManualCollectStatusValues.Running,
                Progress = 0,
                Message = $"Çekim başlatılıyor... ({startTime:dd.MM.yyyy HH:mm} - {endTime:dd.MM.yyyy HH:mm})",
                TotalChunks = totalChunks,
                CurrentChunk = 0,
                StartedAt = DateTime.UtcNow
            });

            _logger.LogInformation("[Manual:{JobId}] Starting collection: {Start} to {End} in {TotalChunks} chunks",
                command.JobId, startTime, endTime, totalChunks);

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

                _logger.LogInformation("[Manual:{JobId}] Fetching chunk {ChunkIndex}/{TotalChunks}: {ChunkStart} to {ChunkEnd}",
                    command.JobId, chunkIndex, totalChunks, chunkStart, chunkEnd);

                int maxRetries = CollectorConstants.MaxApiRetries;
                List<DLPIncident> chunkIncidents = new();
                bool chunkSuccess = false;

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        chunkIncidents = await _collectorService.FetchIncidentsAsync(chunkStart, chunkEnd, 1, _pageSize);
                        _logger.LogInformation("[Manual:{JobId}] Chunk {ChunkIndex}: Fetched {Count} incidents",
                            command.JobId, chunkIndex, chunkIncidents.Count);
                        chunkSuccess = true;
                        break;
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "[Manual:{JobId}] Chunk {ChunkIndex} error on attempt {Attempt}. Retrying...",
                            command.JobId, chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(CollectorConstants.RetryDelaySeconds), cancellationToken);
                    }
                }

                if (chunkSuccess)
                {
                    allIncidents.AddRange(chunkIncidents);
                    successfulChunks++;
                }
                else
                {
                    _logger.LogError("[Manual:{JobId}] Failed to fetch chunk {ChunkIndex} after {MaxRetries} attempts.",
                        command.JobId, chunkIndex, maxRetries);
                    failedChunks++;
                }

                // Update progress
                var progress = (int)((double)chunkIndex / totalChunks * 100);
                await UpdateJobStatusAsync(db, statusKey, new ManualCollectStatus
                {
                    JobId = command.JobId,
                    Status = ManualCollectStatusValues.Running,
                    Progress = Math.Min(progress, 95), // Reserve 5% for Redis push
                    Message = $"Chunk {chunkIndex}/{totalChunks} tamamlandı — {allIncidents.Count} incident bulundu",
                    TotalIncidents = allIncidents.Count,
                    TotalChunks = totalChunks,
                    CurrentChunk = chunkIndex,
                    StartedAt = DateTime.UtcNow
                });

                chunkStart = chunkEnd;

                if (chunkStart < endTime)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(CollectorConstants.ChunkDelayMs), cancellationToken);
                }
            }

            _logger.LogInformation("[Manual:{JobId}] Total {Count} incidents retrieved. Success: {Success}, Failed: {Failed}",
                command.JobId, allIncidents.Count, successfulChunks, failedChunks);

            if (allIncidents.Count == 0)
            {
                await UpdateJobStatusAsync(db, statusKey, new ManualCollectStatus
                {
                    JobId = command.JobId,
                    Status = ManualCollectStatusValues.Completed,
                    Progress = 100,
                    Message = "Çekim tamamlandı — Belirtilen tarih aralığında incident bulunamadı.",
                    TotalIncidents = 0,
                    CompletedAt = DateTime.UtcNow
                });
                return;
            }

            // Deduplicate
            var uniqueIncidents = allIncidents
                .GroupBy(i => i.Id)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("[Manual:{JobId}] After deduplication: {UniqueCount} unique incidents",
                command.JobId, uniqueIncidents.Count);

            // Push to Redis (reuse existing logic from CollectIncidentsAsync)
            var pushedCount = 0;
            var errorCount = 0;

            foreach (var dlpIncident in uniqueIncidents)
            {
                try
                {
                    var incident = IncidentMapper.MapFromDLPIncident(dlpIncident);

                    await _collectorService.PushToRedisStreamAsync(incident);
                    pushedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex, "[Manual:{JobId}] Failed to push incident: User={User}",
                        command.JobId, dlpIncident.User);
                }
            }

            _logger.LogInformation("[Manual:{JobId}] Successfully pushed {PushedCount} incidents to Redis (Errors: {ErrorCount})",
                command.JobId, pushedCount, errorCount);

            // Extract and push released incidents
            await ExtractAndPushReleasedIncidentsAsync(uniqueIncidents, $"Manual:{command.JobId}");

            // Final status - Completed
            await UpdateJobStatusAsync(db, statusKey, new ManualCollectStatus
            {
                JobId = command.JobId,
                Status = ManualCollectStatusValues.Completed,
                Progress = 100,
                Message = $"Çekim tamamlandı — {pushedCount} incident Redis'e aktarıldı" +
                          (failedChunks > 0 ? $" ({failedChunks} chunk başarısız)" : ""),
                TotalIncidents = pushedCount,
                CompletedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Manual:{JobId}] Failed to complete manual collection", command.JobId);

            await UpdateJobStatusAsync(db, statusKey, new ManualCollectStatus
            {
                JobId = command.JobId,
                Status = ManualCollectStatusValues.Failed,
                Progress = 0,
                Message = $"Çekim başarısız: {ex.Message}",
                CompletedAt = DateTime.UtcNow
            });
        }
    }

    private async Task UpdateJobStatusAsync(IDatabase db, string key, ManualCollectStatus status)
    {
        try
        {
            var json = JsonSerializer.Serialize(status, _jsonOptions);
            await db.StringSetAsync(key, json, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update job status in Redis for key {Key}", key);
        }
    }

    private async Task CollectIncidentsAsync(int lookbackHours, string runType, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{RunType}] Starting incident collection with {LookbackHours}h lookback...", 
            runType, lookbackHours);

        var endTime = DateTime.Now;
        var startTime = endTime.AddHours(-lookbackHours);
        
        // Time chunk size in hours
        var chunkSizeHours = CollectorConstants.DefaultChunkSizeHours;
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
                
                int maxRetries = CollectorConstants.MaxApiRetries;
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
                        await Task.Delay(TimeSpan.FromSeconds(CollectorConstants.RetryDelaySeconds), cancellationToken);
                    }
                    catch (HttpRequestException) when (attempt < maxRetries)
                    {
                        _logger.LogWarning("[{RunType}] Chunk {ChunkIndex} connection error on attempt {Attempt}. Retrying...", 
                            runType, chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(CollectorConstants.RetryDelaySeconds), cancellationToken);
                    }
                    catch (Exception ex) when (attempt < maxRetries)
                    {
                        _logger.LogWarning(ex, "[{RunType}] Chunk {ChunkIndex} error on attempt {Attempt}. Retrying...", 
                            runType, chunkIndex, attempt);
                        await Task.Delay(TimeSpan.FromSeconds(CollectorConstants.RetryDelaySeconds), cancellationToken);
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
                    await Task.Delay(TimeSpan.FromMilliseconds(CollectorConstants.ChunkDelayMs), cancellationToken);
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
                    if (dlpIncident.ViolationTriggers != null && dlpIncident.ViolationTriggers.Count > 0)
                    {
                        var classifiersWithMatches = dlpIncident.ViolationTriggers
                            .Where(t => t.Classifiers != null && t.Classifiers.Count > 0)
                            .SelectMany(t => t.Classifiers!)
                            .Where(c => c.NumberMatches > 0)
                            .ToList();
                        
                        if (classifiersWithMatches.Count > 0)
                        {
                            maxMatches = classifiersWithMatches.Max(c => c.NumberMatches);
                        }
                        
                        // Debug logging for troubleshooting
                        if (maxMatches == 0 && dlpIncident.ViolationTriggers.Count > 0)
                        {
                            _logger.LogWarning("[{RunType}] Incident {Id}: ViolationTriggers has {TriggerCount} triggers but maxMatches=0. First trigger: PolicyName={PolicyName}, ClassifierCount={ClassifierCount}",
                                runType, dlpIncident.Id, 
                                dlpIncident.ViolationTriggers.Count,
                                dlpIncident.ViolationTriggers[0]?.PolicyName ?? "null",
                                dlpIncident.ViolationTriggers[0]?.Classifiers?.Count ?? 0);
                        }
                    }
                    else if (dlpIncident.ViolationTriggers == null)
                    {
                        _logger.LogDebug("[{RunType}] Incident {Id}: ViolationTriggers is null", runType, dlpIncident.Id);
                    }
                    
                        // Determine effective UserEmail with fallbacks
                        // Priority: 1. LoginName (User) -> 2. EmailAddress -> 3. HostName -> "unknown"
                        string? userIdentifier = dlpIncident.User;
                        
                        if (string.IsNullOrEmpty(userIdentifier))
                            userIdentifier = dlpIncident.Source?.LoginName;
                            
                        if (string.IsNullOrEmpty(userIdentifier))
                            userIdentifier = dlpIncident.Source?.EmailAddress; // or dlpIncident.EmailAddress helper
                            
                        if (string.IsNullOrEmpty(userIdentifier))
                            userIdentifier = dlpIncident.Source?.HostName; // Fallback to HostName
                            
                        if (string.IsNullOrEmpty(userIdentifier))
                            userIdentifier = "unknown";

                        var incident = new DLP.RiskAnalyzer.Shared.Models.Incident
                        {
                            Id = dlpIncident.Id,
                            UserEmail = userIdentifier,
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
                            LoginName = dlpIncident.Source?.LoginName ?? dlpIncident.LoginName, 
                            EmailAddress = dlpIncident.Source?.EmailAddress ?? dlpIncident.EmailAddress,
                            HostName = dlpIncident.Source?.HostName ?? dlpIncident.HostName, // Map HostName

                        // Parse FullName and Team from Manager
                        FullName = !string.IsNullOrEmpty(dlpIncident.Source?.Manager) 
                            ? dlpIncident.Source.Manager.Split('/')[0].Trim() 
                            : null,
                        Team = !string.IsNullOrEmpty(dlpIncident.Source?.Manager) && dlpIncident.Source.Manager.Contains('/')
                            ? (dlpIncident.Source.Manager.Split('/')[1].Contains('-') 
                                ? dlpIncident.Source.Manager.Split('/')[1].Split(new[]{'-'}, 2)[1].Trim() 
                                : dlpIncident.Source.Manager.Split('/')[1].Trim())
                            : null,

                        // Extract RuleName from ViolationTriggers (join multiple rules with ;)
                        RuleName = dlpIncident.ViolationTriggers != null 
                            ? string.Join("; ", dlpIncident.ViolationTriggers
                                .Select(vt => vt.RuleName)
                                .Where(rn => !string.IsNullOrEmpty(rn))
                                .Distinct())
                            : null,

                        // Clean ViolationTriggers: remove duplicate fields, keep only essential ones
                        ViolationTriggers = dlpIncident.ViolationTriggers != null 
                            ? System.Text.Json.JsonSerializer.Serialize(
                                dlpIncident.ViolationTriggers.Select(vt => new 
                                {
                                    policy_name = vt.PolicyName,
                                    rule_name = vt.RuleName,
                                    classifiers = vt.Classifiers?.Select(c => new 
                                    {
                                        classifier_name = c.ClassifierName,
                                        number_matches = c.NumberMatches
                                    }).ToList()
                                }).ToList(), 
                                new System.Text.Json.JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }) 
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

            // Extract and push "Released quarantined message" entries to separate Redis stream
            await ExtractAndPushReleasedIncidentsAsync(uniqueIncidents, runType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RunType}] Failed to collect incidents from Forcepoint DLP API", runType);
            throw;
        }
    }

    /// <summary>
    /// Incident history'den "Released quarantined message" kayıtlarını çıkartıp
    /// dlp:released-incidents Redis stream'ine pushlar.
    /// </summary>
    private async Task ExtractAndPushReleasedIncidentsAsync(List<DLPIncident> incidents, string runType)
    {
        int releasedCount = 0;
        int releasedErrors = 0;

        foreach (var incident in incidents)
        {
            if (incident.History == null || incident.History.Count == 0)
                continue;

            foreach (var historyItem in incident.History)
            {
                if (historyItem.TaskName != CollectorConstants.TaskNameReleasedMessage)
                    continue;

                try
                {
                    await _collectorService.PushReleasedIncidentToRedisStreamAsync(
                        incidentId: incident.Id,
                        incidentTime: incident.IncidentTimeString ?? incident.EventTimeString ?? "",
                        action: incident.Action ?? "",
                        taskName: historyItem.TaskName,
                        adminName: historyItem.AdminName,
                        comments: historyItem.Comments,
                        updateTime: historyItem.UpdateTime
                    );
                    releasedCount++;
                }
                catch (Exception ex)
                {
                    releasedErrors++;
                    _logger.LogWarning(ex, "[{RunType}] Failed to push released incident {Id} to Redis", runType, incident.Id);
                }
            }
        }

        if (releasedCount > 0 || releasedErrors > 0)
        {
            _logger.LogInformation(
                "[{RunType}] Released incidents: {Count} pushed to Redis, {Errors} errors",
                runType, releasedCount, releasedErrors);
        }
    }
}