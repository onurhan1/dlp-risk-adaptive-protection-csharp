namespace DLP.RiskAnalyzer.Analyzer.Services;

/// <summary>
/// Development-only data seeder interface.
/// Activated when "SeedData:Enabled" = true in configuration.
/// </summary>
public interface IDevDataSeeder
{
    Task SeedAsync();
}
