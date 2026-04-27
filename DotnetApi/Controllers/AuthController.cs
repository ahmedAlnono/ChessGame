using System.Security.Claims;
using ChessAPI.Models.DTOs;
using ChessAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChessAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IAuthService authService,
    ILogger<AuthController> logger) : ControllerBase
{

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await authService.RegisterAsync(request);

        if (result == null)
        {
            return BadRequest(new { message = "Registration failed. Email or username already exists." });
        }

        logger.LogInformation("User {Username} registered successfully", result.User.Username);

        return Ok(result);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await authService.LoginAsync(request);

        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        logger.LogInformation("User {Username} logged in successfully", result.User.Username);

        return Ok(result);
    }

    /// <summary>
    /// Refresh expired access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New authentication tokens</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await authService.RefreshTokenAsync(request.RefreshToken);

        if (result == null)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Logout the current user
    /// </summary>
    /// <returns>Success message</returns>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var success = await authService.LogoutAsync(userId.Value);

        if (!success)
        {
            return BadRequest(new { message = "Logout failed." });
        }

        logger.LogInformation("User {UserId} logged out", userId);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    /// <param name="request">Password change details</param>
    /// <returns>Success message</returns>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var success = await authService.ChangePasswordAsync(
            userId.Value,
            request.CurrentPassword,
            request.NewPassword);

        if (!success)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        logger.LogInformation("User {UserId} changed password", userId);

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Availability status</returns>
    [HttpGet("check-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckEmailAvailability([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var isAvailable = await authService.IsEmailAvailableAsync(email);

        return Ok(new { email, isAvailable });
    }

    /// <summary>
    /// Check if username is available for registration
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <returns>Availability status</returns>
    [HttpGet("check-username")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckUsernameAvailability([FromQuery] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { message = "Username is required." });
        }

        var isAvailable = await authService.IsUsernameAvailableAsync(username);

        return Ok(new { username, isAvailable });
    }

    /// <summary>
    /// Validate if user is authenticated
    /// </summary>
    /// <returns>Current user information</returns>
    [Authorize]
    [HttpGet("validate")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ValidateToken()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await authService.GetUserByIdAsync(userId.Value);

        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.Status,
            user.Rating,
            user.AvatarUrl
        });
    }

    /// <summary>
    /// Get current authenticated user
    /// </summary>
    /// <returns>Current user information</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await authService.GetUserByIdAsync(userId.Value);

        if (user == null)
        {
            return Unauthorized();
        }

        var userDto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Country = user.Country,
            Bio = user.Bio,
            Rating = user.Rating,
            PeakRating = user.PeakRating,
            GamesPlayed = user.GamesPlayed,
            GamesWon = user.GamesWon,
            GamesLost = user.GamesLost,
            GamesDrawn = user.GamesDrawn,
            Status = user.Status,
            Tier = user.Tier,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsOnline = user.IsOnline,
            WinRate = user.WinRate
        };

        return Ok(userDto);
    }

    /// <summary>
    /// Revoke all refresh tokens for the current user (force logout all devices)
    /// </summary>
    /// <returns>Success message</returns>
    [Authorize]
    [HttpPost("revoke-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllTokens()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await authService.GetUserByIdAsync(userId.Value);

        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await authService.LogoutAsync(userId.Value);
        }

        logger.LogInformation("User {UserId} revoked all tokens", userId);

        return Ok(new { message = "All sessions have been revoked." });
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst("UserId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}