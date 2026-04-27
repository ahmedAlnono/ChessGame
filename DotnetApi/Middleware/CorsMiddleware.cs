namespace ChessAPI.Middleware;

public class CorsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorsMiddleware> _logger;
    private readonly string[] _allowedOrigins;

    public CorsMiddleware(
        RequestDelegate next, 
        ILogger<CorsMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers["Origin"].FirstOrDefault();
        
        if (!string.IsNullOrEmpty(origin))
        {
            // Validate origin
            if (IsOriginAllowed(origin))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                
                _logger.LogDebug("CORS allowed for origin: {Origin}", origin);
            }
            else
            {
                _logger.LogWarning("CORS denied for origin: {Origin}", origin);
            }
        }

        // Handle preflight requests
        if (context.Request.Method == "OPTIONS")
        {
            context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, PATCH, OPTIONS");
            context.Response.Headers.Append("Access-Control-Allow-Headers", 
                "Content-Type, Authorization, X-Requested-With, Accept, Origin");
            context.Response.Headers.Append("Access-Control-Max-Age", "86400"); // 24 hours
            
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await _next(context);
    }

    private bool IsOriginAllowed(string origin)
    {
        // Check if origin matches any allowed pattern
        foreach (var allowed in _allowedOrigins)
        {
            if (allowed == "*" || allowed == origin)
            {
                return true;
            }

            // Support wildcard subdomains (e.g., "https://*.example.com")
            if (allowed.StartsWith("*."))
            {
                var domain = allowed[2..];
                if (origin.EndsWith(domain) && origin.Contains($"://"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}