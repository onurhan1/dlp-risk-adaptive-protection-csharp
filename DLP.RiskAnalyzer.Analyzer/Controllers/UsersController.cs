using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UsersController> _logger;
    private static readonly List<UserModel> _users = new();
    private static bool _initialized = false;

    public static List<UserModel> Users => _users;

    public static UserModel? GetUserByUsername(string username) =>
        _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && u.IsActive);

    public static bool TryValidateCredentials(string username, string password, out UserModel? user)
    {
        user = GetUserByUsername(username);
        if (user == null || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        return VerifyPassword(password, user.PasswordHash, user.PasswordSalt);
    }

    public UsersController(IConfiguration configuration, ILogger<UsersController> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Initialize with default admin user if not already done
        if (!_initialized)
        {
            var defaultAdmin = _configuration["Authentication:Username"] ?? "admin";
            var defaultPassword = _configuration["Authentication:Password"] ?? "admin123";
            
            var (hash, salt) = CreatePasswordHash(defaultPassword);
            _users.Add(new UserModel
            {
                Id = 1,
                Username = defaultAdmin,
                Email = $"{defaultAdmin}@company.com",
                Role = "admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                PasswordHash = hash,
                PasswordSalt = salt
            });
            
            _initialized = true;
        }
    }

    [HttpGet]
    public ActionResult<Dictionary<string, object>> GetUsers()
    {
        try
        {
            return Ok(new 
            { 
                users = _users.Select(UserResponse.FromModel).ToList(), 
                total = _users.Count 
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while creating user");
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public ActionResult<UserModel> GetUser(int id)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { detail = "User not found" });
            }
            return Ok(UserResponse.FromModel(user));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error while updating user");
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPost]
    public ActionResult<UserModel> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { detail = "Username and password are required" });
            }

            if (_users.Any(u => u.Username == request.Username))
            {
                return Conflict(new { detail = "Username already exists" });
            }

            if (request.Role != "admin" && request.Role != "standard")
            {
                return BadRequest(new { detail = "Role must be 'admin' or 'standard'" });
            }

            ValidatePasswordStrength(request.Password);
            var (hash, salt) = CreatePasswordHash(request.Password);

            var newUser = new UserModel
            {
                Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1,
                Username = request.Username,
                Email = request.Email ?? $"{request.Username}@company.com",
                Role = request.Role ?? "standard",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                PasswordHash = hash,
                PasswordSalt = salt
            };

            _users.Add(newUser);

            _logger.LogInformation("User created: {Username} with role {Role}", newUser.Username, newUser.Role);

            return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, UserResponse.FromModel(newUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public ActionResult<UserModel> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { detail = "User not found" });
            }

            if (!string.IsNullOrWhiteSpace(request.Username) && request.Username != user.Username)
            {
                if (_users.Any(u => u.Username == request.Username && u.Id != id))
                {
                    return Conflict(new { detail = "Username already exists" });
                }
                user.Username = request.Username;
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                user.Email = request.Email;
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                if (request.Role != "admin" && request.Role != "standard")
                {
                    return BadRequest(new { detail = "Role must be 'admin' or 'standard'" });
                }
                user.Role = request.Role;
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                ValidatePasswordStrength(request.Password);
                var (hash, salt) = CreatePasswordHash(request.Password);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
            }

            _logger.LogInformation("User updated: {Username}", user.Username);

            return Ok(UserResponse.FromModel(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteUser(int id)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user == null)
            {
                return NotFound(new { detail = "User not found" });
            }

            // Don't allow deleting the default admin user
            var defaultAdmin = _configuration["Authentication:Username"] ?? "admin";
            if (user.Username == defaultAdmin)
            {
                return BadRequest(new { detail = "Cannot delete default admin user" });
            }

            _users.Remove(user);

            _logger.LogInformation("User deleted: {Username}", user.Username);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return StatusCode(500, new { detail = ex.Message });
        }
    }

    private static void ValidatePasswordStrength(string password)
    {
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            throw new ArgumentException("Password must be at least 12 characters and include upper, lower case letters and digits.");
        }
    }

    private static (string Hash, string Salt) CreatePasswordHash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public class UserModel
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "standard"; // "admin" or "standard"
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
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

    public static UserResponse FromModel(UserModel model) => new()
    {
        Id = model.Id,
        Username = model.Username,
        Email = model.Email,
        Role = model.Role,
        CreatedAt = model.CreatedAt,
        IsActive = model.IsActive
    };
}

