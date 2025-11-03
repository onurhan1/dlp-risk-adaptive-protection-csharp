using DLP.RiskAnalyzer.Shared.Models;

namespace DLP.RiskAnalyzer.Shared.Services;

/// <summary>
/// Risk skorlama ve analiz servisi
/// </summary>
public class RiskAnalyzer
{
    /// <summary>
    /// Risk skoru hesapla: risk = (severity*3) + (repeat_count*2) + (data_sensitivity*5)
    /// </summary>
    public int CalculateRiskScore(int severity, int repeatCount, int dataSensitivity)
    {
        var baseScore = (severity * 3) + (repeatCount * 2) + (dataSensitivity * 5);
        // Cap at 100 (Forcepoint RAP scale)
        return Math.Min(100, baseScore);
    }

    /// <summary>
    /// Risk seviyesi belirle
    /// - Critical: 91-100
    /// - High: 61-80
    /// - Medium: 41-60
    /// - Low: 0-40
    /// </summary>
    public string GetRiskLevel(int riskScore)
    {
        if (riskScore >= 91)
            return "Critical";
        else if (riskScore >= 61)
            return "High";
        else if (riskScore >= 41)
            return "Medium";
        else
            return "Low";
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


