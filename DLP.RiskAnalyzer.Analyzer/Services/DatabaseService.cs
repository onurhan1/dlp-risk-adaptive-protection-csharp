using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DatabaseService
{
    private readonly AnalyzerDbContext _context;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public DatabaseService(AnalyzerDbContext context, StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis;
    }

    public async Task<List<Incident>> GetIncidentsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? user,
        string? department,
        int limit = 100,
        string orderBy = "timestamp_desc")
    {
        var query = _context.Incidents.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(i => i.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(i => i.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(user))
            query = query.Where(i => i.UserEmail == user);

        if (!string.IsNullOrEmpty(department))
            query = query.Where(i => i.Department == department);

        // Order by
        query = orderBy switch
        {
            "timestamp_asc" => query.OrderBy(i => i.Timestamp),
            "risk_score_desc" => query.OrderByDescending(i => i.RiskScore ?? 0),
            _ => query.OrderByDescending(i => i.Timestamp)
        };

        return await query.Take(limit).ToListAsync();
    }

    public async Task<Incident?> GetIncidentByIdAsync(int id)
    {
        return await _context.Incidents
            .OrderByDescending(i => i.Timestamp)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<int> InsertIncidentAsync(Incident incident)
    {
        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync();
        return incident.Id;
    }

    public async Task ProcessRedisStreamAsync()
    {
        var db = _redis.GetDatabase();
        var streamName = "dlp:incidents";
        var consumerGroup = "analyzer";
        var consumerName = Environment.MachineName;

        // Create consumer group if it doesn't exist
        try
        {
            await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "0", createStream: true);
        }
        catch
        {
            // Group may already exist, which is fine
        }

        // Read from stream using consumer group to track position
        var messages = await db.StreamReadGroupAsync(streamName, consumerGroup, consumerName, ">", count: 100);

        var processedCount = 0;
        foreach (var message in messages)
        {
            try
            {
                var userEmailValue = message.Values.FirstOrDefault(v => v.Name == "user");
                var departmentValue = message.Values.FirstOrDefault(v => v.Name == "department");
                var severityValue = message.Values.FirstOrDefault(v => v.Name == "severity");
                var dataTypeValue = message.Values.FirstOrDefault(v => v.Name == "data_type");
                var timestampValue = message.Values.FirstOrDefault(v => v.Name == "timestamp");
                var policyValue = message.Values.FirstOrDefault(v => v.Name == "policy");
                var channelValue = message.Values.FirstOrDefault(v => v.Name == "channel");

                if (userEmailValue.Value.IsNull || severityValue.Value.IsNull || timestampValue.Value.IsNull)
                {
                    continue; // Skip invalid messages
                }

                var userEmail = userEmailValue.Value.ToString();
                var department = departmentValue.Value.HasValue ? departmentValue.Value.ToString() : null;
                var severity = int.Parse(severityValue.Value.ToString());
                var dataType = dataTypeValue.Value.HasValue ? dataTypeValue.Value.ToString() : null;
                var timestamp = DateTime.Parse(timestampValue.Value.ToString());
                var policy = policyValue.Value.HasValue ? policyValue.Value.ToString() : null;
                var channel = channelValue.Value.HasValue ? channelValue.Value.ToString() : null;

                // Check if incident already exists (avoid duplicates)
                var exists = await _context.Incidents
                    .AnyAsync(i => i.UserEmail == userEmail && 
                                  i.Timestamp == timestamp && 
                                  i.Policy == policy);

                if (!exists)
                {
                    var incident = new Incident
                    {
                        UserEmail = userEmail,
                        Department = string.IsNullOrEmpty(department) ? null : department,
                        Severity = severity,
                        DataType = string.IsNullOrEmpty(dataType) ? null : dataType,
                        Timestamp = timestamp,
                        Policy = string.IsNullOrEmpty(policy) ? null : policy,
                        Channel = string.IsNullOrEmpty(channel) ? null : channel
                    };

                    _context.Incidents.Add(incident);
                    processedCount++;
                }

                // Acknowledge message to remove it from pending list
                await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                Console.WriteLine($"Error processing message {message.Id}: {ex.Message}");
            }
        }

        if (processedCount > 0)
        {
            await _context.SaveChangesAsync();
        }
    }
}

