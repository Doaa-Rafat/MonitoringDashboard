using Application.Common.Interfaces;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(500);
}

public class RefreshTokenCommandHandler(IApplicationDbContext db, IJwtTokenService jwt)
    : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = jwt.HashToken(request.RefreshToken);

        var user = await db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.RefreshToken == hash, ct);

        if (user is null || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry <= DateTime.UtcNow)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var response = TokenIssuer.Issue(user, jwt);   // rotation: the old token stops working
        await db.SaveChangesAsync(ct);
        return response;
    }
}