using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Users;

public record ManagedUserDto(int Id, string UserName, string Email, IReadOnlyList<string> Roles, DateTime CreatedAt);

public record GetUsersQuery : IRequest<IReadOnlyList<ManagedUserDto>>;
public record SetUserRolesCommand(int UserId, IReadOnlyList<string> Roles) : IRequest<ManagedUserDto>;

public class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    private static readonly string[] Allowed = [RoleNames.Admin, RoleNames.User];

    public SetUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("A user needs at least one role.");
        RuleForEach(x => x.Roles).Must(r => Allowed.Contains(r)).WithMessage("Unknown role.");
    }
}

public class GetUsersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetUsersQuery, IReadOnlyList<ManagedUserDto>>
{
    public async Task<IReadOnlyList<ManagedUserDto>> Handle(GetUsersQuery request, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new ManagedUserDto(u.UserId, u.UserName, u.Email,
                u.Roles.Select(r => r.Name).ToList(), u.CreatedAt))
            .ToListAsync(ct);
}

public class SetUserRolesCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    : IRequestHandler<SetUserRolesCommand, ManagedUserDto>
{
    public async Task<ManagedUserDto> Handle(SetUserRolesCommand request, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == request.UserId, ct)
                   ?? throw new NotFoundException(nameof(User), request.UserId);

        var names = request.Roles.Distinct().ToList();
        var roles = await db.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);

        // Prevents the last admin from locking themselves out.
        if (current.UserId == user.UserId && roles.All(r => r.Name != RoleNames.Admin))
            throw new ConflictException("You cannot remove your own Admin role.");

        user.Roles.Clear();
        foreach (var role in roles) user.Roles.Add(role);

        // Force a new sign-in once the access token expires, so the new roles take effect.
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;

        await db.SaveChangesAsync(ct);
        return new ManagedUserDto(user.UserId, user.UserName, user.Email,
            roles.Select(r => r.Name).ToList(), user.CreatedAt);
    }
}