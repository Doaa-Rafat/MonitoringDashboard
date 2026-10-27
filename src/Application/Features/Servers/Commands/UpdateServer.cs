using System.Net;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Servers.Commands;

public record UpdateServerCommand(int Id, string Name, string? IpAddress, string? Description)
    : IRequest<ServerDto>;

public class UpdateServerCommandValidator : AbstractValidator<UpdateServerCommand>
{
    public UpdateServerCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IpAddress).MaximumLength(50)
            .Must(ip => IPAddress.TryParse(ip, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.IpAddress))
            .WithMessage("IpAddress must be a valid IPv4 or IPv6 address.");
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public class UpdateServerCommandHandler(IApplicationDbContext db, IMediator mediator)
    : IRequestHandler<UpdateServerCommand, ServerDto>
{
    public async Task<ServerDto> Handle(UpdateServerCommand request, CancellationToken ct)
    {
        var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerId == request.Id, ct)
                     ?? throw new NotFoundException(nameof(Server), request.Id);

        var name = request.Name.Trim();
        if (await db.Servers.AnyAsync(s => s.Name == name && s.ServerId != request.Id, ct))
            throw new ConflictException($"A server named '{name}' already exists.");

        server.Name = name;
        server.Ipaddress = string.IsNullOrWhiteSpace(request.IpAddress) ? null : request.IpAddress.Trim();
        server.Description = request.Description?.Trim();
        await db.SaveChangesAsync(ct);

        return await mediator.Send(new Queries.GetServerByIdQuery(request.Id), ct);   // returns the fresh DTO with latest metric
    }
}