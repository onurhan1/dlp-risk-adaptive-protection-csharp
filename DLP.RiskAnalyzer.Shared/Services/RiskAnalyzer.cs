using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Shared.Services;

/// <summary>
/// Risk skorlama ve analiz servisi
/// </summary>
public class RiskAnalyzer
{
    /// <summary>
    /// Risk skoru hesapla (GÜNCEL FORMÜL):
    /// BaseScore = (PolicyRepeatCount × 3) + (DataSensitivity × 2) + MaxMatchesTier
    /// FinalScore = BaseScore × ActionMultiplier
    /// 
    /// Action Multipliers:
    /// - BLOCK/BLOCKED/QUARANTINE/QUARANTINED: 1.0 (100%)
    /// - AUTHORIZED/RELEASED/PERMIT/null: 0.2 (20%)
    /// 
    /// Dashboard'da /10 olarak gösterilir (0-100 ölçeği)
    /// </summary>
    public int CalculateRiskScore(int policyRepeatCount, int dataSensitivity, int maxMatches, string? action)
    {
        // Cap repeat count at 20 for formula (max contribution: 60)
        var cappedRepeatCount = Math.Min(policyRepeatCount, 20);
        
        // Base score with tier-based MaxMatches
        var repeatScore = cappedRepeatCount * 3.0;          // Max: 60
        var sensitivityScore = dataSensitivity * 2.0;       // Max: 20
        var maxMatchesScore = GetMaxMatchesTier(maxMatches); // Max: 60
        
        var baseScore = repeatScore + sensitivityScore + maxMatchesScore;
        
        // Apply action multiplier
        var actionMultiplier = GetActionMultiplier(action);
        var finalScore = baseScore * actionMultiplier;
        
        // Cap at 1000 (internal scale, displayed as 100)
        return (int)Math.Min(1000, finalScore * 10); // Scale to 1000
    }
    
    /// <summary>
    /// Tier-based MaxMatches puanlama
    /// MaxMatches sayısına göre tier puanı döner
    /// </summary>
    public int GetMaxMatchesTier(int maxMatches)
    {
        return maxMatches switch
        {
            <= 15 => 5,      // 0-15: Düşük
            <= 30 => 10,     // 16-30: Orta-Düşük
            <= 50 => 18,     // 31-50: Orta
            <= 100 => 28,    // 51-100: Yüksek
            <= 250 => 38,    // 101-250: Çok Yüksek
            <= 500 => 48,    // 251-500: Kritik
            _ => 60          // 500+: Maksimum
        };
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
    /// Eski metot - geriye uyumluluk için
    /// </summary>
    [Obsolete("Use CalculateRiskScore with action parameter")]
    public int CalculateRiskScoreLegacy(int severity, int repeatCount, int dataSensitivity, int maxMatches = 0)
    {
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


