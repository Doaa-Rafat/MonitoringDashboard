using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    string HashToken(string token);
    DateTime GetRefreshTokenExpiryUtc();
}

public interface IPasswordHashService
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ICurrentUserService
{
    int? UserId { get; }
    string? UserName { get; }
}