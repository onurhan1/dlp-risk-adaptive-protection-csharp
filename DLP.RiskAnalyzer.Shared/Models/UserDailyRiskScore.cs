namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// User Daily Risk Score model - Günlük kullanıcı risk skorları
/// Trend analizi ve raporlama için kullanılır
/// </summary>
public class UserDailyRiskScore
{
    public int Id { get; set; }
    
    /// <summary>
    /// Kullanıcı email adresi
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Skor tarihi
    /// </summary>
    public DateOnly Date { get; set; }
    
    /// <summary>
    /// O günkü toplam risk skoru
    /// </summary>
    public double DailyRiskScore { get; set; } = 0;
    
    /// <summary>
    /// O günkü incident sayısı
    /// </summary>
    public int IncidentCount { get; set; } = 0;
    
    /// <summary>
    /// O günkü maksimum risk skoru
    /// </summary>
    public int MaxRiskScore { get; set; } = 0;
    
    /// <summary>
    /// O günkü ortalama risk skoru
    /// </summary>
    public double AvgRiskScore { get; set; } = 0;
    
    /// <summary>
    /// Kullanıcı takımı/departmanı
    /// </summary>
    public string? Team { get; set; }

    /// <summary>
    /// Kullanıcı tam adı
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// O günkü BLOCK action sayısı
    /// </summary>
    public int BlockCount { get; set; } = 0;

    /// <summary>
    /// O günkü PERMIT action sayısı
    /// </summary>
    public int PermitCount { get; set; } = 0;

    /// <summary>
    /// O günkü QUARANTINE action sayısı
    /// </summary>
    public int QuarantineCount { get; set; } = 0;

    /// <summary>
    /// O günkü RELEASED action sayısı
    /// </summary>
    public int ReleasedCount { get; set; } = 0;

    /// <summary>
    /// O günkü en yüksek max_matches değeri
    /// </summary>
    public int MaxMaxMatches { get; set; } = 0;

    /// <summary>
    /// O günkü ortalama max_matches değeri
    /// </summary>
    public double AvgMaxMatches { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
