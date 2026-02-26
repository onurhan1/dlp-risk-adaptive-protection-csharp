using DLP.RiskAnalyzer.Analyzer.Data;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IUserService
{
    UserEntity? GetByUsername(string username);
    bool ValidateCredentials(string username, string password, out UserEntity? user);
    (string Hash, string Salt) CreatePasswordHash(string password);
    Task SeedDefaultAdminAsync(IConfiguration configuration, ILogger? logger = null);
}
