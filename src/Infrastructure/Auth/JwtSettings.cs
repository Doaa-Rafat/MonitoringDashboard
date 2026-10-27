namespace Infrastructure.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}