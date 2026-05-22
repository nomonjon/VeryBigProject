using System.Diagnostics;

namespace TaskTracker.Middlewares;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Incoming request: {Method} {Path} {QueryString}", 
        request.Method, 
        request.Path, 
        request.QueryString);

        try
        {
            await _next(context);
            stopwatch.Stop();

            var level = context.Response.StatusCode >= 500 ? LogLevel.Error
                    : context.Response.StatusCode >= 400 ? LogLevel.Warning
                    : LogLevel.Information;

            _logger.LogInformation("Completed {Method} {Path} => {StatusCode} in {ElapsedMs}ms",
                request.Method,
                request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Failed {Method} {Path} => threw {ExceptionType} in {ElapsedMs}ms:",
                request.Method,
                request.Path,
                ex.GetType().Name,
                stopwatch.ElapsedMilliseconds);
                
            throw;
        }
    }
}