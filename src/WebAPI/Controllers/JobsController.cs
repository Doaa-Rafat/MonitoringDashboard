using Application.Common.Models;
using Application.Features.Jobs;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/jobs")]
public class JobsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<JobDto>>> GetJobs(
        [FromQuery] GetJobsQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("counts")]
    public async Task<ActionResult<JobCountsDto>> GetCounts(CancellationToken ct)
        => Ok(await mediator.Send(new GetJobCountsQuery(), ct));

    [HttpGet("recurring")]
    public async Task<ActionResult<IReadOnlyList<RecurringJobSummaryDto>>> GetRecurring(CancellationToken ct)
        => Ok(await mediator.Send(new GetRecurringJobsQuery(), ct));

    [HttpPost("{id}/retry")]
    public async Task<IActionResult> Retry(string id, CancellationToken ct)
    {
        await mediator.Send(new RetryJobCommand(id), ct);
        return Accepted();
    }
}