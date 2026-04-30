namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// İstisna Kaldırma Listesi - DLP istisna kaldırma kayıtları
/// Excel formatı: VGY_2026_İstisna_Kaldırma_Listesi.xlsx
/// </summary>
public class ExceptionRemovalEntry
{
    public int Id { get; set; }
    
    /// <summary>İstisna Verilen Ekip</summary>
    public string? Team { get; set; }
    
    /// <summary>İstisna Verilen Kural</summary>
    public string? Rule { get; set; }
    
    /// <summary>İstisna Adı</summary>
    public string ExceptionName { get; set; } = string.Empty;
    
    /// <summary>İstisna Durumu (Aktif/Pasif)</summary>
    public string? Status { get; set; }
    
    /// <summary>İstisna Kullanım Sayısı (Son 1 Yıl)</summary>
    public int UsageCount { get; set; }
    
    /// <summary>İstisna Kaldırılma Nedeni</summary>
    public string? RemovalReason { get; set; }
    
    /// <summary>Yapılan İşlem Tarihi</summary>
    public DateTime? ActionDate { get; set; }
    
    /// <summary>Change No (değişiklik numarası)</summary>
    public string? ChangeNo { get; set; }
    
    /// <summary>Kaydı oluşturan kullanıcı</summary>
    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
