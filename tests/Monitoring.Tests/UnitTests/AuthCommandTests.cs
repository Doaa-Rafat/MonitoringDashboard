using Application.Common.Interfaces;
using Application.Features.Auth;
using Application.Features.Auth.Commands;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Auth;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Monitoring.Tests.UnitTests;

public class AuthCommandTests
{
    [Theory]
    [InlineData("", "test@example.com", "Password123", "UserName")]
    [InlineData("john", "bad-email", "Password123", "Email")]
    [InlineData("john", "john@example.com", "short", "Password")]
    public void RegisterValidator_ShouldRejectInvalidInputs(string userName, string email, string password, string field)
    {
        var validator = new RegisterCommandValidator();

        var result = validator.Validate(new RegisterCommand(userName, email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public async Task RegisterCommandHandler_ShouldCreateUser_AndIssueTokens()
    {
        await using var db = CreateDb();
        await db.Roles.AddAsync(new Role { Name = RoleNames.User });
        await db.SaveChangesAsync();

        var handler = new RegisterCommandHandler(db, new FakePasswordHashService(), new FakeJwtTokenService());

        var result = await handler.Handle(new RegisterCommand("alice", "alice@example.com", "Password123"), CancellationToken.None);

        Assert.Equal("alice", result.User.UserName);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DbSeeder_ShouldCreateDefaultRolesAndUsers_WhenDatabaseIsEmpty()
    {
        await using var db = CreateDb();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Admin:UserName"] = "admin",
                ["Seed:Admin:Email"] = "admin@monitoring.local",
                ["Seed:Admin:Password"] = "Admin@123",
                ["Seed:User:UserName"] = "user",
                ["Seed:User:Email"] = "user@monitoring.local",
                ["Seed:User:Password"] = "User@123"
            })
            .Build();

        var seeder = new DbSeeder(db, new PasswordHashService(), config, NullLogger<DbSeeder>.Instance);

        await seeder.SeedAsync();

        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Contains(await db.Users.ToListAsync(), u => u.UserName == "admin");
        Assert.Contains(await db.Users.ToListAsync(), u => u.UserName == "user");
    }

    [Fact]
    public async Task DbSeeder_ShouldUpdateExistingSeedUserCredentials_ToMatchConfiguredValues()
    {
        await using var db = CreateDb();
        await db.Roles.AddRangeAsync(new Role { Name = RoleNames.Admin }, new Role { Name = RoleNames.User });
        await db.Users.AddAsync(new User
        {
            UserName = "admin",
            Email = "admin@example.com",
            PasswordHash = "stale-hash",
            Roles = { new Role { Name = RoleNames.Admin } }
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Admin:UserName"] = "admin",
                ["Seed:Admin:Email"] = "admin@monitoring.local",
                ["Seed:Admin:Password"] = "Admin@123",
                ["Seed:User:UserName"] = "user",
                ["Seed:User:Email"] = "user@monitoring.local",
                ["Seed:User:Password"] = "User@123"
            })
            .Build();

        var seeder = new DbSeeder(db, new PasswordHashService(), config, NullLogger<DbSeeder>.Instance);

        await seeder.SeedAsync();

        var updated = await db.Users.SingleAsync(u => u.UserName == "admin");
        Assert.Equal("admin@monitoring.local", updated.Email);
        Assert.NotEqual("stale-hash", updated.PasswordHash);
    }

    [Fact]
    public async Task LoginCommandHandler_ShouldRejectInvalidCredentials()
    {
        await using var db = CreateDb();
        var role = new Role { Name = RoleNames.User };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(new User
        {
            UserName = "bob",
            Email = "bob@example.com",
            PasswordHash = new FakePasswordHashService().Hash("Password123"),
            Roles = { role }
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(db, new FakePasswordHashService(), new FakeJwtTokenService());

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("bob", "WrongPassword!"), CancellationToken.None));

        Assert.Equal("Invalid credentials.", ex.Message);
    }

    [Fact]
    public async Task RefreshCommandHandler_ShouldRejectExpiredToken()
    {
        await using var db = CreateDb();
        var role = new Role { Name = RoleNames.User };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(new User
        {
            UserName = "charlie",
            Email = "charlie@example.com",
            RefreshToken = "expired-hash",
            RefreshTokenExpiry = DateTime.UtcNow.AddMinutes(-5),
            PasswordHash = "hash",
            Roles = { role }
        });
        await db.SaveChangesAsync();

        var jwt = new FakeJwtTokenService();
        var handler = new RefreshTokenCommandHandler(db, jwt);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new RefreshTokenCommand("expired-token"), CancellationToken.None));

        Assert.Equal("Invalid or expired refresh token.", ex.Message);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string Hash(string password) => password;

        public bool Verify(string hash, string password) => hash == password;
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, IEnumerable<string> roles)
            => ($"token-{user.UserName}", DateTime.UtcNow.AddMinutes(15));

        public string GenerateRefreshToken() => "refresh-token";

        public string HashToken(string token) => $"hash-{token}";

        public DateTime GetRefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(7);
    }
}
