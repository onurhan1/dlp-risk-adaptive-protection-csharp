using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DatabaseService
{
    private readonly AnalyzerDbContext _context;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        AnalyzerDbContext context, 
        StackExchange.Redis.IConnectionMultiplexer redis,
        ILogger<DatabaseService> logger)
    {
        _context = context;
        _redis = redis;
        _logger = logger;
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

    public async Task<int> ProcessRedisStreamAsync()
    {
        var db = _redis.GetDatabase();
        var streamName = "dlp:incidents";
        var consumerGroup = "analyzer";
        var consumerName = Environment.MachineName;

        // Create consumer group if it doesn't exist
        try
        {
            await db.StreamCreateConsumerGroupAsync(streamName, consumerGroup, "0", createStream: true);
            _logger.LogDebug("Created Redis consumer group: {Group}", consumerGroup);
        }
        catch (Exception ex)
        {
            // Group may already exist, which is fine
            _logger.LogDebug("Consumer group may already exist: {Error}", ex.Message);
        }

        // Read from stream using consumer group to track position
        var messages = await db.StreamReadGroupAsync(streamName, consumerGroup, consumerName, ">", count: 100);
        
        if (messages.Length == 0)
        {
            return 0; // No new messages
        }

        _logger.LogDebug("Read {Count} messages from Redis stream", messages.Length);

        var processedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;
        
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
                
                // New fields
                var actionValue = message.Values.FirstOrDefault(v => v.Name == "action");
                var destinationValue = message.Values.FirstOrDefault(v => v.Name == "destination");
                var fileNameValue = message.Values.FirstOrDefault(v => v.Name == "file_name");
                var loginNameValue = message.Values.FirstOrDefault(v => v.Name == "login_name");
                var emailAddressValue = message.Values.FirstOrDefault(v => v.Name == "email_address");
                var violationTriggersValue = message.Values.FirstOrDefault(v => v.Name == "violation_triggers");

                if (userEmailValue.Value.IsNull || severityValue.Value.IsNull || timestampValue.Value.IsNull)
                {
                    _logger.LogWarning("Skipping invalid message {MessageId}: missing required fields", message.Id);
                    await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                    continue;
                }

                var userEmail = userEmailValue.Value.ToString();
                var department = departmentValue.Value.HasValue ? departmentValue.Value.ToString() : null;
                var severity = int.Parse(severityValue.Value.ToString());
                var dataType = dataTypeValue.Value.HasValue ? dataTypeValue.Value.ToString() : null;
                var timestamp = DateTime.Parse(timestampValue.Value.ToString());
                var policy = policyValue.Value.HasValue ? policyValue.Value.ToString() : null;
                var channel = channelValue.Value.HasValue ? channelValue.Value.ToString() : null;
                
                // Parse new fields
                var action = actionValue.Value.HasValue ? actionValue.Value.ToString() : null;
                var destination = destinationValue.Value.HasValue ? destinationValue.Value.ToString() : null;
                var fileName = fileNameValue.Value.HasValue ? fileNameValue.Value.ToString() : null;
                var loginName = loginNameValue.Value.HasValue ? loginNameValue.Value.ToString() : null;
                var emailAddress = emailAddressValue.Value.HasValue ? emailAddressValue.Value.ToString() : null;
                var violationTriggers = violationTriggersValue.Value.HasValue ? violationTriggersValue.Value.ToString() : null;

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
                        Channel = string.IsNullOrEmpty(channel) ? null : channel,
                        // New fields
                        Action = string.IsNullOrEmpty(action) ? null : action,
                        Destination = string.IsNullOrEmpty(destination) ? null : destination,
                        FileName = string.IsNullOrEmpty(fileName) ? null : fileName,
                        LoginName = string.IsNullOrEmpty(loginName) ? null : loginName,
                        EmailAddress = string.IsNullOrEmpty(emailAddress) ? null : emailAddress,
                        ViolationTriggers = string.IsNullOrEmpty(violationTriggers) ? null : violationTriggers
                    };

                    _context.Incidents.Add(incident);
                    
                    // Save immediately to avoid batch failures on duplicates
                    try
                    {
                        await _context.SaveChangesAsync();
                        processedCount++;
                    }
                    catch (DbUpdateException)
                    {
                        // Duplicate key or other constraint violation - skip this one
                        _context.Entry(incident).State = EntityState.Detached;
                        skippedCount++;
                    }
                }
                else
                {
                    skippedCount++;
                }

                // Acknowledge message
                await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Error processing message {MessageId}: {Error}", message.Id, ex.Message);
            }
        }

        if (processedCount > 0 || skippedCount > 0)
        {
            _logger.LogInformation("Processed Redis stream: {Saved} saved, {Skipped} duplicates skipped, {Errors} errors", 
                processedCount, skippedCount, errorCount);
        }

        return processedCount;
    }
}