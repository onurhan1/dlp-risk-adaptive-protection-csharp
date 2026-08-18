using DLP.RiskAnalyzer.Analyzer.Auth;
using DLP.RiskAnalyzer.Analyzer.Controllers;
using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Models;
using DLP.RiskAnalyzer.Analyzer.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class AuthControllerTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly AuthController _sut;
    private readonly AuthJwtSettings _jwt;

    public AuthControllerTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuthTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);

        _jwt = new AuthJwtSettings
        {
            SecretKey = "TestSecretKeyThatIsAtLeast32CharactersLong!!",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpirationHours = 8
        };

        var logger = new Mock<ILogger<AuthController>>();
        var directorySettingsService = new Mock<IDirectorySettingsService>();
        directorySettingsService
            .Setup(s => s.GetLdapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LdapSettingsResponse { Enabled = false });

        _sut = new AuthController(
            _db,
            _jwt,
            logger.Object,
            new UserService(_db),
            directorySettingsService.Object);

        SeedTestUser("testuser", "TestPassword123!");
    }

    private void SeedTestUser(string username, string password)
    {
        var (hash, salt) = UsersController.CreatePasswordHash(password);
        _db.Users.Add(new UserEntity
        {
            Username = username,
            Email = $"{username}@test.com",
            Role = "admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };
        var result = await _sut.Login(request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;

        response.Token.Should().NotBeNullOrEmpty();
        response.Username.Should().Be("testuser");
        response.Role.Should().Be("admin");
        response.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var request = new LoginRequest { Username = "testuser", Password = "WrongPassword!" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var request = new LoginRequest { Username = "nosuchuser", Password = "Any" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_EmptyUsername_ReturnsBadRequest()
    {
        var request = new LoginRequest { Username = "", Password = "SomePass" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_EmptyPassword_ReturnsBadRequest()
    {
        var request = new LoginRequest { Username = "testuser", Password = "" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_TokenContainsCorrectClaims()
    {
        var request = new LoginRequest { Username = "testuser", Password = "TestPassword123!" };
        var result = await _sut.Login(request);

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as LoginResponse;

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response!.Token);

        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Name && c.Value == "testuser");
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "admin");
        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsValid()
    {
        var loginResult = await _sut.Login(new LoginRequest { Username = "testuser", Password = "TestPassword123!" });
        var loginResponse = ((OkObjectResult)loginResult.Result!).Value as LoginResponse;

        var validateResult = _sut.ValidateToken(new ValidateTokenRequest { Token = loginResponse!.Token });

        validateResult.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsInvalid()
    {
        var result = _sut.ValidateToken(new ValidateTokenRequest { Token = "invalid.jwt.token" });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var props = okResult.Value!.GetType().GetProperty("valid");
        props!.GetValue(okResult.Value).Should().Be(false);
    }

    [Fact]
    public void ValidateToken_EmptyToken_ReturnsBadRequest()
    {
        var result = _sut.ValidateToken(new ValidateTokenRequest { Token = "" });
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_CaseInsensitiveUsername_Works()
    {
        var request = new LoginRequest { Username = "TESTUSER", Password = "TestPassword123!" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsUnauthorized()
    {
        var (hash, salt) = UsersController.CreatePasswordHash("InactivePass!");
        _db.Users.Add(new UserEntity
        {
            Username = "inactiveuser",
            Email = "inactive@test.com",
            Role = "standard",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var request = new LoginRequest { Username = "inactiveuser", Password = "InactivePass!" };
        var result = await _sut.Login(request);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    public void Dispose() => _db.Dispose();
}
