namespace DLP.RiskAnalyzer.Analyzer.Models;

/// <summary>
/// Kullanıcı aktivite logları - Sayfa ziyaretleri, giriş/çıkış, işlem takibi
/// LDAP entegrasyonu geldiğinde de çalışacak şekilde tasarlanmıştır.
/// UserName alanı hem yerel kullanıcı hem de LDAP/AD kullanıcı adını tutabilir.
/// </summary>
public class UserActivityLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Kullanıcı adı - Yerel veya LDAP/AD kullanıcı adı.
    /// LDAP geldiğinde format: "domain\username" veya "username@domain" olabilir.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
    
    /// <summary>
    /// Kimlik doğrulama kaynağı - "Local" veya "LDAP" 
    /// LDAP geldiğinde hangi kullanıcının LDAP ile giriş yaptığını ayırt etmek için
    /// </summary>
    public string AuthSource { get; set; } = "Local";
    
    /// <summary>
    /// Aktivite tipi:
    /// - "PageVisit": Sayfa ziyareti
    /// - "Login": Giriş
    /// - "Logout": Çıkış
    /// - "DomainFeatureUpdate": Domain özelliği güncelleme
    /// - "ExceptionCreate": İstisna oluşturma
    /// - "ExceptionUpload": Excel yükleme
    /// - "ExceptionDelete": İstisna silme
    /// - "SettingsChange": Ayar değişikliği
    /// - "DataExport": Veri dışa aktarma
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;
    
    /// <summary>Ziyaret edilen sayfa yolu (örn: /exceptions/domain-features)</summary>
    public string? PagePath { get; set; }
    
    /// <summary>Sayfa başlığı (örn: Domain Features)</summary>
    public string? PageTitle { get; set; }
    
    /// <summary>İşlem detayı (örn: NDA updated for cisco.com)</summary>
    public string? ActionDetail { get; set; }
    
    /// <summary>Kullanıcı IP adresi</summary>
    public string? IpAddress { get; set; }
    
    /// <summary>Sayfada kalma süresi (saniye)</summary>
    public int? SessionDurationSeconds { get; set; }
    
    /// <summary>Tarayıcı bilgisi</summary>
    public string? UserAgent { get; set; }
}
