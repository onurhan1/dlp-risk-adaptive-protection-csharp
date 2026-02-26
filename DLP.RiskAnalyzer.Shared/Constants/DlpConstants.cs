namespace DLP.RiskAnalyzer.Shared.Constants;

public static class DlpConstants
{
    /// <summary>
    /// Redis pub/sub channel used to broadcast Forcepoint DLP configuration updates.
    /// </summary>
    public const string DlpConfigChannel = "dlp:config:updated";
}

