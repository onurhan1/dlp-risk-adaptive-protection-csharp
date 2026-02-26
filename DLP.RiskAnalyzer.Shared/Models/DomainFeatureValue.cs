using System.Text.Json.Serialization;

namespace DLP.RiskAnalyzer.Shared.Models;

/// <summary>
/// Domain ve özellik arasındaki değer ilişkisi
/// </summary>
public class DomainFeatureValue
{
    public int Id { get; set; }
    
    public int DomainId { get; set; }
    [JsonIgnore]
    public virtual NdaDomain Domain { get; set; } = null!;
    
    public int FeatureId { get; set; }
    public virtual DomainFeatureDefinition Feature { get; set; } = null!;
    
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
