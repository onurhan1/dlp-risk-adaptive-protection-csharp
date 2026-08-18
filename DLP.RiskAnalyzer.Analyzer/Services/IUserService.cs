using DLP.RiskAnalyzer.Analyzer.Data;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public interface IUserService
{
    UserEntity? GetByUsername(string username);
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default);
    bool ValidateCredentials(string username, string password, out UserEntity? user);
    Task<UserEntity> GetOrCreateExternalUserAsync(string username, string? email, CancellationToken ct = default);
    (string Hash, string Salt) CreatePasswordHash(string password);
    Task SeedDefaultAdminAsync(IConfiguration configuration, ILogger? logger = null);
}
