using Application.Features.Auth;
using Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(ISender mediator) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Register(RegisterCommand command, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await mediator.Send(command, ct));

    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenCommand command, CancellationToken ct)
        => Ok(await mediator.Send(command, ct));

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(), ct);
        return NoContent();
    }
}