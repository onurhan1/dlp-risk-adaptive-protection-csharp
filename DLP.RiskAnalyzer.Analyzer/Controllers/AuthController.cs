using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

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
            // Model validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Task.FromResult<ActionResult<LoginResponse>>(BadRequest(new { detail = string.Join("; ", errors) }));
            }
            
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Task.FromResult<ActionResult<LoginResponse>>(BadRequest(new { detail = "Username and password are required" }));
            }

            // Normalize username and password to prevent encoding issues
            // Trim whitespace and ensure UTF-8 encoding
            var normalizedUsername = request.Username.Trim();
            var normalizedPassword = request.Password.Trim();
            
            // Remove any zero-width characters or BOM that might cause issues
            normalizedPassword = System.Text.RegularExpressions.Regex.Replace(normalizedPassword, @"\p{C}", string.Empty);

            if (!UsersController.TryValidateCredentials(normalizedUsername, normalizedPassword, out var user))
            {
                _logger.LogWarning("Failed login attempt for username: {Username}", request.Username);
                return Task.FromResult<ActionResult<LoginResponse>>(Unauthorized(new { detail = "Invalid username or password" }));
            }

            // Generate JWT token (simplified - use proper JWT library in production)
            var token = GenerateToken(normalizedUsername, user!.Role);
            var expiresAt = DateTime.UtcNow.AddHours(8); // Token expires in 8 hours

            _logger.LogInformation("Successful login for username: {Username} with role {Role}", normalizedUsername, user.Role);

            return Task.FromResult<ActionResult<LoginResponse>>(Ok(new LoginResponse
            {
                Token = token,
                Username = normalizedUsername,
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

            var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
            var issuer = _configuration["Jwt:Issuer"] ?? "DLP-RiskAnalyzer";
            var audience = _configuration["Jwt:Audience"] ?? "DLP-RiskAnalyzer-Client";

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            try
            {
                tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var username = jwtToken.Claims.First(x => x.Type == ClaimTypes.Name).Value;
                var role = jwtToken.Claims.First(x => x.Type == ClaimTypes.Role).Value;

                return Ok(new { valid = true, username, role });
            }
            catch
            {
                return Ok(new { valid = false });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { detail = "An error occurred during token validation" });
        }
    }

    private string GenerateToken(string username, string role)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";
        var issuer = _configuration["Jwt:Issuer"] ?? "DLP-RiskAnalyzer";
        var audience = _configuration["Jwt:Audience"] ?? "DLP-RiskAnalyzer-Client";
        var expirationHours = _configuration.GetValue<int>("Jwt:ExpirationHours", 8);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
    public string Username { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Password is required")]
    // Note: No MinLength validation for login - we're validating existing password, not creating new one
    // Password strength validation is only applied when creating/updating users
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

