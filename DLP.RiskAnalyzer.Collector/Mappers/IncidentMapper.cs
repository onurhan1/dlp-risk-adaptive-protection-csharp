using DLP.RiskAnalyzer.Collector.Services;
using DLP.RiskAnalyzer.Shared.Models;
using System.Linq;

namespace DLP.RiskAnalyzer.Collector.Mappers;

public static class IncidentMapper
{
    public static Incident MapFromDLPIncident(DLPIncident dlpIncident)
    {
        var maxMatches = 0;
        if (dlpIncident.ViolationTriggers != null && dlpIncident.ViolationTriggers.Count > 0)
        {
            var classifiersWithMatches = dlpIncident.ViolationTriggers
                .Where(t => t.Classifiers != null && t.Classifiers.Count > 0)
                .SelectMany(t => t.Classifiers!)
                .Where(c => c.NumberMatches > 0)
                .ToList();

            if (classifiersWithMatches.Count > 0)
            {
                maxMatches = classifiersWithMatches.Max(c => c.NumberMatches);
            }
        }

        string? userIdentifier = dlpIncident.User;
        if (string.IsNullOrEmpty(userIdentifier))
            userIdentifier = dlpIncident.Source?.LoginName;
        if (string.IsNullOrEmpty(userIdentifier))
            userIdentifier = dlpIncident.Source?.EmailAddress;
        if (string.IsNullOrEmpty(userIdentifier))
            userIdentifier = dlpIncident.Source?.HostName;
        if (string.IsNullOrEmpty(userIdentifier))
            userIdentifier = "unknown";

        return new Incident
        {
            Id = dlpIncident.Id,
            UserEmail = userIdentifier,
            Department = dlpIncident.Department,
            Severity = dlpIncident.Severity,
            DataType = dlpIncident.DataType,
            Timestamp = dlpIncident.Timestamp,
            Policy = dlpIncident.Policy,
            Channel = dlpIncident.Channel,
            MaxMatches = maxMatches,
            Action = dlpIncident.Action,
            Destination = dlpIncident.Destination,
            FileName = dlpIncident.FileName,
            LoginName = dlpIncident.Source?.LoginName ?? dlpIncident.LoginName,
            EmailAddress = dlpIncident.Source?.EmailAddress ?? dlpIncident.EmailAddress,
            HostName = dlpIncident.Source?.HostName ?? dlpIncident.HostName,
            FullName = !string.IsNullOrEmpty(dlpIncident.Source?.Manager)
                ? dlpIncident.Source.Manager.Split('/')[0].Trim()
                : null,
            Team = !string.IsNullOrEmpty(dlpIncident.Source?.Manager) && dlpIncident.Source.Manager.Contains('/')
                ? (dlpIncident.Source.Manager.Split('/')[1].Contains('-')
                    ? dlpIncident.Source.Manager.Split('/')[1].Split(new[] { '-' }, 2)[1].Trim()
                    : dlpIncident.Source.Manager.Split('/')[1].Trim())
                : null,
            RuleName = dlpIncident.ViolationTriggers != null
                ? string.Join("; ", dlpIncident.ViolationTriggers
                    .Select(vt => vt.RuleName)
                    .Where(rn => !string.IsNullOrEmpty(rn))
                    .Distinct())
                : null,
            ViolationTriggers = dlpIncident.ViolationTriggers != null
                ? System.Text.Json.JsonSerializer.Serialize(
                    dlpIncident.ViolationTriggers.Select(vt => new
                    {
                        policy_name = vt.PolicyName,
                        rule_name = vt.RuleName,
                        classifiers = vt.Classifiers?.Select(c => new
                        {
                            classifier_name = c.ClassifierName,
                            number_matches = c.NumberMatches
                        }).ToList()
                    }).ToList(),
                    new System.Text.Json.JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull })
                : null
        };
    }
}
