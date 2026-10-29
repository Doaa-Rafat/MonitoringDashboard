using Application.Features.Users;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "AdminOnly")]
[Route("api/v{version:apiVersion}/users")]
public class UsersController(ISender mediator) : ControllerBase
{
    public record SetRolesRequest(IReadOnlyList<string> Roles);

    /// <summary>Lists all users with their roles (admin only).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserDto>>> GetAll(CancellationToken ct)
        => Ok(await mediator.Send(new GetUsersQuery(), ct));

    /// <summary>Replaces a user's roles. The user must sign in again for the change to apply.</summary>
    [HttpPut("{id:int}/roles")]
    public async Task<ActionResult<ManagedUserDto>> SetRoles(int id, SetRolesRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new SetUserRolesCommand(id, body.Roles), ct));
}