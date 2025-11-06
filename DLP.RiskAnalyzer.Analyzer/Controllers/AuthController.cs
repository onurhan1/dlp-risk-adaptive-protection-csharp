using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { detail = "Username and password are required" });
            }

            // Check if user exists in UsersController
            var user = UsersController.GetUserByUsername(request.Username);
            
            // If user not found, check default admin credentials
            string role = "standard";
            bool isValid = false;

            if (user != null)
            {
                // User exists in system - for now, accept any password (in production, use proper password hashing)
                isValid = true;
                role = user.Role;
            }
            else
            {
                // Fallback to default admin
                var validUsername = _configuration["Authentication:Username"] ?? "admin";
                var validPassword = _configuration["Authentication:Password"] ?? "admin123";

                if (request.Username == validUsername && request.Password == validPassword)
                {
                    isValid = true;
                    role = "admin";
                }
            }

            if (!isValid)
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return Unauthorized(new { detail = "Invalid username or password" });
            }

            // Generate JWT token (simplified - use proper JWT library in production)
            var token = GenerateToken(request.Username, role);
            var expiresAt = DateTime.UtcNow.AddHours(8); // Token expires in 8 hours

            _logger.LogInformation("Successful login for username: {Username} with role {Role}", request.Username, role);

            return Ok(new LoginResponse
            {
                Token = token,
                Username = request.Username,
                Role = role,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { detail = "An error occurred during login" });
        }
    }

    [HttpPost("validate")]
    public ActionResult ValidateToken([FromBody] ValidateTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new { detail = "Token is required" });
            }

            // Simple token validation - in production, use proper JWT validation
            // For now, just check if token is not empty
            return Ok(new { valid = !string.IsNullOrWhiteSpace(request.Token) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { detail = "An error occurred during token validation" });
        }
    }

    private string GenerateToken(string username, string role)
    {
        // Simple token generation - in production, use proper JWT library like System.IdentityModel.Tokens.Jwt
        var tokenData = $"{username}:{role}:{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
        var bytes = Encoding.UTF8.GetBytes(tokenData);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "standard";
    public DateTime ExpiresAt { get; set; }
}

public class ValidateTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

