using Application.Common.Interfaces;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Alerts.Commands;

public record AcknowledgeAlertCommand(int Id) : IRequest<AlertDto>;
public record ResolveAlertCommand(int Id) : IRequest<AlertDto>;

public class AcknowledgeAlertCommandHandler(
    IApplicationDbContext db, IRealtimeNotifier notifier, ILogger<AcknowledgeAlertCommandHandler> logger)
    : IRequestHandler<AcknowledgeAlertCommand, AlertDto>
{
    public async Task<AlertDto> Handle(AcknowledgeAlertCommand request, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.AlertId == request.Id, ct)
                    ?? throw new NotFoundException(nameof(Alert), request.Id);

        if (alert.Status != AlertStatus.Triggered)
            throw new ConflictException($"Only a triggered alert can be acknowledged (current status: {alert.Status}).");

        alert.Status = AlertStatus.Acknowledged;
        await db.SaveChangesAsync(ct);
        return await AlertUpdates.PublishAsync(db, notifier, logger, alert.AlertId, ct);
    }
}

public class ResolveAlertCommandHandler(
    IApplicationDbContext db, IRealtimeNotifier notifier, ILogger<ResolveAlertCommandHandler> logger)
    : IRequestHandler<ResolveAlertCommand, AlertDto>
{
    public async Task<AlertDto> Handle(ResolveAlertCommand request, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.AlertId == request.Id, ct)
                    ?? throw new NotFoundException(nameof(Alert), request.Id);

        if (alert.Status == AlertStatus.Resolved)
            throw new ConflictException("This alert is already resolved.");

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await AlertUpdates.PublishAsync(db, notifier, logger, alert.AlertId, ct);
    }
}

internal static class AlertUpdates
{
    public static async Task<AlertDto> PublishAsync(IApplicationDbContext db, IRealtimeNotifier notifier,
        ILogger logger, int alertId, CancellationToken ct)
    {
        var dto = await db.Alerts.AsNoTracking()
            .Where(a => a.AlertId == alertId)
            .Select(AlertMappings.Projection)
            .FirstAsync(ct);

        try { await notifier.AlertUpdatedAsync(dto, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Real-time notification failed for alert {AlertId}", alertId);
        }
        return dto;
    }
}