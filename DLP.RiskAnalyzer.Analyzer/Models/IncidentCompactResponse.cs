namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Takim Bazli Analiz sayfasinin gercekten kullandigi alan seti.
/// Tam <c>IncidentResponse</c> 31 alan tasiyor; bu sayfa listenin tamamini cektigi icin
/// kullanilmayan alanlar payload'i gereksiz buyutuyor ve tarayiciyi yavaslatiyordu.
/// Alan adlari global SnakeCaseLower politikasiyla snake_case'e cevrilir.
/// </summary>
public sealed class IncidentCompactResponse
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string? Policy { get; set; }
    public string? Action { get; set; }

    /// <summary>Istemci alan adini domain'e cevirmek icin kullanir.</summary>
    public string? Destination { get; set; }

    public string? Department { get; set; }
    public string? Team { get; set; }
    public string? FullName { get; set; }
    public int MaxMatches { get; set; }
    public string? LoginName { get; set; }
    public string? EmailAddress { get; set; }
    public string? ViolationTriggers { get; set; }
    public string? Channel { get; set; }
}
