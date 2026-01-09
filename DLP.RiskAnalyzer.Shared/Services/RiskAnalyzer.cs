using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Shared.Services;

/// <summary>
/// Risk skorlama ve analiz servisi
/// </summary>
public class RiskAnalyzer
{
    /// <summary>
    /// Risk skoru hesapla (YENİ FORMÜL - Severity kaldırıldı):
    /// BaseScore = (WeeklyRepeatCount × 3) + (DataSensitivity × 3) + (MaxMatches × 4)
    /// FinalScore = BaseScore × ActionMultiplier
    /// 
    /// Action Multipliers:
    /// - BLOCK/BLOCKED/QUARANTINE/QUARANTINED: 1.0 (100%)
    /// - AUTHORIZED/RELEASED/PERMIT/null: 0.2 (20%)
    /// 
    /// Dashboard'da /10 olarak gösterilir (0-100 ölçeği)
    /// </summary>
    public int CalculateRiskScore(int weeklyRepeatCount, int dataSensitivity, int maxMatches, string? action)
    {
        // Base score without Severity
        var baseScore = (weeklyRepeatCount * 3.0) + (dataSensitivity * 3.0) + (maxMatches * 4.0);
        
        // Apply action multiplier
        var actionMultiplier = GetActionMultiplier(action);
        var finalScore = baseScore * actionMultiplier;
        
        // Cap at 1000
        return (int)Math.Min(1000, finalScore);
    }
    
    /// <summary>
    /// Get action multiplier for risk calculation
    /// BLOCK/QUARANTINE = 100%, AUTHORIZED/RELEASED/PERMIT = 20%
    /// </summary>
    public double GetActionMultiplier(string? action)
    {
        if (string.IsNullOrEmpty(action))
            return 0.2; // Default to 20% for unknown/null actions
            
        var normalizedAction = action.ToUpperInvariant();
        
        return normalizedAction switch
        {
            "BLOCK" or "BLOCKED" => 1.0,           // 100%
            "QUARANTINE" or "QUARANTINED" => 1.0,  // 100%
            "AUTHORIZED" => 0.2,                    // 20% (false positive azaltma)
            "RELEASED" => 0.2,                      // 20% (karantinadan çıkarılan)
            "PERMIT" => 0.2,                        // 20%
            _ => 0.2                                // Default 20%
        };
    }
    
    /// <summary>
    /// Eski metot - geriye uyumluluk için (severity parametresi artık kullanılmıyor)
    /// </summary>
    [Obsolete("Use CalculateRiskScore with action parameter")]
    public int CalculateRiskScoreLegacy(int severity, int repeatCount, int dataSensitivity, int maxMatches = 0)
    {
        // Eski formül - geriye uyumluluk için, action null olarak hesapla
        return CalculateRiskScore(repeatCount, dataSensitivity, maxMatches, null);
    }

    /// <summary>
    /// Risk seviyesi belirle (1000 üzerinden)
    /// - High: 500-1000 (Dashboard: 50-100)
    /// - Medium: 250-499 (Dashboard: 25-49.9)
    /// - Low: 0-249 (Dashboard: 0-24.9)
    /// </summary>
    public string GetRiskLevel(int riskScore)
    {
        if (riskScore >= 500)
            return "High";
        else if (riskScore >= 250)
            return "Medium";
        else
            return "Low";
    }
    
    /// <summary>
    /// Dashboard için skor dönüşümü (1000 → 100 ölçeği)
    /// </summary>
    public double GetDisplayScore(int riskScore)
    {
        var displayScore = riskScore / 10.0;
        // 0.5 üstü yukarı yuvarlama
        return Math.Round(displayScore, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Policy action önerisi
    /// </summary>
    public string GetPolicyAction(string riskLevel, string channel)
    {
        var actions = new Dictionary<string, Dictionary<string, string>>
        {
            {
                "Critical", new Dictionary<string, string>
                {
                    { "Email", "Block" },
                    { "USB", "Block" },
                    { "Cloud", "Block" },
                    { "Web", "Block" },
                    { "Print", "Block" }
                }
            },
            {
                "High", new Dictionary<string, string>
                {
                    { "Email", "Encrypt" },
                    { "USB", "Encrypt" },
                    { "Cloud", "Encrypt" },
                    { "Web", "Encrypt" },
                    { "Print", "Notify" }
                }
            },
            {
                "Medium", new Dictionary<string, string>
                {
                    { "Email", "Confirm Prompt" },
                    { "USB", "Confirm Prompt" },
                    { "Cloud", "Confirm Prompt" },
                    { "Web", "Confirm Prompt" },
                    { "Print", "Audit" }
                }
            },
            {
                "Low", new Dictionary<string, string>
                {
                    { "Email", "Audit" },
                    { "USB", "Audit" },
                    { "Cloud", "Audit" },
                    { "Web", "Audit" },
                    { "Print", "Audit" }
                }
            }
        };

        if (actions.TryGetValue(riskLevel, out var channelActions))
        {
            if (channelActions.TryGetValue(channel, out var action))
                return action;
        }

        return "Audit"; // Default
    }

    /// <summary>
    /// IOB (Indicator of Behavior) tespiti
    /// </summary>
    public List<string> DetectIOB(Incident incident)
    {
        var iobs = new List<string>();

        // Data Exfiltration patterns
        if (incident.Channel == "Email" && incident.UserEmail.Contains("@") && 
            !incident.UserEmail.Contains("@company.com"))
        {
            iobs.Add("IOB-511"); // Email to personal domain
        }

        if (incident.Channel == "USB" && incident.Severity >= 7)
        {
            iobs.Add("IOB-299"); // USB upload
        }

        if (incident.Channel == "Cloud" && incident.DataSensitivity >= 8)
        {
            iobs.Add("IOB-811"); // Cloud upload
        }

        // Stockpiling patterns
        if (incident.RepeatCount >= 10)
        {
            iobs.Add("IOB-311"); // Anomalous file copying
        }

        // Defense Evasion patterns
        if (incident.Policy?.Contains("Agent") == true && incident.Severity >= 8)
        {
            iobs.Add("IOB-280"); // Agent tampering
        }

        return iobs;
    }
}


