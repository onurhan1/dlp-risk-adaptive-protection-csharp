namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// NDA Domain model - Gizlilik sözleşmesi domain yönetimi
/// Destination risk skoru hesaplaması için kullanılır
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
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
