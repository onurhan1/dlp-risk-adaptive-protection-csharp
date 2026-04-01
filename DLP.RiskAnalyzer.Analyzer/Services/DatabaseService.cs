using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class DatabaseService : IDatabaseService
{
    private readonly AnalyzerDbContext _context;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly PolicyExceptionSyncService _policyExceptionSyncService;
    private readonly ILogger<DatabaseService> _logger;
    private readonly IIncidentRepository _incidentRepository;

    public DatabaseService(
        AnalyzerDbContext context, 
        StackExchange.Redis.IConnectionMultiplexer redis,
        PolicyExceptionSyncService policyExceptionSyncService,
        ILogger<DatabaseService> logger,
        IIncidentRepository incidentRepository)
    {
        _context = context;
        _redis = redis;
        _policyExceptionSyncService = policyExceptionSyncService;
        _logger = logger;
        _incidentRepository = incidentRepository;
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
}
