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

        // Wait a bit for database to be ready
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

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
            catch (Npgsql.NpgsqlException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                // Database connection error - wait longer before retry
                _logger.LogWarning("Database connection failed. Will retry in 30 seconds. Error: {Error}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (ex.InnerException is Npgsql.NpgsqlException)
            {
                // Database connection error - wait longer before retry
                _logger.LogWarning("Database connection failed. Will retry in 30 seconds. Error: {Error}", ex.InnerException?.Message ?? ex.Message);
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

