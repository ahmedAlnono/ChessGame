using System.Diagnostics;

namespace ChessAPI.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses with performance metrics.
/// </summary>
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
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        // Add request ID to response headers for tracking
        context.Response.Headers["X-Request-Id"] = requestId;

        // Log request
        var request = context.Request;
        var userId = context.Items["UserId"]?.ToString() ?? "anonymous";
        var userIp = GetClientIpAddress(context);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["UserId"] = userId,
            ["ClientIP"] = userIp,
            ["Method"] = request.Method,
            ["Path"] = request.Path,
            ["QueryString"] = request.QueryString.ToString()
        }))
        {
            _logger.LogInformation(
                "Request {Method} {Path}{QueryString} from {UserIp} [User: {UserId}]",
                request.Method,
                request.Path,
                request.QueryString,
                userIp,
                userId);

            try
            {
                // Capture response body for logging (optional - can be performance heavy)
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                await _next(context);

                stopwatch.Stop();

                // Log response
                var statusCode = context.Response.StatusCode;
                var logLevel = statusCode >= 500 ? LogLevel.Error :
                               statusCode >= 400 ? LogLevel.Warning :
                               LogLevel.Information;

                _logger.Log(logLevel,
                    "Response {Method} {Path} - {StatusCode} completed in {ElapsedMs}ms [RequestId: {RequestId}]",
                    request.Method,
                    request.Path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds,
                    requestId);

                // Copy response body back to original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "Request {Method} {Path} failed in {ElapsedMs}ms [RequestId: {RequestId}]",
                    request.Method,
                    request.Path,
                    stopwatch.ElapsedMilliseconds,
                    requestId);
                throw;
            }
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP headers (if behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}