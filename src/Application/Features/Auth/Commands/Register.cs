using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Auth.Commands;

public record RegisterCommand(string UserName, string Email, string Password) : IRequest<AuthResponse>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MinimumLength(3).MaximumLength(50)
            .Matches("^[a-zA-Z0-9_.-]+$").WithMessage("Only letters, digits, '_', '.' and '-' are allowed.");
        RuleFor(x => x.Email).NotEmpty().MaximumLength(100).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Must contain a digit.");
    }
}

public class RegisterCommandHandler(IApplicationDbContext db, IPasswordHashService hasher, IJwtTokenService jwt)
    : IRequestHandler<RegisterCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.UserName == userName || u.Email == email, ct))
            throw new ConflictException("A user with this username or email already exists.");

        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.User, ct);

        var user = new User
        {
            UserName = userName,
            Email = email,
            PasswordHash = hasher.Hash(request.Password)
        };
        user.Roles.Add(role);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);               // get UserId first (used in the JWT "sub")

        var response = TokenIssuer.Issue(user, jwt);
        await db.SaveChangesAsync(ct);
        return response;
    }
}