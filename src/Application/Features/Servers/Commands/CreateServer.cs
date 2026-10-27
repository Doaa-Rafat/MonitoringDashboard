using System.Net;
using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Servers.Commands;

public record CreateServerCommand(string Name, string? IpAddress, string? Description) : IRequest<ServerDto>;

public class CreateServerCommandValidator : AbstractValidator<CreateServerCommand>
{
    public CreateServerCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).MaximumLength(50)
            .Must(ip => IPAddress.TryParse(ip, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("IpAddress must be a valid IPv4 or IPv6 address.");
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public class CreateServerCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateServerCommand, ServerDto>
{
    public async Task<ServerDto> Handle(CreateServerCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.Servers.AnyAsync(s => s.Name == name, ct))
            throw new ConflictException($"A server named '{name}' already exists.");

        var server = new Server
        {
            Name = name,
            Ipaddress = string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress.Trim(),
            Description = request.Description?.Trim(),
            Status = ServerStatus.Up
        };
        db.Servers.Add(server);
        await db.SaveChangesAsync(ct);

        return new ServerDto(server.ServerId, server.Name, server.Ipaddress, server.Status,
            server.Description, server.CreatedAt, null);
    }
}