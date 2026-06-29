using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Repositories.Interfaces;
using DLP.RiskAnalyzer.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Repositories.Implementations;

/// <summary>
/// Repository implementation for AI Behavioral Analysis data access
/// </summary>
public class AIAnalysisRepository : IAIAnalysisRepository
{
    private readonly AnalyzerDbContext _context;

    public AIAnalysisRepository(AnalyzerDbContext context)
    {
        _context = context;
    }

    public async Task<AIBehavioralAnalysis?> GetAnalysisAsync(string entityType, string entityId, DateTime analysisDate)
    {
        return await _context.AIBehavioralAnalyses
            .FirstOrDefaultAsync(a => 
                a.EntityType == entityType &&
                a.EntityId == entityId &&
                a.AnalysisDate.Date == analysisDate.Date);
    }

    public async Task SaveAnalysisAsync(AIBehavioralAnalysis analysis)
    {
        var existing = await GetAnalysisAsync(analysis.EntityType, analysis.EntityId, analysis.AnalysisDate);

        if (existing != null)
        {
            existing.RiskScore = analysis.RiskScore;
            existing.AnomalyLevel = analysis.AnomalyLevel;
            existing.AIExplanation = analysis.AIExplanation;
            existing.AIRecommendation = analysis.AIRecommendation;
            existing.ReferenceIncidentIds = analysis.ReferenceIncidentIds;
            existing.AnalysisMetadata = analysis.AnalysisMetadata;
            existing.CreatedAt = DateTime.UtcNow;
            
            _context.AIBehavioralAnalyses.Update(existing);
        }
        else
        {
            analysis.CreatedAt = DateTime.UtcNow;
            _context.AIBehavioralAnalyses.Add(analysis);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetAISettingsAsync()
    {
        return await _context.SystemSettings
            .Where(s => s.Key.StartsWith("ai_"))
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}
