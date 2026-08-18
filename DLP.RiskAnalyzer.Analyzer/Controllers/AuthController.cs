using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using DLP.RiskAnalyzer.Analyzer.Auth;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;

namespace DLP.RiskAnalyzer.Analyzer.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthJwtSettings _jwt;
    private readonly ILogger<AuthController> _logger;
    private readonly AnalyzerDbContext _db;
    private readonly IUserService _userService;
    private readonly IDirectorySettingsService _directorySettingsService;

    public AuthController(
        AnalyzerDbContext db,
        AuthJwtSettings jwt,
        ILogger<AuthController> logger,
        IUserService userService,
        IDirectorySettingsService directorySettingsService)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
        _userService = userService;
        _directorySettingsService = directorySettingsService;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            // Model validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { detail = string.Join("; ", errors) });
            }
            
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { detail = "Username and password are required" });
            }

            // Normalize username and password to prevent encoding issues
            // Trim whitespace and ensure UTF-8 encoding
            var normalizedUsername = request.Username.Trim();
            var normalizedPassword = NormalizePassword(request.Password);

            _logger.LogInformation("Login attempt - Username: '{Username}' (Length: {UserLen}), Password Length: {PassLen}", 
                normalizedUsername, normalizedUsername.Length, normalizedPassword.Length);

            var ldapSettings = await _directorySettingsService.GetLdapAsync();
            if (ldapSettings.Enabled)
                return await LoginWithLdapAsync(normalizedUsername, normalizedPassword);
            
            var userExists = _userService.GetByUsername(normalizedUsername);
            if (userExists == null)
            {
                _logger.LogWarning("Login failed - User not found: {Username}", normalizedUsername);
                return Unauthorized(new { detail = "Invalid username or password" });
            }
            
            _logger.LogInformation("User found - Username: {Username}, HasPasswordHash: {HasHash}, HasPasswordSalt: {HasSalt}", 
                userExists.Username, !string.IsNullOrEmpty(userExists.PasswordHash), !string.IsNullOrEmpty(userExists.PasswordSalt));

            if (!_userService.ValidateCredentials(normalizedUsername, normalizedPassword, out var user))
            {
                _logger.LogWarning("Login failed - Password validation failed for username: {Username}", normalizedUsername);
                return Unauthorized(new { detail = "Invalid username or password" });
            }
            
            _logger.LogInformation("Password validation successful for username: {Username}", normalizedUsername);

            // Generate JWT token (simplified - use proper JWT library in production)
            var token = GenerateToken(normalizedUsername, user!.Role);
            var expiresAt = DateTime.UtcNow.AddHours(_jwt.ExpirationHours);

            _logger.LogInformation("Successful login for username: {Username} with role {Role}", normalizedUsername, user.Role);

            return Ok(new LoginResponse
            {
                Token = token,
                Username = normalizedUsername,
                Role = user.Role,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { detail = "An error occurred during login" });
        }
    }

    [HttpPost("ldap-login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> LdapLogin([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { detail = string.Join("; ", errors) });
            }

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { detail = "Username and password are required" });

            return await LoginWithLdapAsync(request.Username.Trim(), NormalizePassword(request.Password));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LDAP login");
            return StatusCode(500, new { detail = "An error occurred during LDAP login" });
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

            var secretKey = _jwt.SecretKey;
            var issuer = _jwt.Issuer;
            var audience = _jwt.Audience;

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
        var secretKey = _jwt.SecretKey;
        var issuer = _jwt.Issuer;
        var audience = _jwt.Audience;
        var expirationHours = _jwt.ExpirationHours;

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

    private async Task<ActionResult<LoginResponse>> LoginWithLdapAsync(string username, string password)
    {
        var ldapResult = await _directorySettingsService.AuthenticateLdapAsync(username, password);
        if (!ldapResult.Success)
        {
            _logger.LogWarning("LDAP login failed for username: {Username}. Reason: {Reason}", username, ldapResult.Message);
            return Unauthorized(new { detail = "Invalid username or password" });
        }

        var ldapUser = await _userService.GetOrCreateExternalUserAsync(ldapResult.Username, ldapResult.Email);
        if (!ldapUser.IsActive)
        {
            _logger.LogWarning("LDAP login rejected because user is inactive: {Username}", ldapUser.Username);
            return Unauthorized(new { detail = "User is inactive" });
        }

        var token = GenerateToken(ldapUser.Username, ldapUser.Role);
        var expiresAt = DateTime.UtcNow.AddHours(_jwt.ExpirationHours);

        _logger.LogInformation("Successful LDAP login for username: {Username} with role {Role}", ldapUser.Username, ldapUser.Role);

        return Ok(new LoginResponse
        {
            Token = token,
            Username = ldapUser.Username,
            Role = ldapUser.Role,
            ExpiresAt = expiresAt
        });
    }

    private static string NormalizePassword(string password)
    {
        var normalizedPassword = password.Trim();
        normalizedPassword = System.Text.RegularExpressions.Regex.Replace(normalizedPassword, @"\p{C}", string.Empty);
        normalizedPassword = normalizedPassword.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
        return System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(normalizedPassword));
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
