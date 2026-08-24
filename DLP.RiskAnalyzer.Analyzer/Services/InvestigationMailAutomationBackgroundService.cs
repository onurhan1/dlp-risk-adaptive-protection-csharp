namespace DLP.RiskAnalyzer.Analyzer.Services;

public class InvestigationMailAutomationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvestigationMailAutomationBackgroundService> _logger;

    public InvestigationMailAutomationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<InvestigationMailAutomationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var automation = scope.ServiceProvider.GetRequiredService<IInvestigationMailAutomationService>();
                await automation.ProcessInboxAsync(stoppingToken);
                await automation.MarkUnansweredRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Investigation mail automation tick failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
