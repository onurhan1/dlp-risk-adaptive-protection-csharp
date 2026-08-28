using System.Text.Json;

namespace DLP.RiskAnalyzer.Analyzer.Helpers;

/// <summary>
/// Centralized parser for ViolationTriggers JSON field.
/// Previously this same parsing logic was duplicated in RiskController,
/// DatabaseService, and RiskAnalyzerService. Single source of truth.
/// </summary>
public static class ViolationTriggerParser
{
    /// <summary>
    /// Extracts the first rule name found in a ViolationTriggers JSON string.
    /// Handles multiple casing variants: RuleName, rule_name, ruleName.
    /// Returns null if not found or parsing fails.
    /// </summary>
    public static string? ExtractFirstRuleName(string? violationTriggersJson)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return null;

            foreach (var trigger in root.EnumerateArray())
            {
                var name = GetStringProperty(trigger, "RuleName", "rule_name", "ruleName");
                if (!string.IsNullOrEmpty(name)) return name;
            }
        }
        catch (JsonException)
        {
            // Intentionally empty — malformed JSON returns null; caller falls back to policy name
        }

        return null;
    }

    /// <summary>
    /// Returns all distinct rule names found across all triggers in the JSON array.
    /// </summary>
    public static IReadOnlyList<string> ExtractAllRuleNames(string? violationTriggersJson)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(violationTriggersJson)) return results;

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return results;

            foreach (var trigger in root.EnumerateArray())
            {
                var name = GetStringProperty(trigger, "RuleName", "rule_name", "ruleName");
                if (!string.IsNullOrEmpty(name) && !results.Contains(name))
                    results.Add(name);
            }
        }
        catch (JsonException) { /* Intentionally empty — malformed JSON returns safe default */ }

        return results;
    }

    /// <summary>
    /// Calculates the maximum NumberMatches value across all classifiers in all triggers.
    /// Returns 0 if not found or JSON is invalid.
    /// </summary>
    public static int ExtractMaxMatches(string? violationTriggersJson)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return 0;

        int maxMatches = 0;

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return 0;

            foreach (var trigger in root.EnumerateArray())
            {
                if (!trigger.TryGetProperty("Classifiers", out var classifiers) &&
                    !trigger.TryGetProperty("classifiers", out classifiers))
                    continue;

                if (classifiers.ValueKind != JsonValueKind.Array) continue;

                foreach (var classifier in classifiers.EnumerateArray())
                {
                    JsonElement matchesElement;
                    if (!classifier.TryGetProperty("NumberMatches", out matchesElement) &&
                        !classifier.TryGetProperty("number_matches", out matchesElement) &&
                        !classifier.TryGetProperty("numberMatches", out matchesElement))
                        continue;

                    if (TryReadMatchCount(matchesElement, out var matches))
                    {
                        if (matches > maxMatches) maxMatches = matches;
                    }
                }
            }
        }
        catch (JsonException) { /* Intentionally empty — malformed JSON returns safe default */ }

        return maxMatches;
    }

    /// <summary>
    /// Parses a ViolationTriggers JSON string and returns a summary with
    /// first rule name and max matches in one pass — avoids double parsing.
    /// </summary>
    public static (string? RuleName, int MaxMatches) ExtractSummary(string? violationTriggersJson)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return (null, 0);

        string? firstRuleName = null;
        int maxMatches = 0;

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return (null, 0);

            foreach (var trigger in root.EnumerateArray())
            {
                // Extract rule name from first trigger that has one
                if (firstRuleName == null)
                {
                    firstRuleName = GetStringProperty(trigger, "RuleName", "rule_name", "ruleName");
                }

                // Extract max matches across all classifiers
                if (trigger.TryGetProperty("Classifiers", out var classifiers) ||
                    trigger.TryGetProperty("classifiers", out classifiers))
                {
                    if (classifiers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var classifier in classifiers.EnumerateArray())
                        {
                            JsonElement matchesElement;
                            if (classifier.TryGetProperty("NumberMatches", out matchesElement) ||
                                classifier.TryGetProperty("number_matches", out matchesElement) ||
                                classifier.TryGetProperty("numberMatches", out matchesElement))
                            {
                                if (TryReadMatchCount(matchesElement, out var matches))
                                {
                                    if (matches > maxMatches) maxMatches = matches;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException) { /* Intentionally empty — malformed JSON returns safe default */ }

        return (firstRuleName, maxMatches);
    }

    /// <summary>
    /// Parses a ViolationTriggers JSON string and returns the policy name and rule name
    /// associated with the trigger that contains the highest number of classifier matches.
    /// </summary>
    public static (string? PolicyName, string? RuleName) ExtractMaxMatchPolicyAndRule(string? violationTriggersJson)
    {
        if (string.IsNullOrEmpty(violationTriggersJson)) return (null, null);

        string? maxPolicyName = null;
        string? maxRuleName = null;
        int maxMatches = -1; // Use -1 to ensure even 0 matches gets picked if it's the only one

        try
        {
            using var doc = JsonDocument.Parse(violationTriggersJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array) return (null, null);

            foreach (var trigger in root.EnumerateArray())
            {
                int currentTriggerMax = 0;
                bool hasClassifiers = false;

                if (trigger.TryGetProperty("Classifiers", out var classifiers) ||
                    trigger.TryGetProperty("classifiers", out classifiers))
                {
                    if (classifiers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var classifier in classifiers.EnumerateArray())
                        {
                            JsonElement matchesElement;
                            if (classifier.TryGetProperty("NumberMatches", out matchesElement) ||
                                classifier.TryGetProperty("number_matches", out matchesElement) ||
                                classifier.TryGetProperty("numberMatches", out matchesElement))
                            {
                                if (TryReadMatchCount(matchesElement, out var matches))
                                {
                                    hasClassifiers = true;
                                    if (matches > currentTriggerMax) currentTriggerMax = matches;
                                }
                            }
                        }
                    }
                }

                if (hasClassifiers && currentTriggerMax > maxMatches)
                {
                    maxMatches = currentTriggerMax;
                    maxPolicyName = GetStringProperty(trigger, "PolicyName", "policy_name", "policyName");
                    maxRuleName = GetStringProperty(trigger, "RuleName", "rule_name", "ruleName");
                }
                else if (maxMatches == -1) // Fallback to first trigger if no classifiers have matches
                {
                    maxMatches = 0;
                    maxPolicyName = GetStringProperty(trigger, "PolicyName", "policy_name", "policyName");
                    maxRuleName = GetStringProperty(trigger, "RuleName", "rule_name", "ruleName");
                }
            }
        }
        catch (JsonException) { /* Intentionally empty — malformed JSON returns safe default */ }

        return (maxPolicyName, maxRuleName);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private static string? GetStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrEmpty(value)) return value;
            }
        }
        return null;
    }

    /// <summary>
    /// Reads NumberMatches defensively. Imports in the wild contain both JSON
    /// numbers and numeric strings, and a value outside Int32 must never make
    /// an investigation or report request fail.
    /// </summary>
    private static bool TryReadMatchCount(JsonElement element, out int value)
    {
        value = 0;

        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out value))
                return value >= 0;

            if (element.TryGetInt64(out var longValue) && longValue > 0)
            {
                value = longValue > int.MaxValue ? int.MaxValue : (int)longValue;
                return true;
            }

            return false;
        }

        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), out value))
        {
            return value >= 0;
        }

        return false;
    }
}
