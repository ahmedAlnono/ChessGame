using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChessAPI.Data;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ChessAPI.Services.Implementations;

public class AuthService(
    ApplicationDbContext context,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{


    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await context.Users.AnyAsync(u => u.Email == request.Email))
        {
            logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
            return null;
        }

        if (await context.Users.AnyAsync(u => u.Username == request.Username))
        {
            logger.LogWarning("Registration failed: Username {Username} already exists", request.Username);
            return null;
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Rating = 1200,
            PeakRating = 1200,
            Role = "User",
            Status = UserStatus.Online,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        logger.LogInformation("User {Username} registered successfully", user.Username);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed for email: {Email}", request.Email);
            return null;
        }

        if (user.IsBanned)
        {
            if (user.BannedUntil.HasValue && user.BannedUntil > DateTime.UtcNow)
            {
                logger.LogWarning("Banned user {Username} attempted to login", user.Username);
                return null;
            }
            
            user.IsBanned = false;
            user.BannedUntil = null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.Status = UserStatus.Online;
        await context.SaveChangesAsync();

        logger.LogInformation("User {Username} logged in successfully", user.Username);

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && 
                                     u.RefreshTokenExpiryTime > DateTime.UtcNow);

        if (user == null)
        {
            return null;
        }

        return await GenerateAuthResponseAsync(user);
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null) return false;

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.Status = UserStatus.Offline;
        user.ConnectionId = null;
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await context.Users.FindAsync(userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ValidateCredentialsAsync(string email, string password)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    public async Task<bool> IsEmailAvailableAsync(string email)
    {
        return !await context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        return !await context.Users.AnyAsync(u => u.Username == username);
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(User user)
    {
        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = MapToUserDto(user)
        };
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("Username", user.Username),
            new Claim("Role", user.Role),
            new Claim("UserId", user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
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
    }
}