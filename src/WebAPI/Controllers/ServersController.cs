using Application.Common.Models;
using Application.Features.Servers;
using Application.Features.Servers.Commands;
using Application.Features.Servers.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/servers")]
public class ServersController(ISender mediator) : ControllerBase
{
    public record UpdateServerRequest(string Name, string? IpAddress, string? Description);

    [HttpGet]
    public async Task<ActionResult<PagedResult<ServerDto>>> GetAll(
        [FromQuery] GetServersQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServerDto>> GetById(int id, CancellationToken ct)
        => Ok(await mediator.Send(new GetServerByIdQuery(id), ct));

    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ServerDto>> Create(CreateServerCommand command, CancellationToken ct)
    {
        var created = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ServerDto>> Update(int id, UpdateServerRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateServerCommand(id, body.Name, body.IpAddress, body.Description), ct));

    [HttpDelete("{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteServerCommand(id), ct);
        return NoContent();
    }
}