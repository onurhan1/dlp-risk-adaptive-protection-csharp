namespace DLP.RiskAnalyzer.Analyzer.Services;

public record WeeklyFlagIncidentDto(
    DateTime Timestamp,
    string? Policy,
    int MaxMatches,
    string? Destination,
    string? Channel);

public record WeeklyFlagUserDto(
    string UserEmail,
    string? FullName,
    string? Team,
    string ContactEmail,
    int TriggerCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    List<WeeklyFlagIncidentDto> SampleIncidents,
    string? Gender = null);

public class WeeklyFlagsResult
{
    public List<WeeklyFlagUserDto> PersonalEmailSenders { get; set; } = new();
    public List<WeeklyFlagUserDto> HighVolume { get; set; } = new();
    public List<WeeklyFlagUserDto> MassiveMatches { get; set; } = new();
}

public interface IWeeklyFlagService
{
    Task<WeeklyFlagsResult> GetWeeklyFlagsAsync(int days);
}
