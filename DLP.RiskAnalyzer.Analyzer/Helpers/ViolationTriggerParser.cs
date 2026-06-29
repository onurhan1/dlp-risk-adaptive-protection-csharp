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

                    if (matchesElement.ValueKind == JsonValueKind.Number)
                    {
                        var matches = matchesElement.GetInt32();
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
                                if (matchesElement.ValueKind == JsonValueKind.Number)
                                {
                                    var matches = matchesElement.GetInt32();
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
}
