using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Helpers;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Fires enabled playbooks when their cron schedule comes due. Follows the same shape as
/// <see cref="AnalyzerBackgroundService"/>: a scoped service provider per tick and a longer
/// backoff on database connectivity errors so a cold start does not spam the log.
/// </summary>
public class PlaybookSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlaybookSchedulerService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(60);

    public PlaybookSchedulerService(IServiceProvider serviceProvider, ILogger<PlaybookSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Playbook Scheduler started. Checking for due playbooks every {Interval}s",
            _tickInterval.TotalSeconds);

        // Give migrations and the rest of the host a moment before the first query.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.LogWarning("Playbook scheduler: database unavailable, retrying in 30s. {Error}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playbook scheduler tick failed");
            }

            if (!stoppingToken.IsCancellationRequested)
                await Task.Delay(_tickInterval, stoppingToken);
        }

        _logger.LogInformation("Playbook Scheduler stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AnalyzerDbContext>();

        await PlaybookSchema.EnsureAsync(context, _logger, ct);

        var now = DateTime.UtcNow;
        var due = await context.Playbooks
            .Where(p => p.Enabled && p.NextRunAt != null && p.NextRunAt <= now)
            .OrderBy(p => p.NextRunAt)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        var engine = scope.ServiceProvider.GetRequiredService<IPlaybookEngine>();

        foreach (var playbook in due)
        {
            // Advance the schedule before running: if the run throws, the playbook still moves on
            // to its next slot instead of retrying in a tight loop every 60 seconds.
            playbook.NextRunAt = CronSchedule.Next(playbook.ScheduleCron, now, RadarTimeZone.Turkey);
            playbook.UpdatedAt = now;

            if (playbook.NextRunAt == null)
            {
                playbook.Enabled = false;
                _logger.LogWarning(
                    "Playbook {PlaybookId} ('{Name}') disabled: schedule '{Cron}' has no upcoming run",
                    playbook.Id, playbook.Name, playbook.ScheduleCron);
            }

            await context.SaveChangesAsync(ct);

            try
            {
                _logger.LogInformation(
                    "Running scheduled playbook {PlaybookId} ('{Name}')", playbook.Id, playbook.Name);

                var run = await engine.RunAsync(playbook.Id, PlaybookTriggerType.Schedule, null, ct);

                _logger.LogInformation(
                    "Scheduled playbook {PlaybookId} finished: {Status} (sent {Sent}, pending {Pending}, failed {Failed})",
                    playbook.Id, run.Status, run.MailsSent, run.MailsPending, run.MailsFailed);
            }
            catch (Exception ex)
            {
                // RunAsync already records a failed run; keep the loop alive for other playbooks.
                _logger.LogError(ex, "Scheduled playbook {PlaybookId} could not be started", playbook.Id);
            }
        }
    }
}
