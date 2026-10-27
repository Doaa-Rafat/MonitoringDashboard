using Application.Common.Models;
using Application.Features.Metrics;
using Application.Features.Metrics.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/servers/{serverId:int}/metrics")]
public class MetricsController(ISender mediator) : ControllerBase
{
    public record MetricsFilter(DateTime? From, DateTime? To, string SortBy = "timestamp",
        string SortDirection = "desc", int Page = 1, int PageSize = 60);

    [HttpGet]
    public async Task<ActionResult<PagedResult<MetricDto>>> GetHistory(
        int serverId, [FromQuery] MetricsFilter f, CancellationToken ct)
        => Ok(await mediator.Send(new GetServerMetricsQuery(
            serverId, f.From, f.To, f.SortBy, f.SortDirection, f.Page, f.PageSize), ct));
}