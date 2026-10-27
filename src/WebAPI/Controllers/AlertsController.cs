using Application.Common.Models;
using Application.Features.Alerts;
using Application.Features.Alerts.Commands;
using Application.Features.Alerts.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/alerts")]
public class AlertsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AlertDto>>> GetAll(
        [FromQuery] GetAlertsQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpPost("{id:int}/acknowledge")]
    public async Task<ActionResult<AlertDto>> Acknowledge(int id, CancellationToken ct)
        => Ok(await mediator.Send(new AcknowledgeAlertCommand(id), ct));

    [HttpPost("{id:int}/resolve")]
    public async Task<ActionResult<AlertDto>> Resolve(int id, CancellationToken ct)
        => Ok(await mediator.Send(new ResolveAlertCommand(id), ct));
}