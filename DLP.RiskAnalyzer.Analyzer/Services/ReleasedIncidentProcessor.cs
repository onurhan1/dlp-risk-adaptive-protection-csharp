using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using DLP.RiskAnalyzer.Analyzer.Constants;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IReleasedIncidentProcessor
{
    Task<int> ProcessReleasedIncidentsStreamAsync();
}

public class ReleasedIncidentProcessor : IReleasedIncidentProcessor
{
    private readonly AnalyzerDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ReleasedIncidentProcessor> _logger;

    public ReleasedIncidentProcessor(
        AnalyzerDbContext context,
        IConnectionMultiplexer redis,
        ILogger<ReleasedIncidentProcessor> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// dlp:released-incidents Redis stream'ini okur ve released_incidents tablosuna kaydeder.
    /// Aynı ProcessRedisStreamAsync pattern'ını takip eder.
    /// </summary>
    public async Task<int> ProcessReleasedIncidentsStreamAsync()
    {
        var db = _redis.GetDatabase();
        var streamName = "dlp:released-incidents";
        var consumerGroup = "analyzer-released";
        var consumerName = Environment.MachineName;

        try
        {
            await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "0", createStream: true);
            _logger.LogDebug("Created Redis consumer group for released incidents: {Group}", consumerGroup);
        }
        catch (Exception ex)
        {
            // Group may already exist — expected on repeat startup
            _logger.LogDebug("Redis consumer group for released incidents may already exist: {Error}", ex.Message);
        }

        var totalProcessed = 0;
        var totalSkipped = 0;
        const int batchSize = RedisProcessorConstants.DefaultBatchSize;

        string[] phases = { "0", ">" };

        foreach (var phase in phases)
        {
            var maxBatches = phase == "0" ? RedisProcessorConstants.ReleasedPendingPhaseMaxBatches : RedisProcessorConstants.ReleasedNewPhaseMaxBatches;
            var batchCount = 0;

            while (batchCount < maxBatches)
            {
                batchCount++;
                var messages = await db.StreamReadGroupAsync(streamName, consumerGroup, consumerName, phase, count: batchSize);

                if (messages.Length == 0)
                    break;

                foreach (var message in messages)
                {
                    try
                    {
                        var incidentIdStr = message.Values.FirstOrDefault(v => v.Name == "incident_id").Value.ToString();
                        var incidentTime = message.Values.FirstOrDefault(v => v.Name == "incident_time").Value.ToString();
                        var action = message.Values.FirstOrDefault(v => v.Name == "action").Value.ToString();
                        var taskName = message.Values.FirstOrDefault(v => v.Name == "task_name").Value.ToString();
                        var adminName = message.Values.FirstOrDefault(v => v.Name == "admin_name").Value.ToString();
                        var comments = message.Values.FirstOrDefault(v => v.Name == "comments").Value.ToString();
                        var updateTimeStr = message.Values.FirstOrDefault(v => v.Name == "update_time").Value.ToString();

                        if (!long.TryParse(incidentIdStr, out var incidentId) || incidentId <= 0)
                        {
                            await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                            continue;
                        }

                        DateTime? incidentTimestamp = TryParseDateMultiFormat(incidentTime);
                        DateTime? updateTime = TryParseDateMultiFormat(updateTimeStr);

                        var exists = await _context.ReleasedIncidents
                            .AnyAsync(r => r.IncidentId == incidentId && r.UpdateTime == updateTime);

                        if (exists)
                        {
                            totalSkipped++;
                            await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                            continue;
                        }

                        var released = new ReleasedIncident
                        {
                            IncidentId = incidentId,
                            IncidentTimestamp = incidentTimestamp ?? DateTime.MinValue,
                            Action = string.IsNullOrEmpty(action) ? "" : action,
                            TaskName = string.IsNullOrEmpty(taskName) ? RedisProcessorConstants.TaskNameReleasedMessage : taskName,
                            AdminName = string.IsNullOrEmpty(adminName) ? null : adminName,
                            Comments = string.IsNullOrEmpty(comments) ? null : comments,
                            UpdateTime = updateTime
                        };

                        _context.ReleasedIncidents.Add(released);
                        await _context.SaveChangesAsync();
                        totalProcessed++;

                        await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                    }
                    catch (DbUpdateException)
                    {
                        totalSkipped++;
                        _context.ChangeTracker.Clear();
                        try { await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id); } catch (Exception ackEx) { _logger.LogDebug(ackEx, "Redis ACK failed after DbUpdateException for message {MessageId}", message.Id); }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing released incident message {MessageId}", message.Id);
                        _context.ChangeTracker.Clear();
                        try { await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id); } catch (Exception ackEx) { _logger.LogDebug(ackEx, "Redis ACK failed after processing error for message {MessageId}", message.Id); }
                    }
                }
            }
        }

        if (totalProcessed > 0 || totalSkipped > 0)
        {
            _logger.LogInformation("Released incidents from Redis: {Processed} saved, {Skipped} skipped",
                totalProcessed, totalSkipped);
        }

        return totalProcessed;
    }

    private static readonly string[] _dateFormats = {
        "dd/MM/yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "dd-MM-yyyy HH:mm:ss"
    };

    private DateTime? TryParseDateMultiFormat(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return null;

        foreach (var format in _dateFormats)
        {
            if (DateTime.TryParseExact(dateStr, format, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
                return parsed;
        }

        if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var fallback))
            return fallback;

        return null;
    }

}
