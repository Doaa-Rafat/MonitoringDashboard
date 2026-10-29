using Application.Common.Interfaces;
using Application.Features.Alerts;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.AlertRules;

public record CreateAlertRuleCommand(int? ServerId, string MetricType, string Operator,
    double Threshold, bool IsEnabled = true) : IRequest<AlertRuleDto>;

public record UpdateAlertRuleCommand(int Id, string Operator, double Threshold, bool IsEnabled)
    : IRequest<AlertRuleDto>;

public record DeleteAlertRuleCommand(int Id) : IRequest;

public record GetAlertRulesQuery(int? ServerId = null) : IRequest<IReadOnlyList<AlertRuleDto>>;

public class CreateAlertRuleCommandValidator : AbstractValidator<CreateAlertRuleCommand>
{
    public CreateAlertRuleCommandValidator()
    {
        RuleFor(x => x.ServerId).GreaterThan(0).When(x => x.ServerId.HasValue);
        RuleFor(x => x.MetricType).Must(t => MetricTypes.All.Contains(t))
            .WithMessage($"MetricType must be one of: {string.Join(", ", MetricTypes.All)}.");
        RuleFor(x => x.Operator).Must(o => AlertOperators.All.Contains(o))
            .WithMessage($"Operator must be one of: {string.Join(", ", AlertOperators.All)}.");
        RuleFor(x => x.Threshold).InclusiveBetween(0, 100)
            .When(x => x.MetricType != MetricTypes.ResponseTime)
            .WithMessage("Threshold for percentage metrics must be between 0 and 100.");
        RuleFor(x => x.Threshold).InclusiveBetween(0, 600_000)
            .When(x => x.MetricType == MetricTypes.ResponseTime)
            .WithMessage("Threshold for ResponseTime is in milliseconds (0 - 600000).");
    }
}

public class UpdateAlertRuleCommandValidator : AbstractValidator<UpdateAlertRuleCommand>
{
    public UpdateAlertRuleCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Operator).Must(o => AlertOperators.All.Contains(o))
            .WithMessage($"Operator must be one of: {string.Join(", ", AlertOperators.All)}.");
        RuleFor(x => x.Threshold).GreaterThanOrEqualTo(0);
    }
}

public class CreateAlertRuleCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateAlertRuleCommand, AlertRuleDto>
{
    public async Task<AlertRuleDto> Handle(CreateAlertRuleCommand request, CancellationToken ct)
    {
        if (request.ServerId is { } sid && !await db.Servers.AnyAsync(s => s.ServerId == sid, ct))
            throw new NotFoundException(nameof(Server), sid);

        var rule = new AlertRule
        {
            ServerId = request.ServerId,
            MetricType = request.MetricType,
            Operator = request.Operator,
            Threshold = request.Threshold,
            IsEnabled = request.IsEnabled
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return await db.AlertRules.AsNoTracking()
            .Where(r => r.AlertRuleId == rule.AlertRuleId)
            .Select(AlertRuleMappings.ToDto)
            .FirstAsync(ct);
    }
}

// Scope (server) and metric type are immutable: changing them would make existing alerts misleading.
// To change scope, create a new rule and disable the old one.
public class UpdateAlertRuleCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateAlertRuleCommand, AlertRuleDto>
{
    public async Task<AlertRuleDto> Handle(UpdateAlertRuleCommand request, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.AlertRuleId == request.Id, ct)
                   ?? throw new NotFoundException(nameof(AlertRule), request.Id);

        if (rule.MetricType != MetricTypes.ResponseTime && request.Threshold > 100)
            throw new ValidationException([new ValidationFailure(nameof(request.Threshold),
                "Threshold for percentage metrics must be between 0 and 100.")]);
        if (rule.MetricType == MetricTypes.ResponseTime && request.Threshold > 600_000)
            throw new ValidationException([new ValidationFailure(nameof(request.Threshold),
                "Threshold for ResponseTime is in milliseconds (0 - 600000).")]);

        rule.Operator = request.Operator;
        rule.Threshold = request.Threshold;
        rule.IsEnabled = request.IsEnabled;
        await db.SaveChangesAsync(ct);

        return await db.AlertRules.AsNoTracking()
            .Where(r => r.AlertRuleId == rule.AlertRuleId)
            .Select(AlertRuleMappings.ToDto)
            .FirstAsync(ct);
    }
}

public class DeleteAlertRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteAlertRuleCommand>
{
    public async Task Handle(DeleteAlertRuleCommand request, CancellationToken ct)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(r => r.AlertRuleId == request.Id, ct)
                   ?? throw new NotFoundException(nameof(AlertRule), request.Id);

        if (await db.Alerts.AnyAsync(a => a.AlertRuleId == request.Id, ct))
            throw new ConflictException("This rule has alert history and cannot be deleted. Disable it instead.");

        db.AlertRules.Remove(rule);
        await db.SaveChangesAsync(ct);
    }
}

public class GetAlertRulesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAlertRulesQuery, IReadOnlyList<AlertRuleDto>>
{
    public async Task<IReadOnlyList<AlertRuleDto>> Handle(GetAlertRulesQuery request, CancellationToken ct)
    {
        var query = db.AlertRules.AsNoTracking().AsQueryable();

        if (request.ServerId is { } sid)   // rules that apply to this server: its own + the global ones
            query = query.Where(r => r.ServerId == sid || r.ServerId == null);

        return await query.OrderBy(r => r.MetricType).ThenBy(r => r.AlertRuleId)
            .Select(AlertRuleMappings.ToDto)
            .ToListAsync(ct);
    }
}
