using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Shared.Services;

/// <summary>
/// Risk skorlama ve analiz servisi
/// </summary>
public class RiskAnalyzer
{
    /// <summary>
    /// Risk skoru hesapla: 
    /// RiskScore = (Severity × 2.5) + (RepeatCount × 1.5) + (DataSensitivity × 2) + (MaxMatches × 4)
    /// Dashboard'da /10 olarak gösterilir
    /// </summary>
    public int CalculateRiskScore(int severity, int repeatCount, int dataSensitivity, int maxMatches = 0)
    {
        var baseScore = (severity * 2.5) + (repeatCount * 1.5) + (dataSensitivity * 2) + (maxMatches * 4);
        // Cap at 1000
        return (int)Math.Min(1000, baseScore);
    }
    
    /// <summary>
    /// Eski metot - geriye uyumluluk için
    /// </summary>
    [Obsolete("Use CalculateRiskScore with maxMatches parameter")]
    public int CalculateRiskScoreLegacy(int severity, int repeatCount, int dataSensitivity)
    {
        return CalculateRiskScore(severity, repeatCount, dataSensitivity, 0);
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


