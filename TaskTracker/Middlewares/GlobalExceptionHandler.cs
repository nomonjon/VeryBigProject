using Microsoft.AspNetCore.Diagnostics;
using TaskTracker.Models;

namespace TaskTracker.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, error) = exception switch
        {
            ArgumentNullException => (400, new ErrorResponse { Code = "BAD_REQUEST", Message = exception.Message, Status = 400 }),
            UnauthorizedAccessException => (401, new ErrorResponse { Code = "UNAUTHORIZED", Message = "Not authorized", Status = 401 }),
            System.Collections.Generic.KeyNotFoundException => (404, new ErrorResponse { Code = "NOT_FOUND", Message = exception.Message, Status = 404 }),
            _ => (500, new ErrorResponse { Code = "INTERNAL_ERROR", Message = "Something went wrong", Status = 500 })
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(error, cancellationToken);

        return true;
    }
}
