using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public class DbSeeder(IApplicationDbContext db, IPasswordHashService hasher,
    IConfiguration config, ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await EnsureUserAsync("Seed:Admin", RoleNames.Admin, ct);
        await EnsureUserAsync("Seed:User", RoleNames.User, ct);
    }

    private async Task EnsureUserAsync(string section, string roleName, CancellationToken ct)
    {
        var userName = config[$"{section}:UserName"];
        var email = config[$"{section}:Email"];
        var password = config[$"{section}:Password"];

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed section {Section} is not configured; skipping.", section);
            return;
        }

        if (await db.Users.AnyAsync(u => u.UserName == userName, ct)) return;

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            logger.LogWarning("Role {Role} not found; run db/schema.sql first.", roleName);
            return;
        }

        var user = new User
        {
            UserName = userName,
            Email = email.ToLowerInvariant(),
            PasswordHash = hasher.Hash(password)
        };
        user.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded user {UserName} with role {Role}", userName, roleName);
    }
}