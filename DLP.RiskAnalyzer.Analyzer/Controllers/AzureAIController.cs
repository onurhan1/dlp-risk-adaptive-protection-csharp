using Microsoft.AspNetCore.Mvc;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Controllers
{
    /// <summary>
    /// API controller for Azure AI Explanations data
    /// </summary>
    [ApiController]
    [Route("api/azure-ai")]
    public class AzureAIController : ControllerBase
    {
        private readonly AnalyzerDbContext _context;
        private readonly ILogger<AzureAIController> _logger;

        public AzureAIController(AnalyzerDbContext context, ILogger<AzureAIController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get Azure AI analysis summary for a specific user
        /// Returns average risk score and all analyzed incidents
        /// </summary>
        [HttpGet("by-user/{userEmail}")]
        public async Task<ActionResult<object>> GetByUser(string userEmail)
        {
            try
            {
                var decodedEmail = Uri.UnescapeDataString(userEmail);
                
                var explanations = await _context.AzureAIExplanations
                    .Where(e => e.UserEmail == decodedEmail)
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();

                if (!explanations.Any())
                {
                    return Ok(new {
                        userEmail = decodedEmail,
                        hasAnalysis = false,
                        averageRiskScore = 0,
                        totalAnalyzedIncidents = 0,
                        incidents = Array.Empty<object>()
                    });
                }

                var avgScore = explanations.Average(e => e.RiskScore);
                var anomalyCount = explanations.Count(e => e.AnomalyDetected);
                var highestRiskLevel = GetHighestRiskLevel(explanations);

                return Ok(new {
                    userEmail = decodedEmail,
                    hasAnalysis = true,
                    averageRiskScore = Math.Round(avgScore, 1),
                    totalAnalyzedIncidents = explanations.Count,
                    anomalyCount = anomalyCount,
                    highestRiskLevel = highestRiskLevel,
                    incidents = explanations.Select(e => new {
                        incidentId = e.IncidentId,
                        riskScore = e.RiskScore,
                        riskLevel = e.RiskLevel,
                        anomalyDetected = e.AnomalyDetected,
                        explanation = e.Explanation,
                        recommendedAction = e.RecommendedAction,
                        timestamp = e.Timestamp
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Azure AI data for user {UserEmail}", userEmail);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get Azure AI explanation for a specific incident
        /// </summary>
        [HttpGet("by-incident/{incidentId}")]
        public async Task<ActionResult<object>> GetByIncident(int incidentId)
        {
            try
            {
                var explanation = await _context.AzureAIExplanations
                    .FirstOrDefaultAsync(e => e.IncidentId == incidentId);

                if (explanation == null)
                {
                    return Ok(new {
                        incidentId = incidentId,
                        hasAnalysis = false
                    });
                }

                return Ok(new {
                    incidentId = explanation.IncidentId,
                    hasAnalysis = true,
                    userEmail = explanation.UserEmail,
                    riskScore = explanation.RiskScore,
                    riskLevel = explanation.RiskLevel,
                    anomalyDetected = explanation.AnomalyDetected,
                    explanation = explanation.Explanation,
                    recommendedAction = explanation.RecommendedAction,
                    timestamp = explanation.Timestamp
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Azure AI data for incident {IncidentId}", incidentId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get list of users who have Azure AI analysis
        /// </summary>
        [HttpGet("users-with-analysis")]
        public async Task<ActionResult<object>> GetUsersWithAnalysis()
        {
            try
            {
                var users = await _context.AzureAIExplanations
                    .GroupBy(e => e.UserEmail)
                    .Select(g => new {
                        userEmail = g.Key,
                        averageRiskScore = Math.Round(g.Average(e => e.RiskScore), 1),
                        totalAnalyzedIncidents = g.Count(),
                        anomalyCount = g.Count(e => e.AnomalyDetected),
                        latestAnalysis = g.Max(e => e.Timestamp)
                    })
                    .OrderByDescending(u => u.averageRiskScore)
                    .ToListAsync();

                return Ok(new {
                    totalUsers = users.Count,
                    users = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users with Azure AI analysis");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private string GetHighestRiskLevel(List<AzureAIExplanation> explanations)
        {
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                { "kritik", 4 },
                { "yüksek", 3 },
                { "orta", 2 },
                { "düşük", 1 }
            };

            string highest = "düşük";
            int highestValue = 0;

            foreach (var e in explanations)
            {
                if (levels.TryGetValue(e.RiskLevel, out int value) && value > highestValue)
                {
                    highestValue = value;
                    highest = e.RiskLevel;
                }
            }

            return highest;
        }
    }
}
