using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Servers.Queries;

public record GetServerByIdQuery(int Id) : IRequest<ServerDto>;

public class GetServerByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetServerByIdQuery, ServerDto>
{
    public async Task<ServerDto> Handle(GetServerByIdQuery request, CancellationToken ct) =>
        await db.Servers.AsNoTracking()
            .Where(s => s.ServerId == request.Id)
            .Select(ServerMappings.ToDto)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Server), request.Id);
}