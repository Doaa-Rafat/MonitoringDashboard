using Application.Common.Interfaces;
using Domain.Entities;

namespace Application.Features.Auth;

public record UserDto(int Id, string UserName, string Email, IReadOnlyList<string> Roles);

public record AuthResponse(string AccessToken, string RefreshToken,
    DateTime AccessTokenExpiresAtUtc, UserDto User);

internal static class TokenIssuer
{
    // Creates the tokens and stores only the HASH of the refresh token on the user.
    // The caller must call SaveChangesAsync afterwards.
    public static AuthResponse Issue(User user, IJwtTokenService jwt)
    {
        var roles = user.Roles.Select(r => r.Name).ToList();
        var (access, accessExpires) = jwt.CreateAccessToken(user, roles);

        var refresh = jwt.GenerateRefreshToken();
        user.RefreshToken = jwt.HashToken(refresh);
        user.RefreshTokenExpiry = jwt.GetRefreshTokenExpiryUtc();

        return new AuthResponse(access, refresh, accessExpires,
            new UserDto(user.UserId, user.UserName, user.Email, roles));
    }
}