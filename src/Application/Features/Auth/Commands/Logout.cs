using Application.Common.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands;

public record LogoutCommand : IRequest;

public class LogoutCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("Not authenticated.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await db.SaveChangesAsync(ct);
    }
}