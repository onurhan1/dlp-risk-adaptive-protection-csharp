using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;

/// <summary>
/// Repository implementation for Incident data access operations
/// </summary>
public class IncidentRepository : IIncidentRepository
{
    private readonly AnalyzerDbContext _context;

    public IncidentRepository(AnalyzerDbContext context)
    {
        _context = context;
    }

    public async Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsAsync(DateOnly startDate, DateOnly endDate, int page, int pageSize)
    {
        return await _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .OrderByDescending(i => i.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsByUserAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.UserEmail == userEmail &&
                       i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsByDepartmentAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue) &&
                       !string.IsNullOrEmpty(i.Department))
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsWithoutRiskScoreAsync()
    {
        return await _context.Incidents
            .Where(i => i.RiskScore == null)
            .ToListAsync();
    }

    public async Task<int> GetPreviousIncidentsCountAsync(string userEmail, DateTime beforeDate)
    {
        return await _context.Incidents
            .CountAsync(i => i.UserEmail == userEmail && i.Timestamp < beforeDate);
    }

    public async Task<int> UpdateIncidentsAsync(IEnumerable<Incident> incidents)
    {
        _context.Incidents.UpdateRange(incidents);
        return await _context.SaveChangesAsync();
    }

    public async Task<List<Incident>> GetIncidentsByChannelAsync(DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue) &&
                       !string.IsNullOrEmpty(i.Channel))
            .ToListAsync();
    }

    public async Task<List<Incident>> GetRecentIncidentsAsync(int count)
    {
        return await _context.Incidents
            .OrderByDescending(i => i.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Incident>> GetIncidentsForAnomalyDetectionAsync(string userEmail, DateOnly startDate, DateOnly endDate)
    {
        return await _context.Incidents
            .Where(i => i.UserEmail == userEmail &&
                       i.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       i.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
    }

    public async Task SaveAnomalyAsync(AnomalyDetection anomaly)
    {
        _context.AnomalyDetections.Add(anomaly);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AnomalyDetection>> GetAnomaliesAsync(DateOnly startDate, DateOnly endDate, string? severity)
    {
        var query = _context.AnomalyDetections
            .Where(a => a.Timestamp >= startDate.ToDateTime(TimeOnly.MinValue) &&
                       a.Timestamp <= endDate.ToDateTime(TimeOnly.MaxValue));

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(a => a.Severity == severity);
        }

        return await query.OrderByDescending(a => a.Timestamp).ToListAsync();
    }
}

