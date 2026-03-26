using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DatabaseService
{
    private readonly AnalyzerDbContext _context;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly PolicyExceptionSyncService _policyExceptionSyncService;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(
        AnalyzerDbContext context, 
        StackExchange.Redis.IConnectionMultiplexer redis,
        PolicyExceptionSyncService policyExceptionSyncService,
        ILogger<DatabaseService> logger)
    {
        _context = context;
        _redis = redis;
        _policyExceptionSyncService = policyExceptionSyncService;
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
            // Ensure end-of-day so the entire end date is included (23:59:59.9999999)
            var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
            var utcEndDate = endOfDay.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(endOfDay, DateTimeKind.Utc)
                : endOfDay.ToUniversalTime();
            query = query.Where(i => i.Timestamp <= utcEndDate);
        }

        if (!string.IsNullOrEmpty(user))
        {
            var userLower = user.ToLower();
            query = query.Where(i => i.UserEmail.ToLower() == userLower 
                || (i.EmailAddress != null && i.EmailAddress.ToLower() == userLower)
                || (i.LoginName != null && i.LoginName.ToLower() == userLower)
                || (i.FullName != null && i.FullName.ToLower() == userLower));
        }

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

    /// <summary>
    /// Returns per policy_name + rule_name aggregated incident counts and last incident date.
    /// Uses PostgreSQL jsonb_array_elements to parse ViolationTriggers JSON directly in SQL.
    /// </summary>
    public async Task<List<ExceptionIncidentStats>> GetExceptionIncidentStatsAsync(
        DateTime? startDate, DateTime? endDate)
    {
        var results = new List<ExceptionIncidentStats>();

        var conn = _context.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();

            // Parametreli SQL — string interpolasyon ile SQL injection riski önlendi
            var sql = @"
                SELECT
                    trigger_elem->>'policy_name' AS policy_name,
                    trigger_elem->>'rule_name' AS rule_name,
                    COUNT(*) AS incident_count,
                    MAX(i.""timestamp"") AS last_incident_date
                FROM incidents i,
                    jsonb_array_elements(i.violation_triggers::jsonb) AS trigger_elem
                WHERE i.violation_triggers IS NOT NULL
                    AND i.violation_triggers != '[]'
                    AND i.violation_triggers != ''";

            if (startDate.HasValue)
            {
                var utcStart = startDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc)
                    : startDate.Value.ToUniversalTime();
                sql += " AND i.\"timestamp\" >= @startDate";
                var p = cmd.CreateParameter();
                p.ParameterName = "@startDate";
                p.Value = utcStart;
                cmd.Parameters.Add(p);
            }
            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                var utcEnd = endOfDay.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(endOfDay, DateTimeKind.Utc)
                    : endOfDay.ToUniversalTime();
                sql += " AND i.\"timestamp\" <= @endDate";
                var p = cmd.CreateParameter();
                p.ParameterName = "@endDate";
                p.Value = utcEnd;
                cmd.Parameters.Add(p);
            }

            sql += @"
                GROUP BY trigger_elem->>'policy_name', trigger_elem->>'rule_name'
                ORDER BY incident_count DESC";

            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new ExceptionIncidentStats
                {
                    PolicyName = reader.IsDBNull(0) ? "" : reader.GetString(0),
                    RuleName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    IncidentCount = reader.GetInt64(2),
                    LastIncidentDate = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                });
            }
        }
        finally
        {
            await conn.CloseAsync();
        }

        return results;
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

        // Exception lookup yükle (exception_name -> parent_rule_name)
        Dictionary<string, string> exceptionLookup;
        try
        {
            exceptionLookup = await _policyExceptionSyncService.GetExceptionLookupAsync();
            if (exceptionLookup.Count > 0)
                _logger.LogDebug("Loaded {Count} policy exception mappings for ViolationTriggers enrichment", exceptionLookup.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load policy exception lookup, proceeding without exception enrichment");
            exceptionLookup = new Dictionary<string, string>();
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
                // P-04: collect new incidents for a single batch SaveChangesAsync
                var pendingInserts = new List<Incident>();

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
                var maxMatchesValue = message.Values.FirstOrDefault(v => v.Name == "max_matches");
                
                // New fields for missing data
                var fullNameValue = message.Values.FirstOrDefault(v => v.Name == "full_name");
                var teamValue = message.Values.FirstOrDefault(v => v.Name == "team");
                var ruleNameValue = message.Values.FirstOrDefault(v => v.Name == "rule_name");

                // Parse ID first - DLP API'den gelen orijinal ID
                var incidentId = 0;
                if (idValue.Value.HasValue && !string.IsNullOrEmpty(idValue.Value.ToString()))
                {
                    int.TryParse(idValue.Value.ToString(), out incidentId);
                }

                // Validation: ID zorunlu - yoksa veya 0 ise bir sorun var demektir
                if (incidentId <= 0)
                {
                    _logger.LogWarning("Skipping message {MessageId}: missing or invalid ID", message.Id);
                    await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                    continue;
                }
                
                // Timestamp zorunlu
                if (timestampValue.Value.IsNull)
                {
                    _logger.LogWarning("Skipping message {MessageId}: ID={Id} but missing timestamp", message.Id, incidentId);
                    await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
                    continue;
                }

                // userEmail: önce user alanını dene, yoksa email_address'e, sonra login_name'e fallback yap
                var rawUserEmail = userEmailValue.Value.HasValue ? userEmailValue.Value.ToString() : null;
                
                // Domain prefix'i kaldır (örn: "KUVEYTTURK\enesa" -> "enesa")
                // Network email ve Endpoint kullanıcılarını birleştirmek için
                if (!string.IsNullOrEmpty(rawUserEmail) && rawUserEmail.Contains("\\"))
                {
                    rawUserEmail = rawUserEmail.Split('\\').Last();
                }

                // Parse new fields early for fallback
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
                
                // Parse fullName early as well for the final fallback
                var fullName = fullNameValue.Value.HasValue ? fullNameValue.Value.ToString() : null;

                // Global Email-to-Login Fallback
                if (!string.IsNullOrEmpty(emailAddress) && emailAddress.Contains('@'))
                {
                    var emailPrefix = emailAddress.Split('@')[0];
                    if (string.IsNullOrEmpty(loginName)) loginName = emailPrefix;
                    if (string.IsNullOrEmpty(fullName)) fullName = emailPrefix;
                    
                    // If user is empty or identical to the full email address, use the prefix instead
                    if (string.IsNullOrEmpty(rawUserEmail) || rawUserEmail.Equals(emailAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        rawUserEmail = emailPrefix;
                    }
                }

                // Fallback hiyerarşisi: user → email_address → login_name → full_name → "unknown"
                string userEmail;
                if (!string.IsNullOrWhiteSpace(rawUserEmail))
                    userEmail = rawUserEmail;
                else if (!string.IsNullOrWhiteSpace(emailAddress))
                    userEmail = emailAddress;
                else if (!string.IsNullOrWhiteSpace(loginName))
                    userEmail = loginName;
                else if (!string.IsNullOrWhiteSpace(fullName))
                    userEmail = fullName;
                else
                    userEmail = "unknown";
                
                var department = departmentValue.Value.HasValue ? departmentValue.Value.ToString() : null;
                var severity = severityValue.Value.HasValue && int.TryParse(severityValue.Value.ToString(), out var sev) ? sev : 1;
                var dataType = dataTypeValue.Value.HasValue ? dataTypeValue.Value.ToString() : null;
                var timestamp = DateTime.Parse(timestampValue.Value.ToString());
                var policy = policyValue.Value.HasValue ? policyValue.Value.ToString() : null;
                var channel = channelValue.Value.HasValue ? channelValue.Value.ToString() : null;
                
                // hostName wasn't parsed earlier
                var hostNameValue = message.Values.FirstOrDefault(v => v.Name == "host_name");
                var hostName = hostNameValue.Value.HasValue ? hostNameValue.Value.ToString() : null;

                var violationTriggers = violationTriggersValue.Value.HasValue ? violationTriggersValue.Value.ToString() : null;
                
                // Parse new fields (Team, RuleName)
                var team = teamValue.Value.HasValue ? teamValue.Value.ToString() : null;
                var ruleName = ruleNameValue.Value.HasValue ? ruleNameValue.Value.ToString() : null;

                // Check if incident already exists by ID (ID is unique in DLP API)
                var existingIncident = await _context.Incidents.FirstOrDefaultAsync(i => i.Id == incidentId);

                if (existingIncident == null)
                {
                    // New incident — stage for batch insert (P-04)
                    var incident = new Incident
                    {
                        Id                = incidentId,
                        UserEmail         = userEmail,
                        Department        = string.IsNullOrEmpty(department)        ? null : department,
                        Severity          = severity,
                        DataType          = string.IsNullOrEmpty(dataType)          ? null : dataType,
                        Timestamp         = timestamp,
                        Policy            = string.IsNullOrEmpty(policy)            ? null : policy,
                        Channel           = string.IsNullOrEmpty(channel)           ? null : channel,
                        Action            = string.IsNullOrEmpty(action)            ? null : action,
                        Destination       = string.IsNullOrEmpty(destination)       ? null : destination,
                        FileName          = string.IsNullOrEmpty(fileName)          ? null : fileName,
                        LoginName         = string.IsNullOrEmpty(loginName)         ? null : loginName,
                        HostName          = string.IsNullOrEmpty(hostName)          ? null : hostName,
                        EmailAddress      = string.IsNullOrEmpty(emailAddress)      ? null : emailAddress,
                        ViolationTriggers = string.IsNullOrEmpty(violationTriggers) ? null : DeduplicateViolationTriggers(violationTriggers, exceptionLookup),
                        FullName          = string.IsNullOrEmpty(fullName)          ? null : fullName,
                        Team              = string.IsNullOrEmpty(team)              ? null : team,
                        RuleName          = string.IsNullOrEmpty(ruleName)          ? null : ruleName,
                        MaxMatches        = ParseMaxMatchesFromRedis(maxMatchesValue, violationTriggers)
                    };

                    pendingInserts.Add(incident);
                    _context.Incidents.Add(incident);
                }
                else if (existingIncident.Action != action && !string.IsNullOrEmpty(action))
                {
                    _logger.LogInformation(
                        "Incident {Id} action changed from {OldAction} to {NewAction}, updating...",
                        incidentId, existingIncident.Action, action);

                    existingIncident.Action    = action;
                    existingIncident.Timestamp = timestamp;
                    existingIncident.RiskScore = null;  // Force recalculation with new action multiplier

                    if (!string.IsNullOrEmpty(destination)) existingIncident.Destination = destination;
                    if (!string.IsNullOrEmpty(fileName))    existingIncident.FileName    = fileName;
                    if (!string.IsNullOrEmpty(violationTriggers))
                    {
                        var dedupedTriggers = DeduplicateViolationTriggers(violationTriggers, exceptionLookup);
                        existingIncident.ViolationTriggers = dedupedTriggers;
                        existingIncident.MaxMatches        = CalculateMaxMatches(dedupedTriggers);
                    }
                    if (!string.IsNullOrEmpty(fullName))  existingIncident.FullName  = fullName;
                    if (!string.IsNullOrEmpty(team))      existingIncident.Team      = team;
                    if (!string.IsNullOrEmpty(ruleName))  existingIncident.RuleName  = ruleName;

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
                else if (string.IsNullOrEmpty(existingIncident.ViolationTriggers) || existingIncident.ViolationTriggers == "[]")
                {
                    if (!string.IsNullOrEmpty(violationTriggers) && violationTriggers != "[]")
                    {
                        _logger.LogInformation(
                            "Incident {Id} missing violation_triggers, backfilling from Redis...",
                            incidentId);

                        var dedupedTriggers = DeduplicateViolationTriggers(violationTriggers, exceptionLookup);
                        existingIncident.ViolationTriggers = dedupedTriggers;
                        existingIncident.MaxMatches        = CalculateMaxMatches(dedupedTriggers);

                        if (string.IsNullOrEmpty(existingIncident.FullName)  && !string.IsNullOrEmpty(fullName))  existingIncident.FullName  = fullName;
                        if (string.IsNullOrEmpty(existingIncident.Team)      && !string.IsNullOrEmpty(team))      existingIncident.Team      = team;
                        if (string.IsNullOrEmpty(existingIncident.RuleName)  && !string.IsNullOrEmpty(ruleName))  existingIncident.RuleName  = ruleName;

                        try
                        {
                            await _context.SaveChangesAsync();
                            processedCount++;
                            _logger.LogInformation("Incident {Id} backfilled with violation_triggers, max_matches={MaxMatches}",
                                incidentId, existingIncident.MaxMatches);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error backfilling incident {Id}", incidentId);
                            skippedCount++;
                        }
                    }
                    else
                    {
                        skippedCount++;
                        _logger.LogDebug("SKIPPED incident {Id} (both existing and new have empty violation_triggers)", incidentId);
                    }
                }
                else if (string.IsNullOrEmpty(existingIncident.EmailAddress) && !string.IsNullOrEmpty(emailAddress))
                {
                    // EmailAddress backfill: mevcut kayıtta email_address boş ama yeni veride dolu
                    _logger.LogInformation(
                        "Incident {Id} missing email_address, backfilling: {EmailAddress}",
                        incidentId, emailAddress);

                    existingIncident.EmailAddress = emailAddress;

                    // UserEmail "unknown" ise veya hostname ise email ile güncelle
                    if (existingIncident.UserEmail == "unknown" || 
                        existingIncident.UserEmail == existingIncident.HostName)
                    {
                        existingIncident.UserEmail = emailAddress;
                        _logger.LogInformation(
                            "Incident {Id} UserEmail updated from '{OldValue}' to '{NewValue}'",
                            incidentId, existingIncident.UserEmail, emailAddress);
                    }

                    // Diğer boş alanları da doldur
                    if (string.IsNullOrEmpty(existingIncident.FullName)  && !string.IsNullOrEmpty(fullName))  existingIncident.FullName  = fullName;
                    if (string.IsNullOrEmpty(existingIncident.Team)      && !string.IsNullOrEmpty(team))      existingIncident.Team      = team;
                    if (string.IsNullOrEmpty(existingIncident.LoginName) && !string.IsNullOrEmpty(loginName)) existingIncident.LoginName = loginName;
                    if (string.IsNullOrEmpty(existingIncident.HostName)  && !string.IsNullOrEmpty(hostName))  existingIncident.HostName  = hostName;
                    if (string.IsNullOrEmpty(existingIncident.RuleName)  && !string.IsNullOrEmpty(ruleName))  existingIncident.RuleName  = ruleName;

                    try
                    {
                        await _context.SaveChangesAsync();
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error backfilling email_address for incident {Id}", incidentId);
                        skippedCount++;
                    }
                }
                else
                {
                    skippedCount++;
                    _logger.LogDebug("SKIPPED duplicate incident {Id} (same action: {Action})", incidentId, existingIncident.Action);
                }

                // Acknowledge message regardless
                await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id);
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Error processing message {MessageId}: {Error}", message.Id, ex.Message);
                // P-04 / C-03 fix: acknowledge even on parse errors to avoid infinite-retry loop
                try { await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id); }
                catch (Exception ackEx) { _logger.LogError(ackEx, "Failed to acknowledge message {MessageId} after error", message.Id); }
            }
        }

        // ── P-04: Flush pending inserts as a single batch ───────────────────────
        if (pendingInserts.Count > 0)
        {
            try
            {
                await _context.SaveChangesAsync();
                processedCount += pendingInserts.Count;
                _logger.LogDebug("Batch saved {Count} new incidents", pendingInserts.Count);
            }
            catch (DbUpdateException ex)
            {
                // One or more rows conflicted; save them individually to isolate the bad one
                _logger.LogWarning("Batch insert failed ({Error}), retrying individually...", ex.InnerException?.Message ?? ex.Message);

                foreach (var inc in pendingInserts)
                {
                    _context.Entry(inc).State = EntityState.Detached;
                }

                foreach (var inc in pendingInserts)
                {
                    try
                    {
                        _context.Incidents.Add(inc);
                        await _context.SaveChangesAsync();
                        processedCount++;
                    }
                    catch (DbUpdateException dupEx)
                    {
                        _context.Entry(inc).State = EntityState.Detached;
                        skippedCount++;
                        _logger.LogWarning("Skipped duplicate incident {Id}: {Error}", inc.Id, dupEx.InnerException?.Message ?? dupEx.Message);
                    }
                }
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
        catch
        {
            // Group may already exist
        }

        var totalProcessed = 0;
        var totalSkipped = 0;
        const int batchSize = 500;

        string[] phases = { "0", ">" };

        foreach (var phase in phases)
        {
            var maxBatches = phase == "0" ? 3 : 5;
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
                            TaskName = string.IsNullOrEmpty(taskName) ? "Released quarantined message" : taskName,
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
                        try { await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id); } catch { }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing released incident message {MessageId}", message.Id);
                        _context.ChangeTracker.Clear();
                        try { await db.StreamAcknowledgeAsync(streamName, consumerGroup, message.Id); } catch { }
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

    /// <summary>
    /// Parse max_matches from Redis stream, fallback to calculating from ViolationTriggers JSON
    /// </summary>
    private int ParseMaxMatchesFromRedis(StackExchange.Redis.NameValueEntry maxMatchesValue, string? violationTriggers)
    {
        // Önce Redis'ten direkt max_matches değerini dene
        if (maxMatchesValue.Value.HasValue && !string.IsNullOrEmpty(maxMatchesValue.Value.ToString()))
        {
            if (int.TryParse(maxMatchesValue.Value.ToString(), out int parsedMax) && parsedMax > 0)
            {
                _logger.LogDebug("Using max_matches from Redis: {MaxMatches}", parsedMax);
                return parsedMax;
            }
        }
        
        // Fallback: ViolationTriggers'dan hesapla
        var dedupedTriggers = string.IsNullOrEmpty(violationTriggers) ? null : DeduplicateViolationTriggers(violationTriggers, new Dictionary<string, string>());
        var calculated = CalculateMaxMatches(dedupedTriggers);
        _logger.LogDebug("Calculated max_matches from ViolationTriggers: {MaxMatches}", calculated);
        return calculated;
    }

    private int CalculateMaxMatches(string? violationTriggersJson)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return 0;

        try
        {
            var maxMatches = 0;
            // Parse JSON manually to support multiple casing (snake, Pascal, camel)
            using var doc = System.Text.Json.JsonDocument.Parse(violationTriggersJson);
            
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var trigger in doc.RootElement.EnumerateArray())
                {
                    // Check for classifiers/Classifiers
                    System.Text.Json.JsonElement classifiers;
                    if (trigger.TryGetProperty("classifiers", out classifiers) || 
                        trigger.TryGetProperty("Classifiers", out classifiers))
                    {
                        if (classifiers.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var classifier in classifiers.EnumerateArray())
                            {
                                int matches = 0;
                                // Check all casing variants for NumberMatches (do not rely on || short-circuit as serialized JSON might have all keys)
                                if (classifier.TryGetProperty("number_matches", out var m1) && m1.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var val = m1.GetInt32();
                                    if (val > matches) matches = val;
                                }
                                
                                if (classifier.TryGetProperty("NumberMatches", out var m2) && m2.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var val = m2.GetInt32();
                                    if (val > matches) matches = val;
                                }

                                if (classifier.TryGetProperty("numberMatches", out var m3) && m3.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var val = m3.GetInt32();
                                    if (val > matches) matches = val;
                                }
                                
                                if (matches > maxMatches) maxMatches = matches;
                            }
                            }

                    }
                }
            }
            return maxMatches;
        }
        catch (Exception ex)
        {
            // Fallback to 0 if parsing fails
            _logger.LogWarning("Failed to parse violation triggers for max matches: {Error}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// ViolationTriggers JSON'ını temizler, duplicate'leri kaldırır ve exception bilgisiyle zenginleştirir.
    /// exceptionLookup: exception_name → parent_rule_name eşlemesi
    /// </summary>
    private string DeduplicateViolationTriggers(string? violationTriggersJson, Dictionary<string, string> exceptionLookup)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return "[]";

        try
        {
            var options = new System.Text.Json.JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                WriteIndented = false 
            };
            
            // Parse as JSON document to handle various field names
            using var doc = System.Text.Json.JsonDocument.Parse(violationTriggersJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return "[]";

            // Deduplicate and clean based on policy_name and rule_name
            var uniqueTriggers = new List<Dictionary<string, object?>>(); 
            var seenKeys = new HashSet<string>();

            foreach (var trigger in doc.RootElement.EnumerateArray())
            {
                // Extract policy_name (from various field names)
                string? policyName = GetJsonStringValue(trigger, "policy_name", "PolicyName", "PolicyNameSnake", "PolicyNamePascal");
                
                // Extract rule_name (from various field names)
                string? ruleName = GetJsonStringValue(trigger, "rule_name", "RuleName", "RuleNameSnake", "RuleNamePascal", "RuleNameCamel");

                var key = $"{policyName ?? "unknown"}|{ruleName ?? "unknown"}";
                
                if (!seenKeys.Contains(key))
                {
                    seenKeys.Add(key);
                    
                    // Build clean classifier list
                    var cleanClassifiers = new List<Dictionary<string, object?>>(); 
                    if (trigger.TryGetProperty("classifiers", out var classifiersElem) || 
                        trigger.TryGetProperty("Classifiers", out classifiersElem))
                    {
                        if (classifiersElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var classifier in classifiersElem.EnumerateArray())
                            {
                                var classifierName = GetJsonStringValue(classifier, "classifier_name", "ClassifierName", "ClassifierNameSnake", "ClassifierNamePascal");
                                var numberMatches = GetJsonIntValue(classifier, "number_matches", "NumberMatches", "NumberMatchesSnake", "NumberMatchesPascal", "NumberMatchesCamel");
                                
                                cleanClassifiers.Add(new Dictionary<string, object?>
                                {
                                    ["classifier_name"] = classifierName,
                                    ["number_matches"] = numberMatches
                                });
                            }
                        }
                    }
                    
                    // Exception kontrolü: policy_name + rule_name composite key ile kontrol et
                    var exceptionKey = $"{policyName}|{ruleName}";
                    var isException = !string.IsNullOrEmpty(ruleName) && !string.IsNullOrEmpty(policyName) 
                        && exceptionLookup.ContainsKey(exceptionKey);
                    string? parentRuleName = isException ? exceptionLookup[exceptionKey] : null;
                    
                    // Add clean trigger with essential fields + exception info
                    var triggerDict = new Dictionary<string, object?>
                    {
                        ["policy_name"] = policyName,
                        ["rule_name"] = ruleName,
                        ["classifiers"] = cleanClassifiers,
                        ["is_exception"] = isException
                    };
                    
                    if (isException)
                    {
                        triggerDict["parent_rule_name"] = parentRuleName;
                    }
                    
                    uniqueTriggers.Add(triggerDict);
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(uniqueTriggers, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to deduplicate violation triggers: {Error}", ex.Message);
            return violationTriggersJson; // Return original if parsing fails
        }
    }

    private string? GetJsonStringValue(System.Text.Json.JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private int GetJsonIntValue(System.Text.Json.JsonElement element, params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            if (element.TryGetProperty(fieldName, out var prop))
            {
                if (prop.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    var value = prop.GetInt32();
                    if (value > 0) return value;
                }
            }
        }
        return 0;
    }
}