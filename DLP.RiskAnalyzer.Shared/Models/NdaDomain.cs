namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// NDA Domain model - Domain özellikleri yönetimi
/// Destination risk skoru hesaplaması ve domain sınıflandırması için kullanılır
/// </summary>
public class NdaDomain
{
    public int Id { get; set; }
    
    /// <summary>
    /// Domain adı (lowercase olarak saklanır)
    /// </summary>
    public string Domain { get; set; } = string.Empty;
    
    /// <summary>
    /// Gizlilik sözleşmesi var mı?
    /// true: NDA VAR (skor 1)
    /// false: NDA YOK (skor 5)
    /// </summary>
    public bool HasNda { get; set; } = false;
    
    /// <summary>
    /// Yeni keşfedilmiş ve henüz sınıflandırılmamış domain
    /// Admin tarafından güncellenmesi gerekir
    /// </summary>
    public bool IsUnknown { get; set; } = false;
    
    /// <summary>
    /// Kişisel domain mi? (gmail, hotmail, outlook vb.)
    /// true ise skor 10 uygulanır
    /// </summary>
    public bool IsPersonal { get; set; } = false;
    
    // ===== NEW FEATURE COLUMNS =====
    
    /// <summary>İştirak domain mi?</summary>
    public bool IstirakDomain { get; set; } = false;
    
    /// <summary>Eğitim kurumu mu?</summary>
    public bool Egitim { get; set; } = false;
    
    /// <summary>Noter mi?</summary>
    public bool Noter { get; set; } = false;
    
    /// <summary>Hukuk bürosu/ofisi mi?</summary>
    public bool Hukuk { get; set; } = false;
    
    /// <summary>Denetim firması mı?</summary>
    public bool Denetim { get; set; } = false;
    
    /// <summary>Banka mı?</summary>
    public bool Banka { get; set; } = false;
    
    // ===== NDA TRACKING FIELDS =====
    
    /// <summary>Gizlilik sözleşmesi dosya dizin yolu (UNC veya yerel yol)</summary>
    public string? NdaFilePath { get; set; }
    
    /// <summary>NDA işlemini yapan kullanıcı</summary>
    public string? NdaUpdatedBy { get; set; }
    
    /// <summary>NDA işlem zamanı</summary>
    public DateTime? NdaUpdatedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

