using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SecureProxyChatClients.Server.Security;

/// <summary>
/// Global exception handler that returns ProblemDetails and never leaks internal details.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var (statusCode, title) = exception switch
        {
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled"),
            TimeoutException => (StatusCodes.Status504GatewayTimeout, "Request timed out"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Access denied"),
            _ => (StatusCodes.Status500InternalServerError, "An internal error occurred"),
        };

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{statusCode}",
        };

        // Include detail in development for debugging (never in production)
        var env = httpContext.RequestServices?.GetService<IWebHostEnvironment>();
        if (env is not null && env.IsDevelopment())
        {
            problemDetails.Detail = exception.ToString();
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
