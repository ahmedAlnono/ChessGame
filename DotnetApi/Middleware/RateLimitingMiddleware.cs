using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace ChessAPI.Middleware;

/// <summary>
/// Custom rate limiting middleware to prevent API abuse.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitInfo> _clients = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow;

    public RateLimitingMiddleware(
        RequestDelegate next, 
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _maxRequests = configuration.GetValue<int>("RateLimiting:MaxRequests", 100);
        _timeWindow = TimeSpan.FromSeconds(configuration.GetValue<int>("RateLimiting:TimeWindowSeconds", 60));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for SignalR hubs and health checks
        if (context.Request.Path.StartsWithSegments("/hubs") ||
            context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var rateLimitInfo = _clients.AddOrUpdate(clientId,
            _ => new RateLimitInfo { RequestCount = 1, WindowStart = DateTime.UtcNow },
            (_, info) =>
            {
                var now = DateTime.UtcNow;
                if (now - info.WindowStart > _timeWindow)
                {
                    // Reset window
                    return new RateLimitInfo { RequestCount = 1, WindowStart = now };
                }
                
                info.RequestCount++;
                return info;
            });

        var timeRemaining = _timeWindow - (DateTime.UtcNow - rateLimitInfo.WindowStart);

        if (rateLimitInfo.RequestCount > _maxRequests)
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);
            
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = ((int)timeRemaining.TotalSeconds).ToString();
            context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)rateLimitInfo.WindowStart.Add(_timeWindow)).ToUnixTimeSeconds().ToString();
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
            {
                error = "Rate limit exceeded",
                message = $"You have exceeded the {_maxRequests} requests in {_timeWindow.TotalSeconds} seconds limit.",
                retryAfter = (int)timeRemaining.TotalSeconds
            }));
            
            return;
        }

        // Add rate limit headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = (_maxRequests - rateLimitInfo.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)rateLimitInfo.WindowStart.Add(_timeWindow)).ToUnixTimeSeconds().ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try to get user ID from context items (set by JwtMiddleware)
        if (context.Items.TryGetValue("UserId", out var userId) && userId is Guid id)
        {
            return $"user:{id}";
        }

        // Fallback to IP address
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Check for forwarded IP (if behind proxy)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            ip = forwardedFor.Split(',')[0].Trim();
        }

        return $"ip:{ip}";
    }

    private class RateLimitInfo
    {
        public int RequestCount { get; set; }
        public DateTime WindowStart { get; set; }
    }
}