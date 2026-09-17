namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface ILocalLlmLabService
{
    Task<LocalLlmLabSettings> GetSettingsAsync(CancellationToken ct);
    Task SaveSettingsAsync(LocalLlmLabSettings settings, CancellationToken ct);
    Task<string> TestConnectionAsync(LocalLlmLabSettings settings, CancellationToken ct);
    Task<LocalLlmIncidentSnapshot> GetIncidentSnapshotAsync(LocalLlmSnapshotRequest request, CancellationToken ct);
    Task<LocalLlmChatResult> ChatAsync(LocalLlmChatRequest request, CancellationToken ct);
}

public sealed record LocalLlmLabSettings(
    bool Enabled,
    string GenerateUrl,
    string Model,
    double Temperature,
    int MaxTokens);

public sealed record LocalLlmSnapshotRequest(int LookbackDays = 30, int SampleSize = 40, bool MaskIdentifiers = true);

public sealed record LocalLlmChatMessage(string Role, string Content);

public sealed record LocalLlmChatRequest(
    string Message,
    List<LocalLlmChatMessage>? History,
    int LookbackDays = 30,
    int SampleSize = 40,
    bool MaskIdentifiers = true);

public sealed record LocalLlmChatResult(string Reply, LocalLlmIncidentSnapshot Snapshot);

public sealed record LocalLlmIncidentSnapshot(
    DateTime StartUtc,
    DateTime EndUtc,
    int TotalIncidents,
    int UniqueUsers,
    int MaximumMatches,
    IReadOnlyList<LocalLlmCount> Actions,
    IReadOnlyList<LocalLlmCount> Channels,
    IReadOnlyList<LocalLlmCount> Policies,
    IReadOnlyList<LocalLlmUserProfile> Users,
    IReadOnlyList<LocalLlmIncidentSample> Samples);

public sealed record LocalLlmCount(string Name, int Count);

public sealed record LocalLlmUserProfile(
    string User,
    string? Department,
    int IncidentCount,
    int MaximumSeverity,
    int MaximumMatches,
    double AverageDataSensitivity,
    int RepeatCount);

public sealed record LocalLlmIncidentSample(
    DateTime Timestamp,
    string User,
    string? Department,
    string? Action,
    int Severity,
    int MaxMatches,
    int DataSensitivity,
    int RepeatCount,
    string? Policy,
    string? Rule,
    string? Channel,
    string? Destination);
