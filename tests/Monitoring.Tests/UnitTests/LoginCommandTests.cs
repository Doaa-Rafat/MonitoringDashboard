using Application.Common.Interfaces;
using Application.Features.Auth.Commands;
using Domain.Entities;
using Domain.Exceptions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Monitoring.Tests.UnitTests;

public class LoginCommandTests
{
    [Fact]
    public async Task Handle_WhenUserNameMatches_ReturnsAuthResponse()
    {
        await using var db = CreateDb();
        var role = new Role { Name = "User" };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(new User
        {
            UserId = 1,
            UserName = "alice",
            Email = "alice@example.com",
            PasswordHash = "valid-hash",
            Roles = { role }
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db,
            new FakePasswordHashService("valid-hash", "Password123"),
            new FakeJwtTokenService());

        var result = await handler.Handle(new LoginCommand("alice", "Password123"), CancellationToken.None);

        Assert.Equal("alice", result.User.UserName);
        Assert.Equal("alice@example.com", result.User.Email);
        Assert.Equal("token-alice", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.NotNull(result.User.Roles);
    }

    [Fact]
    public async Task Handle_WhenEmailMatches_ReturnsAuthResponse()
    {
        await using var db = CreateDb();
        var role = new Role { Name = "User" };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(new User
        {
            UserId = 2,
            UserName = "bob",
            Email = "bob@example.com",
            PasswordHash = "valid-hash",
            Roles = { role }
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db,
            new FakePasswordHashService("valid-hash", "Password123"),
            new FakeJwtTokenService());

        var result = await handler.Handle(new LoginCommand("bob@example.com", "Password123"), CancellationToken.None);

        Assert.Equal("bob", result.User.UserName);
        Assert.Equal("bob@example.com", result.User.Email);
        Assert.Equal("token-bob", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ThrowsUnauthorizedException()
    {
        await using var db = CreateDb();
        var handler = new LoginCommandHandler(db, new FakePasswordHashService("valid-hash", "Password123"), new FakeJwtTokenService());

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("missing-user", "Password123"), CancellationToken.None));

        Assert.Equal("Invalid credentials.", ex.Message);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsIncorrect_ThrowsUnauthorizedException()
    {
        await using var db = CreateDb();
        var role = new Role { Name = "User" };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(new User
        {
            UserId = 3,
            UserName = "carol",
            Email = "carol@example.com",
            PasswordHash = "valid-hash",
            Roles = { role }
        });
        await db.SaveChangesAsync();

        var handler = new LoginCommandHandler(
            db,
            new FakePasswordHashService("valid-hash", "Password123"),
            new FakeJwtTokenService());

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginCommand("carol", "WrongPassword!"), CancellationToken.None));

        Assert.Equal("Invalid credentials.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_ShouldRejectEmptyUserNameOrEmail(string userNameOrEmail)
    {
        var validator = new LoginCommandValidator();

        var result = validator.Validate(new LoginCommand(userNameOrEmail, "Password123"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.UserNameOrEmail));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_ShouldRejectEmptyPassword(string password)
    {
        var validator = new LoginCommandValidator();

        var result = validator.Validate(new LoginCommand("alice", password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }

    [Theory]
    [InlineData(101)]
    public void Validator_ShouldRejectValuesLongerThan100Characters(int length)
    {
        var validator = new LoginCommandValidator();
        var value = new string('x', length);

        var result = validator.Validate(new LoginCommand(value, value));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.UserNameOrEmail));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginCommand.Password));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakePasswordHashService(string expectedHash, string validPassword) : IPasswordHashService
    {
        public string Hash(string password) => expectedHash;

        public bool Verify(string hash, string password) => hash == expectedHash && password == validPassword;
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, IEnumerable<string> roles)
            => ($"token-{user.UserName}", DateTime.UtcNow.AddMinutes(15));

        public string GenerateRefreshToken() => "refresh-token";

        public string HashToken(string token) => $"hashed-{token}";

        public DateTime GetRefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(7);
    }
}
