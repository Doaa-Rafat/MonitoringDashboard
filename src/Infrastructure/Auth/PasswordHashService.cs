using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Auth;

public class PasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new User(), password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(new User(), hash, password) != PasswordVerificationResult.Failed;
}