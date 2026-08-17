namespace DLP.RiskAnalyzer.Analyzer.Services;

public class ScheduledJobBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledJobBackgroundService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(60);

    public ScheduledJobBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledJobBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IScheduledJobService>();
                await scheduler.RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled job background tick failed");
            }

            await Task.Delay(_tickInterval, stoppingToken);
        }
    }
}
