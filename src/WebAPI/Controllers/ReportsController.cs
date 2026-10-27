using Application.Common.Models;
using Application.Features.Reports;
using Application.Features.Reports.Commands;
using Application.Features.Reports.Queries;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
public class ReportsController(ISender mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ReportDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestReport(RequestReportCommand command, CancellationToken ct)
    {
        var report = await mediator.Send(command, ct);
        return AcceptedAtAction(nameof(GetById), new { id = report.Id }, report);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ReportDto>>> GetAll(
        [FromQuery] GetReportsQuery query, CancellationToken ct)
        => Ok(await mediator.Send(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReportDto>> GetById(int id, CancellationToken ct)
        => Ok(await mediator.Send(new GetReportByIdQuery(id), ct));

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var file = await mediator.Send(new GetReportFileQuery(id), ct);
        return File(file.Content, file.ContentType, file.FileName);   // sent as an attachment, never rendered on our origin
    }
}