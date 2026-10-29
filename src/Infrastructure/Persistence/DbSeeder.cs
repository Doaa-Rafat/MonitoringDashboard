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
        await EnsureRolesAsync(ct);
        await EnsureUserAsync("Seed:Admin", RoleNames.Admin, ct);
        await EnsureUserAsync("Seed:User", RoleNames.User, ct);
    }

    private async Task EnsureRolesAsync(CancellationToken ct)
    {
        var roleNames = new[] { RoleNames.Admin, RoleNames.User };
        var existing = await db.Roles.Where(r => roleNames.Contains(r.Name)).Select(r => r.Name).ToListAsync(ct);
        var missing = roleNames.Except(existing).ToArray();
        foreach (var name in missing)
            db.Roles.Add(new Role
            {
                Name = name,
                Description = name == RoleNames.Admin ? "Full access" : "Standard dashboard access"
            });

        if (missing.Length > 0)
            await db.SaveChangesAsync(ct);
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

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            logger.LogWarning("Role {Role} could not be initialized.", roleName);
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (user is null)
        {
            if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
                throw new InvalidOperationException($"The configured email for {section} is already used by another account.");

            user = new User
            {
                UserName = userName,
                Email = normalizedEmail,
                PasswordHash = hasher.Hash(password)
            };
            user.Roles.Add(role);
            db.Users.Add(user);
        }
        else
        {
            if (await db.Users.AnyAsync(u => u.UserId != user.UserId && u.Email == normalizedEmail, ct))
                throw new InvalidOperationException($"The configured email for {section} is already used by another account.");

            user.Email = normalizedEmail;
            if (!hasher.Verify(user.PasswordHash, password))
            {
                user.PasswordHash = hasher.Hash(password);
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
            }
            if (user.Roles.All(r => r.RoleId != role.RoleId)) user.Roles.Add(role);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded user {UserName} with role {Role}", userName, roleName);
    }
}
