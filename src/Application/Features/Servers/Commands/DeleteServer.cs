using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Servers.Commands;

public record DeleteServerCommand(int Id) : IRequest;

public class DeleteServerCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteServerCommand>
{
    public async Task Handle(DeleteServerCommand request, CancellationToken ct)
    {
        var server = await db.Servers.FirstOrDefaultAsync(s => s.ServerId == request.Id, ct)
                     ?? throw new NotFoundException(nameof(Server), request.Id);

        // FKs are RESTRICT: check explicitly so the client gets a clear message.
        var blockers = new List<string>();
        if (await db.Metrics.AnyAsync(m => m.ServerId == request.Id, ct))    blockers.Add("metrics");
        if (await db.Disks.AnyAsync(d => d.ServerId == request.Id, ct))      blockers.Add("disks");
        if (await db.Alerts.AnyAsync(a => a.ServerId == request.Id, ct))     blockers.Add("alerts");
        if (await db.AlertRules.AnyAsync(r => r.ServerId == request.Id, ct)) blockers.Add("alert rules");
        if (await db.Reports.AnyAsync(r => r.ServerId == request.Id, ct))    blockers.Add("reports");

        if (blockers.Count > 0)
            throw new ConflictException(
                $"Server '{server.Name}' cannot be deleted because it has related {string.Join(", ", blockers)}.");

        db.Servers.Remove(server);
        await db.SaveChangesAsync(ct);
    }
}