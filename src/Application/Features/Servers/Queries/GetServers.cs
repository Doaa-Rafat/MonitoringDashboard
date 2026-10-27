using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;
using Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Servers.Queries;

public record GetServersQuery(
    string? Search = null,
    string? Status = null,
    string SortBy = "name",
    string SortDirection = "asc",
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<ServerDto>>;

public class GetServersQueryValidator : AbstractValidator<GetServersQuery>
{
    private static readonly string[] SortFields = ["name", "status", "createdat", "ipaddress"];

    public GetServersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => ServerStatus.All.Contains(s!))
            .When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Status must be 'Up' or 'Down'.");
        RuleFor(x => x.SortBy).Must(s => SortFields.Contains(s.ToLowerInvariant()))
            .WithMessage($"SortBy must be one of: {string.Join(", ", SortFields)}.");
        RuleFor(x => x.SortDirection).Must(d => d.ToLowerInvariant() is "asc" or "desc")
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}

public class GetServersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetServersQuery, PagedResult<ServerDto>>
{
    public async Task<PagedResult<ServerDto>> Handle(GetServersQuery request, CancellationToken ct)
    {
        IQueryable<Server> query = db.Servers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.Name.Contains(term) || (s.Ipaddress != null && s.Ipaddress.Contains(term)));
        }
        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(s => s.Status == request.Status);

        var total = await query.CountAsync(ct);

        var desc = request.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
        var items = await Sort(query, request.SortBy, desc)
            .ThenBy(s => s.ServerId)                       // stable order, so pages never overlap
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ServerMappings.ToDto)
            .ToListAsync(ct);

        return new PagedResult<ServerDto>(items, request.Page, request.PageSize, total);
    }

    // Whitelisted fields only: never build ORDER BY from raw user input.
    private static IOrderedQueryable<Server> Sort(IQueryable<Server> q, string sortBy, bool desc) =>
        sortBy.ToLowerInvariant() switch
        {
            "status"    => desc ? q.OrderByDescending(s => s.Status)    : q.OrderBy(s => s.Status),
            "createdat" => desc ? q.OrderByDescending(s => s.CreatedAt) : q.OrderBy(s => s.CreatedAt),
            "ipaddress" => desc ? q.OrderByDescending(s => s.Ipaddress) : q.OrderBy(s => s.Ipaddress),
            _           => desc ? q.OrderByDescending(s => s.Name)      : q.OrderBy(s => s.Name)
        };
}