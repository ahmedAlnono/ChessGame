using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;

namespace ChessAPI.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(Guid userId);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<bool> ValidateCredentialsAsync(string email, string password);
    Task<bool> IsEmailAvailableAsync(string email);
    Task<bool> IsUsernameAvailableAsync(string username);
}