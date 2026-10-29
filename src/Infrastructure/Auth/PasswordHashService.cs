using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Auth;

public class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new User(), password);

    public bool Verify(string hash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(new User(), hash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Treat malformed or legacy seed hashes as invalid so they can be replaced safely.
            return false;
        }
    }
}
