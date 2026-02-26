using DLP.RiskAnalyzer.Shared.Models;
using DLP.RiskAnalyzer.Shared.Constants;

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
    /// Note: Ensure Shared project is rebuilt if signatures change.
    /// 
    /// Action Multipliers:
    /// - BLOCK/BLOCKED/QUARANTINE/QUARANTINED: 1.0 (100%)
    /// - AUTHORIZED/RELEASED/PERMIT/null: 0.2 (20%)
    /// 
    /// Dashboard'da /10 olarak gösterilir (0-100 ölçeği)
    /// </summary>
    /// <summary>
    /// Risk skoru hesapla (YENİ FORMÜL):
    /// BaseScore = (MaxMatchesTier * ChannelMultiplier) + DestinationScore
    /// FinalScore = BaseScore * ActionMultiplier
    /// 
    /// Destination Scores:
    /// - SPL / NDA Var: 1
    /// - Printer: 3
    /// - NDA Yok / Unknown / Default: 5
    /// - Personal: 10
    /// 
    /// Channel Multipliers:
    /// - ENDPOINT_LAN: 0.2
    /// - ENDPOINT_PRINTING: 0.4
    /// - Others (Email, Cloud etc): 1.0
    /// 
    /// Action Multipliers:
    /// - BLOCK/QUARANTINE: 1.0
    /// - AUTHORIZED/PERMIT: 0.2
    /// - RELEASED: 0.0 (Sıfırlanır)
    /// </summary>
    /// </summary>
    public int CalculateRiskScoreV2(int maxMatches, string? channel, int destinationScore, string? action)
    {
        // 1. MaxMatches Tier Score
        var maxMatchesScore = GetMaxMatchesTier(maxMatches);
        
        // 2. Channel Multiplier
        var channelMultiplier = GetChannelMultiplier(channel);

        // 3. Base Score Calculation
        // DOĞRU FORMÜL: (MaxMatchesTier * ChannelMultiplier) + DestinationScore
        var baseScore = (maxMatchesScore * channelMultiplier) + destinationScore;
        
        // 4. Action Multiplier
        var actionMultiplier = GetActionMultiplier(action);
        
        // Final Score
        var finalScore = baseScore * actionMultiplier;
        
        // Direct 0-100 scale (no multiplier needed)
        // Max possible: (85 * 1.0) + 15 = 100
        return (int)Math.Min(100, finalScore);
    }
    
    public double GetChannelMultiplier(string? channel)
    {
        if (string.IsNullOrEmpty(channel)) return RiskConstants.ChannelMultipliers.Default;
        
        var uChannel = channel.ToUpperInvariant();
        if (uChannel.Contains("ENDPOINT_LAN")) return RiskConstants.ChannelMultipliers.EndpointLan;
        if (uChannel.Contains("ENDPOINT_PRINTING") || uChannel.Contains("PRINTER")) return RiskConstants.ChannelMultipliers.EndpointPrinting;
        
        return RiskConstants.ChannelMultipliers.Default;
    }
    
    /// <summary>
    /// Tier-based MaxMatches puanlama
    /// MaxMatches sayısına göre tier puanı döner
    /// </summary>
    public int GetMaxMatchesTier(int maxMatches)
    {
        return maxMatches switch
        {
            <= 15 => 7,      // 0-15: Düşük
            <= 30 => 14,     // 16-30: Orta-Düşük
            <= 50 => 25,     // 31-50: Orta
            <= 100 => 40,    // 51-100: Yüksek
            <= 250 => 55,    // 101-250: Çok Yüksek
            <= 500 => 70,    // 251-500: Kritik
            _ => 85          // 500+: Maksimum
        };
    }
    
    /// <summary>
    /// Get action multiplier for risk calculation
    /// BLOCK/QUARANTINE = 100%, AUTHORIZED/PERMIT = 20%, RELEASED = 0%
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
            "AUTHORIZED" => 0.2,                    // 20%
            "RELEASED" => 0.0,                      // 0% (Sıfırla)
            "PERMIT" => 0.2,                        // 20%
            _ => 0.2                                // Default 20%
        };
    }
    
    /// <summary>
    /// Wrapper for backward compatibility if needed, but prefer the new one.
    /// </summary>
    [Obsolete("Use CalculateRiskScore(int maxMatches, string? channel, int destinationScore, string? action)")]
    public int CalculateRiskScore(int policyRepeatCount, int dataSensitivity, int maxMatches, string? action)
    {
        // Backward compatibility hack: Assume default destination score (Average=5) and default channel
        return CalculateRiskScoreV2(maxMatches, null, 5, action);
    }
    
    /// <summary>
    /// Eski metot - geriye uyumluluk için
    /// </summary>
    [Obsolete("Use CalculateRiskScore with action parameter")]
    public int CalculateRiskScoreLegacy(int severity, int repeatCount, int dataSensitivity, int maxMatches = 0)
    {
        return CalculateRiskScoreV2(maxMatches, null, 5, null);
    }

    /// <summary>
    /// Risk seviyesi belirle (0-100 ölçeği)
    /// - High: 50-100
    /// - Medium: 25-49
    /// - Low: 0-24
    /// </summary>
    public string GetRiskLevel(int riskScore)
    {
        if (riskScore >= 50)
            return "High";
        else if (riskScore >= 25)
            return "Medium";
        else
            return "Low";
    }
    
    /// <summary>
    /// Dashboard için skor (0-100 ölçeği, artık dönüşüm yok)
    /// </summary>
    public double GetDisplayScore(int riskScore)
    {
        // Score is already 0-100, no conversion needed
        return Math.Round((double)riskScore, 1, MidpointRounding.AwayFromZero);
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


