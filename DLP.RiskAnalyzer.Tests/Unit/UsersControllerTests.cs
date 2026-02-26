using DLP.RiskAnalyzer.Analyzer.Controllers;
using DLP.RiskAnalyzer.Analyzer.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DLP.RiskAnalyzer.Tests.Unit;

public class UsersControllerTests : IDisposable
{
    private readonly AnalyzerDbContext _db;
    private readonly UsersController _sut;

    public UsersControllerTests()
    {
        var options = new DbContextOptionsBuilder<AnalyzerDbContext>()
            .UseInMemoryDatabase($"UsersTests_{Guid.NewGuid()}")
            .Options;
        _db = new AnalyzerDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "admin",
                ["Authentication:Password"] = "admin123"
            })
            .Build();

        _sut = new UsersController(_db, config, new Mock<ILogger<UsersController>>().Object);
    }

    // ── CreateUser ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_ValidRequest_ReturnsCreated()
    {
        var request = new CreateUserRequest
        {
            Username = "newuser",
            Password = "StrongPass1!",
            Role = "standard"
        };

        var result = await _sut.CreateUser(request);

        result.Should().BeOfType<CreatedAtActionResult>();
        _db.Users.Should().ContainSingle(u => u.Username == "newuser");
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsConflict()
    {
        var request = new CreateUserRequest { Username = "dupuser", Password = "Pass1!" };
        await _sut.CreateUser(request);

        var result = await _sut.CreateUser(new CreateUserRequest { Username = "dupuser", Password = "Pass2!" });
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateUser_EmptyUsername_ReturnsBadRequest()
    {
        var result = await _sut.CreateUser(new CreateUserRequest { Username = "", Password = "Pass!" });
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateUser_InvalidRole_ReturnsBadRequest()
    {
        var result = await _sut.CreateUser(new CreateUserRequest
        {
            Username = "roleuser",
            Password = "Pass1!",
            Role = "superadmin"
        });
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetUsers ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        await _sut.CreateUser(new CreateUserRequest { Username = "u1", Password = "P1!" });
        await _sut.CreateUser(new CreateUserRequest { Username = "u2", Password = "P2!" });

        var result = await _sut.GetUsers();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var props = okResult.Value!.GetType().GetProperty("total");
        ((int)props!.GetValue(okResult.Value)!).Should().Be(2);
    }

    // ── GetUser ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUser_ExistingId_ReturnsOk()
    {
        await _sut.CreateUser(new CreateUserRequest { Username = "findme", Password = "P!" });
        var user = await _db.Users.FirstAsync(u => u.Username == "findme");

        var result = await _sut.GetUser(user.Id);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUser_NonExistentId_ReturnsNotFound()
    {
        var result = await _sut.GetUser(99999);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── UpdateUser ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ChangeRole_Succeeds()
    {
        await _sut.CreateUser(new CreateUserRequest { Username = "upuser", Password = "P!", Role = "standard" });
        var user = await _db.Users.FirstAsync(u => u.Username == "upuser");

        var result = await _sut.UpdateUser(user.Id, new UpdateUserRequest { Role = "admin" });
        result.Should().BeOfType<OkObjectResult>();

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.Role.Should().Be("admin");
    }

    [Fact]
    public async Task UpdateUser_PasswordChange_UpdatesHash()
    {
        await _sut.CreateUser(new CreateUserRequest { Username = "pwuser", Password = "Old!" });
        var user = await _db.Users.FirstAsync(u => u.Username == "pwuser");
        var oldHash = user.PasswordHash;

        await _sut.UpdateUser(user.Id, new UpdateUserRequest { Password = "NewPassword!" });

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.PasswordHash.Should().NotBe(oldHash);
    }

    // ── DeleteUser ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_ExistingUser_ReturnsNoContent()
    {
        await _sut.CreateUser(new CreateUserRequest { Username = "deluser", Password = "P!" });
        var user = await _db.Users.FirstAsync(u => u.Username == "deluser");

        var result = await _sut.DeleteUser(user.Id);
        result.Should().BeOfType<NoContentResult>();
        (await _db.Users.FindAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteUser_DefaultAdmin_ReturnsBadRequest()
    {
        var (h, s) = UsersController.CreatePasswordHash("p");
        _db.Users.Add(new UserEntity
        {
            Username = "admin",
            PasswordHash = h,
            PasswordSalt = s,
            Role = "admin",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var admin = await _db.Users.FirstAsync(u => u.Username == "admin");
        var result = await _sut.DeleteUser(admin.Id);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Password hashing ─────────────────────────────────────────────────────

    [Fact]
    public void CreatePasswordHash_ProducesUniqueHashPerCall()
    {
        var (h1, s1) = UsersController.CreatePasswordHash("same");
        var (h2, s2) = UsersController.CreatePasswordHash("same");

        s1.Should().NotBe(s2, "each call should generate a unique salt");
        h1.Should().NotBe(h2, "different salt → different hash");
    }

    // ── SeedDefaultAdmin ─────────────────────────────────────────────────────

    [Fact]
    public async Task SeedDefaultAdmin_EmptyDb_CreatesAdmin()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "seededadmin",
                ["Authentication:Password"] = "seedpass"
            })
            .Build();

        await UsersController.SeedDefaultAdminAsync(_db, config);

        _db.Users.Should().ContainSingle(u => u.Username == "seededadmin" && u.Role == "admin");
    }

    [Fact]
    public async Task SeedDefaultAdmin_WithExistingUsers_DoesNotDuplicate()
    {
        var (h, s) = UsersController.CreatePasswordHash("x");
        _db.Users.Add(new UserEntity
        {
            Username = "existing",
            PasswordHash = h,
            PasswordSalt = s,
            Role = "standard",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Username"] = "admin",
                ["Authentication:Password"] = "admin123"
            })
            .Build();

        await UsersController.SeedDefaultAdminAsync(_db, config);

        _db.Users.Should().HaveCount(1);
    }

    public void Dispose() => _db.Dispose();
}
