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

        // Convert dates to UTC for PostgreSQL timestamptz compatibility
        if (startDate.HasValue)
        {
            var utcStartDate = startDate.Value.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) 
                : startDate.Value.ToUniversalTime();
            query = query.Where(i => i.Timestamp >= utcStartDate);
        }

        if (endDate.HasValue)
        {
            var utcEndDate = endDate.Value.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) 
                : endDate.Value.ToUniversalTime();
            query = query.Where(i => i.Timestamp <= utcEndDate);
        }

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

        var totalProcessedCount = 0;
        var totalSkippedCount = 0;
        var totalErrorCount = 0;
        var batchNumber = 0;
        const int batchSize = 500;
        
        // Process messages in two phases: pending first, then new
        // Phase 1: "0" = pending messages (already read but not acknowledged)
        // Phase 2: ">" = new messages (not yet read)
        string[] phases = { "0", ">" };
        
        foreach (var phase in phases)
        {
            var phaseName = phase == "0" ? "PENDING" : "NEW";
            var phaseLimit = phase == "0" ? 3 : 10; // Max batches per phase
            var phaseBatchCount = 0;
            
            while (phaseBatchCount < phaseLimit)
            {
                phaseBatchCount++;
                batchNumber++;
                
                var messages = await db.StreamReadGroupAsync(streamName, consumerGroup, consumerName, phase, count: batchSize);
                
                if (messages.Length == 0)
                {
                    break; // No more messages in this phase
                }

                _logger.LogInformation("Processing {Phase} batch {BatchNum}: {Count} messages", phaseName, batchNumber, messages.Length);

                var processedCount = 0;
                var skippedCount = 0;
                var errorCount = 0;
                
                foreach (var message in messages)
        {
            try
            {
                // DLP API'den gelen orijinal ID
                var idValue = message.Values.FirstOrDefault(v => v.Name == "id");
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

                // Parse ID - DLP API'den gelen orijinal ID
                var incidentId = 0;
                if (idValue.Value.HasValue && !string.IsNullOrEmpty(idValue.Value.ToString()))
                {
                    int.TryParse(idValue.Value.ToString(), out incidentId);
                }

                var userEmail = userEmailValue.Value.ToString();
                
                // Domain prefix'i kaldır (örn: "KUVEYTTURK\enesa" -> "enesa")
                // Network email ve Endpoint kullanıcılarını birleştirmek için
                if (!string.IsNullOrEmpty(userEmail) && userEmail.Contains("\\"))
                {
                    userEmail = userEmail.Split('\\').Last();
                }
                
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
                
                // LoginName için de domain prefix'i kaldır
                if (!string.IsNullOrEmpty(loginName) && loginName.Contains("\\"))
                {
                    loginName = loginName.Split('\\').Last();
                }
                
                var emailAddress = emailAddressValue.Value.HasValue ? emailAddressValue.Value.ToString() : null;
                var violationTriggers = violationTriggersValue.Value.HasValue ? violationTriggersValue.Value.ToString() : null;

                // Check if incident already exists by ID (ID is unique in DLP API)
                var existingIncident = await _context.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId);

                if (existingIncident == null)
                {
                    // New incident - insert
                    var incident = new Incident
                    {
                        Id = incidentId,  // DLP API'den gelen orijinal ID (0 ise auto-increment)
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
                        _logger.LogDebug("SAVED incident {Id} for user {User} at {Timestamp}", incidentId, userEmail, timestamp);
                    }
                    catch (DbUpdateException ex)
                    {
                        // Duplicate key or other constraint violation - skip this one
                        _context.Entry(incident).State = EntityState.Detached;
                        skippedCount++;
                        _logger.LogWarning("DB ERROR for incident {Id}: {Error}", incidentId, ex.InnerException?.Message ?? ex.Message);
                    }
                }
                else if (existingIncident.Action != action && !string.IsNullOrEmpty(action))
                {
                    // Incident exists but action changed (e.g., QUARANTINE -> RELEASE)
                    // Update the existing record
                    _logger.LogInformation(
                        "Incident {Id} action changed from {OldAction} to {NewAction}, updating...",
                        incidentId, existingIncident.Action, action);
                    
                    existingIncident.Action = action;
                    existingIncident.Timestamp = timestamp;  // Update timestamp to latest
                    
                    // Update other fields that might have changed
                    if (!string.IsNullOrEmpty(destination))
                        existingIncident.Destination = destination;
                    if (!string.IsNullOrEmpty(fileName))
                        existingIncident.FileName = fileName;
                    if (!string.IsNullOrEmpty(violationTriggers))
                        existingIncident.ViolationTriggers = violationTriggers;
                    
                    try
                    {
                        await _context.SaveChangesAsync();
                        processedCount++;
                        _logger.LogInformation("Incident {Id} updated with new action: {Action}", incidentId, action);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error updating incident {Id}", incidentId);
                        skippedCount++;
                    }
                }
                else
                {
                    // Duplicate with same action - skip
                    skippedCount++;
                    _logger.LogDebug("SKIPPED duplicate incident {Id} (same action: {Action})", incidentId, existingIncident.Action);
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

        // Track totals across batches
        totalProcessedCount += processedCount;
        totalSkippedCount += skippedCount;
        totalErrorCount += errorCount;

        if (processedCount > 0 || skippedCount > 0)
        {
            _logger.LogInformation("{Phase} batch {BatchNum} completed: {Saved} saved, {Skipped} skipped, {Errors} errors", 
                phaseName, batchNumber, processedCount, skippedCount, errorCount);
        }
            } // end while (phaseBatchCount < phaseLimit)
        } // end foreach (phase)

        if (totalProcessedCount > 0 || totalSkippedCount > 0)
        {
            _logger.LogInformation("Total processed in this cycle: {Saved} saved, {Skipped} skipped, {Errors} errors in {Batches} batches", 
                totalProcessedCount, totalSkippedCount, totalErrorCount, batchNumber);
        }

        return totalProcessedCount;
    }
}