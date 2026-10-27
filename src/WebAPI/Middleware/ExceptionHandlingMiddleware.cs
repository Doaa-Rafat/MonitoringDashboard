using System.Text.Json;
using Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        int status;
        string title;
        string? detail = null;
        Dictionary<string, string[]>? errors = null;

        switch (ex)
        {
            case ValidationException v:
                status = StatusCodes.Status400BadRequest;
                title = "Validation failed";
                errors = v.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                logger.LogWarning("Validation failed: {@Errors}", errors);
                break;
            case NotFoundException:
                status = StatusCodes.Status404NotFound;
                title = "Resource not found";
                detail = ex.Message;
                logger.LogWarning(ex, "Not found");
                break;
            case ConflictException:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = ex.Message;
                logger.LogWarning(ex, "Conflict");
                break;
            case UnauthorizedException:
                status = StatusCodes.Status401Unauthorized;
                title = "Unauthorized";
                detail = ex.Message;
                logger.LogWarning("Unauthorized: {Message}", ex.Message);
                break;
            case DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 547 } }:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = "The operation conflicts with existing data.";
                logger.LogWarning(ex, "Database constraint violation");
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                title = "An unexpected error occurred";   
                logger.LogError(ex, "Unhandled exception");
                break;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is not null) problem.Extensions["errors"] = errors;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, "application/problem+json");
    }
}