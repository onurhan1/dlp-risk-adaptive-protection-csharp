using System.Security.Cryptography;
using DLP.RiskAnalyzer.Analyzer.Data;
using Microsoft.EntityFrameworkCore;

namespace DLP.RiskAnalyzer.Analyzer.Services;

public class UserService : IUserService
{
    private readonly AnalyzerDbContext _db;

    public UserService(AnalyzerDbContext db)
    {
        _db = db;
    }

    public UserEntity? GetByUsername(string username) =>
        _db.Users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower() && u.IsActive);

    public async Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() && u.IsActive, ct);

    public bool ValidateCredentials(string username, string password, out UserEntity? user)
    {
        user = GetByUsername(username);
        if (user == null || string.IsNullOrWhiteSpace(password))
            return false;

        return VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
    }

    public async Task<UserEntity> GetOrCreateExternalUserAsync(string username, string? email, CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalized.ToLower(), ct);
        if (user != null)
        {
            if (!string.IsNullOrWhiteSpace(email) && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                user.Email = email.Trim();
            await _db.SaveChangesAsync(ct);
            return user;
        }

        var (hash, salt) = CreatePasswordHash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        user = new UserEntity
        {
            Username = normalized,
            Email = string.IsNullOrWhiteSpace(email) ? $"{normalized}@company.com" : email.Trim(),
            Role = "standard",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public (string Hash, string Salt) CreatePasswordHash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public async Task SeedDefaultAdminAsync(IConfiguration configuration, ILogger? logger = null)
    {
        try
        {
            if (await _db.Users.AnyAsync())
            {
                logger?.LogInformation("Users table already has data — skipping seed.");
                return;
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning("Users table check failed ({Message}), creating table...", ex.Message);
            await _db.Database.ExecuteSqlRawAsync(@"
                CREATE SCHEMA IF NOT EXISTS auth;
                CREATE TABLE IF NOT EXISTS auth.users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(100) NOT NULL UNIQUE,
                    email VARCHAR(255),
                    role VARCHAR(20) NOT NULL DEFAULT 'standard',
                    password_hash TEXT NOT NULL,
                    password_salt TEXT NOT NULL,
                    is_active BOOLEAN NOT NULL DEFAULT TRUE,
                    created_at TIMESTAMP NOT NULL DEFAULT NOW()
                );
            ");
        }

        var adminUser = configuration["Authentication:Username"] ?? "admin";
        var adminPass = configuration["Authentication:Password"] ?? "admin123";

        if (await _db.Users.AnyAsync())
        {
            logger?.LogInformation("Users table already has data — skipping seed.");
            return;
        }

        var (hash, salt) = CreatePasswordHash(adminPass);

        _db.Users.Add(new UserEntity
        {
            Username = adminUser,
            Email = $"{adminUser}@company.com",
            Role = "admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        logger?.LogInformation("Default admin user '{Username}' seeded into the database.", adminUser);
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return false;

        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expectedHash = Convert.FromBase64String(hash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeUsername(string value)
    {
        var normalized = value.Trim();
        var slash = normalized.LastIndexOf('\\');
        if (slash >= 0 && slash + 1 < normalized.Length) normalized = normalized[(slash + 1)..];
        var at = normalized.IndexOf('@');
        if (at > 0) normalized = normalized[..at];
        return normalized;
    }
}
