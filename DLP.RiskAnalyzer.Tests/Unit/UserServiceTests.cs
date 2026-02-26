using DLP.RiskAnalyzer.Analyzer.Data;
using DLP.RiskAnalyzer.Analyzer.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class UserServiceTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"UserServiceTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);
        _sut = new UserService(_db);
    }

    public void Dispose() => _db.Dispose();

    private UserEntity SeedUser(string username = "testuser", string password = "Pass123!", bool isActive = true)
    {
        var (hash, salt) = _sut.CreatePasswordHash(password);
        var user = new UserEntity
        {
            Username = username,
            Email = $"{username}@company.com",
            Role = "standard",
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        return user;
    }

    // ─── GetByUsername ────────────────────────────────────────────

    [Fact]
    public void GetByUsername_ExistingUser_ReturnsUser()
    {
        SeedUser("alice");

        var result = _sut.GetByUsername("alice");

        result.Should().NotBeNull();
        result!.Username.Should().Be("alice");
    }

    [Fact]
    public void GetByUsername_SameCase_ReturnsUser()
    {
        SeedUser("alice");

        var result = _sut.GetByUsername("alice");

        result.Should().NotBeNull();
        result!.Username.Should().Be("alice");
    }

    [Fact]
    public void GetByUsername_NonExistentUser_ReturnsNull()
    {
        var result = _sut.GetByUsername("nobody");

        result.Should().BeNull();
    }

    [Fact]
    public void GetByUsername_InactiveUser_ReturnsNull()
    {
        SeedUser("inactive_user", isActive: false);

        var result = _sut.GetByUsername("inactive_user");

        result.Should().BeNull();
    }

    // ─── ValidateCredentials ─────────────────────────────────────

    [Fact]
    public void ValidateCredentials_CorrectPassword_ReturnsTrue()
    {
        SeedUser("bob", "SecurePass1!");

        var valid = _sut.ValidateCredentials("bob", "SecurePass1!", out var user);

        valid.Should().BeTrue();
        user.Should().NotBeNull();
        user!.Username.Should().Be("bob");
    }

    [Fact]
    public void ValidateCredentials_WrongPassword_ReturnsFalse()
    {
        SeedUser("bob", "SecurePass1!");

        var valid = _sut.ValidateCredentials("bob", "WrongPass!", out var user);

        valid.Should().BeFalse();
    }

    [Fact]
    public void ValidateCredentials_NonExistentUser_ReturnsFalse()
    {
        var valid = _sut.ValidateCredentials("ghost", "anything", out var user);

        valid.Should().BeFalse();
        user.Should().BeNull();
    }

    [Fact]
    public void ValidateCredentials_EmptyPassword_ReturnsFalse()
    {
        SeedUser("bob", "SecurePass1!");

        var valid = _sut.ValidateCredentials("bob", "", out _);

        valid.Should().BeFalse();
    }

    [Fact]
    public void ValidateCredentials_NullPassword_ReturnsFalse()
    {
        SeedUser("bob", "SecurePass1!");

        var valid = _sut.ValidateCredentials("bob", null!, out _);

        valid.Should().BeFalse();
    }

    [Fact]
    public void ValidateCredentials_InactiveUser_ReturnsFalse()
    {
        SeedUser("disabled", "Pass123!", isActive: false);

        var valid = _sut.ValidateCredentials("disabled", "Pass123!", out var user);

        valid.Should().BeFalse();
        user.Should().BeNull();
    }

    // ─── CreatePasswordHash ──────────────────────────────────────

    [Fact]
    public void CreatePasswordHash_ProducesNonEmptyHashAndSalt()
    {
        var (hash, salt) = _sut.CreatePasswordHash("test");

        hash.Should().NotBeNullOrWhiteSpace();
        salt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreatePasswordHash_SamePassword_ProducesDifferentSalts()
    {
        var (hash1, salt1) = _sut.CreatePasswordHash("same");
        var (hash2, salt2) = _sut.CreatePasswordHash("same");

        salt1.Should().NotBe(salt2);
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void CreatePasswordHash_ResultIsBase64Encoded()
    {
        var (hash, salt) = _sut.CreatePasswordHash("test");

        var hashAction = () => Convert.FromBase64String(hash);
        var saltAction = () => Convert.FromBase64String(salt);

        hashAction.Should().NotThrow();
        saltAction.Should().NotThrow();
    }

    // ─── SeedDefaultAdminAsync ───────────────────────────────────

    [Fact]
    public async Task SeedDefaultAdminAsync_EmptyDb_CreatesAdmin()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "superadmin",
                ["Authentication:Password"] = "SuperPass!"
            })
            .Build();

        await _sut.SeedDefaultAdminAsync(config);

        var users = await _db.Users.ToListAsync();
        users.Should().HaveCount(1);
        users[0].Username.Should().Be("superadmin");
        users[0].Role.Should().Be("admin");
        users[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_ExistingUsers_DoesNotDuplicate()
    {
        SeedUser("existing");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "admin",
                ["Authentication:Password"] = "admin123"
            })
            .Build();

        await _sut.SeedDefaultAdminAsync(config);

        var users = await _db.Users.ToListAsync();
        users.Should().HaveCount(1);
        users[0].Username.Should().Be("existing");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_UsesDefaultValuesWhenConfigMissing()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        await _sut.SeedDefaultAdminAsync(config);

        var admin = await _db.Users.FirstOrDefaultAsync();
        admin.Should().NotBeNull();
        admin!.Username.Should().Be("admin");
    }

    [Fact]
    public async Task SeedDefaultAdminAsync_SeededUser_CanLogin()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "admin",
                ["Authentication:Password"] = "secret123"
            })
            .Build();

        await _sut.SeedDefaultAdminAsync(config);

        var valid = _sut.ValidateCredentials("admin", "secret123", out var user);
        valid.Should().BeTrue();
        user.Should().NotBeNull();
    }
}
