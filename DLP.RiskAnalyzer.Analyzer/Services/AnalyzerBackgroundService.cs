using DLP.RiskAnalyzer.Analyzer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Background service to continuously process Redis stream and save incidents to database
/// </summary>
public class AnalyzerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyzerBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _exceptionSyncInterval = TimeSpan.FromHours(24);
    private DateTime _lastExceptionSync = DateTime.MinValue;

    public AnalyzerBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AnalyzerBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger          = logger;
        _configuration   = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ── Seed Mode: skip Redis, populate DB from DevDataSeeder ──────────
        if (_configuration.GetValue<bool>("SeedData:Enabled"))
        {
            _logger.LogInformation(
                "[SeedMode] AnalyzerBackgroundService: Redis stream processing DISABLED. " +
                "Running DevDataSeeder instead...");

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); // wait for migrations

            using (var scope = _serviceProvider.CreateScope())
            {
                var seeder = scope.ServiceProvider.GetRequiredService<IDevDataSeeder>();
                await seeder.SeedAsync();
            }

            _logger.LogInformation("[SeedMode] DevDataSeeder complete. Background polling is idle.");

            // Keep the hosted service alive but do nothing
            while (!stoppingToken.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            return;
        }

        // ── Normal Mode: process Redis stream as usual ─────────────────────
        _logger.LogInformation(
            "Analyzer Background Service started. Processing Redis stream every {Interval}s",
            _processingInterval.TotalSeconds);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();
                    var riskAnalyzerService = scope.ServiceProvider.GetRequiredService<IRiskAnalyzerService>();
                    var scoringService = scope.ServiceProvider.GetRequiredService<IRiskScoringService>();

                    // Policy exception sync (24 saatte bir)
                    if ((DateTime.UtcNow - _lastExceptionSync) >= _exceptionSyncInterval)
                    {
                        try
                        {
                            var syncService = scope.ServiceProvider.GetRequiredService<PolicyExceptionSyncService>();
                            var syncedCount = await syncService.SyncAsync();
                            _lastExceptionSync = DateTime.UtcNow;
                            _logger.LogInformation("Policy exception sync completed: {Count} exceptions synced", syncedCount);
                        }
                        catch (Exception syncEx)
                        {
                            _logger.LogWarning(syncEx, "Policy exception sync failed, will retry in next cycle");
                        }
                    }

                    // Process released incidents from Redis stream (Collector pushes to dlp:released-incidents)
                    try
                    {
                        var releasedProcessor = scope.ServiceProvider.GetRequiredService<IReleasedIncidentProcessor>();
                        var releasedProcessed = await releasedProcessor.ProcessReleasedIncidentsStreamAsync();
                        if (releasedProcessed > 0)
                        {
                            _logger.LogInformation("Processed {Count} released incidents from Redis stream", releasedProcessed);
                        }
                    }
                    catch (Exception releasedEx)
                    {
                        _logger.LogWarning(releasedEx, "Released incident stream processing failed, will retry in next cycle");
                    }

                    // Process Redis stream and calculate risk scores
                    var redisProcessor = scope.ServiceProvider.GetRequiredService<IRedisStreamProcessor>();
                    // Note: ProcessRedisStreamAsync internally handled scoring before, but now we probably need to process the stream and THEN calculate scores? 
                    // Actually, let me check how RiskAnalyzerService was managing it. Wait, ProcessRedisStreamAsync returns processedCount. Then it logs.
                    // ProcessRedisStreamAsync itself handles the risks. But wait, did ProcessRedisStreamAsync call CalculateRiskScoresAsync? No, CalculateRiskScoresAsync is a separate batch job for missing scores.
                    // Let's ensure ProcessRedisStreamAsync is still correct.
                    var processedCount = await riskAnalyzerService.ProcessRedisStreamAsync(redisProcessor);
                    
                    if (processedCount > 0)
                    {
                        var scoredCount = await scoringService.CalculateRiskScoresAsync();
                        _logger.LogInformation("Processed {Count} incidents from Redis stream and calculated {ScoredCount} risk scores", 
                            processedCount, scoredCount);
                        
                        // Calculate daily scores for today to keep Dashboard data up-to-date
                        // This populates user_daily_risk_scores table for "Potential Data Exfiltration" and other dashboards
                        try
                        {
                            var today = DateOnly.FromDateTime(DateTime.UtcNow);
                            var updatedUsers = await scoringService.CalculateDailyScoresAsync(today);
                            if (updatedUsers > 0)
                            {
                                _logger.LogInformation("Updated daily risk scores for {Count} users on {Date}", 
                                    updatedUsers, today);
                            }
                        }
                        catch (Exception dailyEx)
                        {
                            _logger.LogWarning(dailyEx, "Failed to calculate daily scores, will retry next cycle");
                        }
                    }
                }
            }
            catch (Npgsql.NpgsqlException ex) when (
                ex.InnerException is System.Net.Sockets.SocketException ||
                ex.Message.Contains("Failed to connect") ||
                ex.Message.Contains("No connection could be made"))
            {
                // Database connection error - wait longer before retry
                // Windows may need more time for services to start
                _logger.LogWarning("Database connection failed. Will retry in 30 seconds. Error: {Error}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (
                ex.InnerException is Npgsql.NpgsqlException ||
                ex.InnerException is System.Net.Sockets.SocketException)
            {
                // Database connection error - wait longer before retry
                _logger.LogWarning("Database connection failed. Will retry in 30 seconds. Error: {Error}", 
                    ex.InnerException?.Message ?? ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                // Direct socket exception (Windows specific sometimes)
                _logger.LogWarning("Socket connection failed. Will retry in 30 seconds. Error: {Error}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Redis stream in background service");
                // Wait a bit before retry on other errors
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            // Wait before next processing cycle (only if no error delay was applied)
            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_processingInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Analyzer Background Service stopped");
    }
}

