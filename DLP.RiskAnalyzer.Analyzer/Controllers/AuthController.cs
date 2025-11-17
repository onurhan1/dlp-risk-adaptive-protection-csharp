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
    public Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Task.FromResult<ActionResult<LoginResponse>>(BadRequest(new { detail = "Username and password are required" }));
            }

            if (!UsersController.TryValidateCredentials(request.Username, request.Password, out var user))
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return Task.FromResult<ActionResult<LoginResponse>>(Unauthorized(new { detail = "Invalid username or password" }));
            }

            // Generate JWT token (simplified - use proper JWT library in production)
            var token = GenerateToken(request.Username, user!.Role);
            var expiresAt = DateTime.UtcNow.AddHours(8); // Token expires in 8 hours

            _logger.LogInformation("Successful login for username: {Username} with role {Role}", request.Username, user.Role);

            return Task.FromResult<ActionResult<LoginResponse>>(Ok(new LoginResponse
            {
                Token = token,
                Username = request.Username,
                Role = user.Role,
                ExpiresAt = expiresAt
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return Task.FromResult<ActionResult<LoginResponse>>(StatusCode(500, new { detail = "An error occurred during login" }));
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

