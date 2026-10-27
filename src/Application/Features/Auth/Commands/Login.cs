using Application.Common.Interfaces;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands;

public record LoginCommand(string UserNameOrEmail, string Password) : IRequest<AuthResponse>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.UserNameOrEmail).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}

public class LoginCommandHandler(IApplicationDbContext db, IPasswordHashService hasher, IJwtTokenService jwt)
    : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var id = request.UserNameOrEmail.Trim();
        var email = id.ToLowerInvariant();

        var user = await db.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserName == id || u.Email == email, ct);

        // Same message for "no such user" and "wrong password": don't reveal which accounts exist.
        if (user is null || !hasher.Verify(user.PasswordHash, request.Password))
            throw new UnauthorizedException("Invalid credentials.");

        var response = TokenIssuer.Issue(user, jwt);
        await db.SaveChangesAsync(ct);
        return response;
    }
}