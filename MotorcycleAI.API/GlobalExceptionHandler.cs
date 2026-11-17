using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MotorcycleAI.API;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var (statusCode, title, detail) = exception switch
        {
            ArgumentException or ValidationException =>
                (StatusCodes.Status400BadRequest, "Invalid request", "The request contains invalid values."),
            TimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Upstream timeout", "A required service did not respond in time."),
            HttpRequestException =>
                (StatusCodes.Status503ServiceUnavailable, "Upstream service unavailable", "A required service is unavailable."),
            InvalidDataException =>
                (StatusCodes.Status502BadGateway, "Invalid upstream response", "A required service returned invalid data."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Request {TraceIdentifier} failed with status code {StatusCode}.",
                httpContext.TraceIdentifier,
                statusCode);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request {TraceIdentifier} was rejected with status code {StatusCode}.",
                httpContext.TraceIdentifier,
                statusCode);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
