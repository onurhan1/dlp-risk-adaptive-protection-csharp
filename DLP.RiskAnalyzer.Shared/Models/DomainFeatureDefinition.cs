using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Dinamik domain özelliği tanımı (örn: "Sağlık Sektörü", "Riskli Tedarikçi")
/// </summary>
public class DomainFeatureDefinition
{
    public int Id { get; set; }
    
    public string KeyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public virtual ICollection<DomainFeatureValue> Values { get; set; } = new List<DomainFeatureValue>();
}
