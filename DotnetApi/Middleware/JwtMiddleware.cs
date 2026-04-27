// Middleware/JwtMiddleware.cs
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ChessAPI.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace ChessAPI.Middleware;

/// <summary>
/// Middleware that validates JWT tokens and attaches the user to the HttpContext.
/// </summary>
public class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtMiddleware> _logger;
    
    // Paths that don't require JWT validation
    private static readonly string[] _publicPaths = new[]
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/auth/refresh",
        "/api/auth/check-email",
        "/api/auth/check-username",
        "/health",
        "/swagger",
        "/hubs"  // SignalR handles its own auth
    };

    public JwtMiddleware(
        RequestDelegate next, 
        IConfiguration configuration,
        ILogger<JwtMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        // Skip JWT validation for public endpoints
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var token = ExtractTokenFromHeader(context);

        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var userId = ValidateTokenAndGetUserId(token);
                
                if (userId.HasValue)
                {
                    // Attach user to context on successful jwt validation
                    var user = await authService.GetUserByIdAsync(userId.Value);
                    
                    if (user != null && user.IsActive && !user.IsBanned)
                    {
                        context.Items["User"] = user;
                        context.Items["UserId"] = user.Id;
                        
                        // Add claims to the context user for authorization
                        var claims = new[]
                        {
                            new System.Security.Claims.Claim("UserId", user.Id.ToString()),
                            new System.Security.Claims.Claim("Username", user.Username),
                            new System.Security.Claims.Claim("Email", user.Email),
                            new System.Security.Claims.Claim("Role", user.Role)
                        };
                        
                        var identity = new System.Security.Claims.ClaimsIdentity(claims, "jwt");
                        context.User = new System.Security.Claims.ClaimsPrincipal(identity);
                        
                        _logger.LogDebug("JWT validated for user: {UserId}", userId);
                    }
                }
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("Expired JWT token received for path: {Path}", context.Request.Path);
                context.Items["TokenExpired"] = true;
            }
            catch (SecurityTokenMalformedException ex)
            {
                _logger.LogWarning(ex, "Malformed JWT token received for path: {Path}", context.Request.Path);
                // Don't throw - just don't attach user to context
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid JWT token received for path: {Path}", context.Request.Path);
                // Don't throw - just don't attach user to context
            }
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        // Check if path starts with any of the public paths
        foreach (var publicPath in _publicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        // Also allow OPTIONS requests (CORS preflight)
        return false;
    }

    private static string? ExtractTokenFromHeader(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            // Try to get token from query string (for SignalR)
            var token = context.Request.Query["access_token"].FirstOrDefault();
            return token;
        }

        return authHeader["Bearer ".Length..].Trim();
    }

    private Guid? ValidateTokenAndGetUserId(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var key = Encoding.UTF8.GetBytes(secretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
        var userIdClaim = principal.FindFirst("UserId") ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}