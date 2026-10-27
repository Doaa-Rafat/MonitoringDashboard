using Application.Features.Alerts;
using Application.Features.AlertRules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/alert-rules")]
public class AlertRulesController(ISender mediator) : ControllerBase
{
    public record UpdateAlertRuleRequest(string Operator, double Threshold, bool IsEnabled);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertRuleDto>>> GetAll(
        [FromQuery] int? serverId, CancellationToken ct)
        => Ok(await mediator.Send(new GetAlertRulesQuery(serverId), ct));

    [HttpPost, Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AlertRuleDto>> Create(CreateAlertRuleCommand command, CancellationToken ct)
    {
        var created = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetAll), new { serverId = created.ServerId }, created);
    }

    [HttpPut("{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<AlertRuleDto>> Update(int id, UpdateAlertRuleRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new UpdateAlertRuleCommand(id, body.Operator, body.Threshold, body.IsEnabled), ct));

    [HttpDelete("{id:int}"), Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAlertRuleCommand(id), ct);
        return NoContent();
    }
}