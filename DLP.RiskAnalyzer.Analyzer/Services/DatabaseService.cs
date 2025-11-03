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

        // Read from stream
        var messages = await db.StreamReadAsync(streamName, "0", count: 100);

        foreach (var message in messages)
        {
            try
            {
                var userEmail = message.Values.FirstOrDefault(v => v.Name == "user").Value.ToString();
                var department = message.Values.FirstOrDefault(v => v.Name == "department").Value.ToString();
                var severity = int.Parse(message.Values.FirstOrDefault(v => v.Name == "severity").Value.ToString());
                var dataType = message.Values.FirstOrDefault(v => v.Name == "data_type").Value.ToString();
                var timestamp = DateTime.Parse(message.Values.FirstOrDefault(v => v.Name == "timestamp").Value.ToString());
                var policy = message.Values.FirstOrDefault(v => v.Name == "policy").Value.ToString();
                var channel = message.Values.FirstOrDefault(v => v.Name == "channel").Value.ToString();

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
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
    }
}

