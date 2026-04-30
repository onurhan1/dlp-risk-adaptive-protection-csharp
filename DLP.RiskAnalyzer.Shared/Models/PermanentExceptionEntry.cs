namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Kalıcı İstisna Listesi - DLP kalıcı istisna tanımlama kayıtları
/// Excel formatı: VGY_2026_Kalıcı_İstisna_Listesi.xlsx
/// </summary>
public class PermanentExceptionEntry
{
    public int Id { get; set; }
    
    /// <summary>İstisna Adı</summary>
    public string ExceptionName { get; set; } = string.Empty;
    
    /// <summary>İstisna Verilen Domain</summary>
    public string? ExceptionDomain { get; set; }
    
    /// <summary>İstisna Verilen Ekip</summary>
    public string? Team { get; set; }
    
    /// <summary>İstisna Verilen Politikalar (virgülle ayrılmış)</summary>
    public string? Policies { get; set; }
    
    /// <summary>İstisna Verilen Kurallar (virgülle ayrılmış)</summary>
    public string? Rules { get; set; }
    
    /// <summary>Channel (Mail, Endpoint Printing, Web vb.)</summary>
    public string? Channel { get; set; }
    
    /// <summary>Verilen Süre (1 yıl, 6 ay vb.)</summary>
    public string? Duration { get; set; }
    
    /// <summary>İşlem Tarihi</summary>
    public DateTime? ActionDate { get; set; }
    
    /// <summary>Change No (değişiklik numarası)</summary>
    public string? ChangeNo { get; set; }
    
    /// <summary>Kaydı oluşturan kullanıcı</summary>
    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
