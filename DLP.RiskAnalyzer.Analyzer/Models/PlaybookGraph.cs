using System.Text.Json;
using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// The node graph of a playbook, stored as JSON in <see cref="Playbook.GraphJson"/>.
/// Serialised with <see cref="PlaybookJson.Options"/> (snake_case) so it round-trips with the
/// request bodies produced by the dashboard, which uses the API-wide snake_case convention.
/// </summary>
public class PlaybookGraph
{
    public List<PlaybookNode> Nodes { get; set; } = new();
    public List<PlaybookEdge> Edges { get; set; } = new();
}

public class PlaybookNode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>See <see cref="PlaybookNodeType"/>.</summary>
    public string Type { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Canvas position in graph coordinates.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>
    /// Free-form per-node settings. Dictionary keys are not touched by the naming policy,
    /// so they are snake_case by convention on both sides — read them through the
    /// <see cref="PlaybookConfigExtensions"/> helpers.
    /// </summary>
    public Dictionary<string, JsonElement> Config { get; set; } = new();
}

public class PlaybookEdge
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Which output port of the source node this edge leaves from. Only meaningful for
    /// <see cref="PlaybookNodeType.LogicCondition"/>, where it is "true" or "false".
    /// </summary>
    public string? SourceHandle { get; set; }
}

public static class PlaybookNodeType
{
    public const string TriggerSchedule = "trigger.schedule";
    public const string TriggerManual = "trigger.manual";
    public const string SourceWeeklyFlags = "source.weeklyFlags";
    public const string SourceIncidentMetric = "source.incidentMetric";
    public const string SourceHighRiskUsers = "source.highRiskUsers";
    public const string SourceTopActionUsers = "source.topActionUsers";
    public const string SourceHighMaxMatchTransfers = "source.highMaxMatchTransfers";
    public const string TransformFilter = "transform.filter";
    public const string LogicCondition = "logic.condition";
    public const string LogicMetricThreshold = "logic.metricThreshold";
    public const string ActionSendMail = "action.sendMail";
    public const string ActionSendReportMail = "action.sendReportMail";
    public const string OutputReport = "output.report";

    public static readonly string[] All =
    {
        TriggerSchedule, TriggerManual, SourceWeeklyFlags, SourceIncidentMetric,
        SourceHighRiskUsers, SourceTopActionUsers, SourceHighMaxMatchTransfers,
        TransformFilter, LogicCondition, LogicMetricThreshold,
        ActionSendMail, ActionSendReportMail, OutputReport
    };

    public static bool IsTrigger(string type) =>
        type == TriggerSchedule || type == TriggerManual;

    /// <summary>Number of input ports; triggers accept none.</summary>
    public static int InputCount(string type) => IsTrigger(type) ? 0 : 1;

    /// <summary>
    /// Number of output ports. The report node terminates a branch; the two branching nodes
    /// fork into a "true"/"false" pair.
    /// </summary>
    public static int OutputCount(string type) => type switch
    {
        OutputReport => 0,
        LogicCondition or LogicMetricThreshold => 2,
        _ => 1
    };
}

/// <summary>Weekly-flag criteria a source node can subscribe to (mirrors <c>WeeklyFlagsResult</c>).</summary>
public static class WeeklyFlagCriterion
{
    public const string PersonalEmailSenders = "personal_email_senders";
    public const string HighVolume = "high_volume";
    public const string MassiveMatches = "massive_matches";

    public static readonly string[] All = { PersonalEmailSenders, HighVolume, MassiveMatches };

    public static string Label(string criterion) => criterion switch
    {
        PersonalEmailSenders => "Şahsi Maile Gönderim Yapanlar",
        HighVolume => "30 Dakikada 10+ Olay Üretenler",
        MassiveMatches => "Ard Arda 500+ Eşleşmeli Olay Üretenler",
        _ => criterion
    };
}

/// <summary>Shared serialiser settings for everything stored inside a playbook.</summary>
public static class PlaybookJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Options);
}

/// <summary>Tolerant readers for <see cref="PlaybookNode.Config"/> values.</summary>
public static class PlaybookConfigExtensions
{
    public static string? GetString(this PlaybookNode node, string key)
    {
        if (!node.Config.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static int? GetInt(this PlaybookNode node, string key)
    {
        if (!node.Config.TryGetValue(key, out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
        return null;
    }

    public static bool GetBool(this PlaybookNode node, string key, bool fallback = false)
    {
        if (!node.Config.TryGetValue(key, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) ? b : fallback,
            _ => fallback
        };
    }

    /// <summary>
    /// Reads a string list. Accepts a JSON array or a comma/newline separated string, so
    /// free-text inputs in the UI ("a@x.com, b@y.com") work without extra client parsing.
    /// </summary>
    public static List<string> GetStringList(this PlaybookNode node, string key)
    {
        var result = new List<string>();
        if (!node.Config.TryGetValue(key, out var el)) return result;

        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                if (!string.IsNullOrWhiteSpace(value)) result.Add(value.Trim());
            }
            return result;
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            var raw = el.GetString() ?? string.Empty;
            result.AddRange(raw
                .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0));
        }

        return result;
    }
}

/// <summary>Outcome of <c>IPlaybookEngine.Validate</c>: blocking errors plus advisory warnings.</summary>
public class PlaybookValidationResult
{
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsValid => Errors.Count == 0;
}
