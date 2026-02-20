using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DLP.RiskAnalyzer.Analyzer.Data;
using System.Security.Cryptography;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AnalyzerDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AnalyzerDbContext db, IConfiguration configuration, ILogger<UsersController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    // ── Static helpers used by AuthController ───────────────────────────

    public static UserEntity? GetUserByUsername(AnalyzerDbContext db, string username) =>
        db.Users.FirstOrDefault(u => u.Username.ToLower() == username.ToLower() && u.IsActive);

    public static bool TryValidateCredentials(AnalyzerDbContext db, string username, string password, out UserEntity? user)
    {
        user = GetUserByUsername(db, username);
        if (user == null || string.IsNullOrWhiteSpace(password))
            return false;

        return VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
    }

    /// <summary>
    /// Seed the default admin user if the users table is empty.
    /// Called from Program.cs on application startup.
    /// </summary>
    public static async Task SeedDefaultAdminAsync(AnalyzerDbContext db, IConfiguration configuration, ILogger? logger = null)
    {
        // Ensure the users table exists (EnsureCreated won't help with migrations,
        // but we can safely try — if the table doesn't exist yet the migration will create it)
        try
        {
            if (await db.Users.AnyAsync())
            {
                logger?.LogInformation("Users table already has data — skipping seed.");
                return;
            }
        }
        catch (Exception ex)
        {
            // Table might not exist yet — create it via raw SQL as fallback
            logger?.LogWarning("Users table check failed ({Message}), creating table...", ex.Message);
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS users (
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

        // Re-check after possible table creation
        if (await db.Users.AnyAsync())
        {
            logger?.LogInformation("Users table already has data — skipping seed.");
            return;
        }

        var (hash, salt) = CreatePasswordHash(adminPass);

        db.Users.Add(new UserEntity
        {
            Username = adminUser,
            Email = $"{adminUser}@company.com",
            Role = "admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        logger?.LogInformation("Default admin user '{Username}' seeded into the database.", adminUser);
    }

    // ── CRUD endpoints ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult> GetUsers()
    {
        try
        {
            var users = await _db.Users
                .OrderBy(u => u.Id)
                .Select(u => UserResponse.FromEntity(u))
                .ToListAsync();

            return Ok(new { users, total = users.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetUser(int id)
    {
        try
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { detail = "User not found" });

            return Ok(UserResponse.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { detail = "Username and password are required" });

            if (await _db.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower()))
                return Conflict(new { detail = "Username already exists" });

            var role = request.Role ?? "standard";
            if (role != "admin" && role != "standard")
                return BadRequest(new { detail = "Role must be 'admin' or 'standard'" });

            var (hash, salt) = CreatePasswordHash(request.Password);

            var user = new UserEntity
            {
                Username = request.Username.Trim(),
                Email = request.Email ?? $"{request.Username.Trim()}@company.com",
                Role = role,
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User created: {Username} with role {Role}", user.Username, user.Role);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserResponse.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { detail = "User not found" });

            if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
            {
                if (await _db.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower() && u.Id != id))
                    return Conflict(new { detail = "Username already exists" });
                user.Username = request.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
                user.Email = request.Email;

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                if (request.Role != "admin" && request.Role != "standard")
                    return BadRequest(new { detail = "Role must be 'admin' or 'standard'" });
                user.Role = request.Role;
            }

            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                var (hash, salt) = CreatePasswordHash(request.Password);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation("User updated: {Username}", user.Username);
            return Ok(UserResponse.FromEntity(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { detail = "User not found" });

            var defaultAdmin = _configuration["Authentication:Username"] ?? "admin";
            if (user.Username.Equals(defaultAdmin, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "Cannot delete default admin user" });

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User deleted: {Username}", user.Username);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    // ── Password helpers ────────────────────────────────────────────────

    public static (string Hash, string Salt) CreatePasswordHash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return false;

        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expectedHash = Convert.FromBase64String(hash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Role { get; set; } = "standard";
}

public class UpdateUserRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Password { get; set; }
}

public class UserResponse
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "standard";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    public static UserResponse FromEntity(UserEntity e) => new()
    {
        Id = e.Id,
        Username = e.Username,
        Email = e.Email,
        Role = e.Role,
        CreatedAt = e.CreatedAt,
        IsActive = e.IsActive
    };
}

