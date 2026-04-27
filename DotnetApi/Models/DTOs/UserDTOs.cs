// Models/DTOs/UserDTOs.cs
using System.ComponentModel.DataAnnotations;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Country { get; set; }
    public string? Bio { get; set; }
    public int Rating { get; set; }
    public int PeakRating { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int GamesDrawn { get; set; }
    public UserStatus Status { get; set; }
    public RatingTier Tier { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsOnline { get; set; }
    public int WinRate { get; set; }
}

public class UserProfileDto : UserDto
{
    public UserStatisticsDto? RecentStats { get; set; }
    public List<UserAchievementDto> Achievements { get; set; } = new();
    public List<GameSummaryDto> RecentGames { get; set; } = new();
}

public class UpdateProfileRequest
{
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }
    
    [StringLength(500)]
    public string? Bio { get; set; }
    
    [StringLength(50)]
    public string? Country { get; set; }
    
    public string? AvatarUrl { get; set; }
}

public class UserStatisticsDto
{
    public DateTime Date { get; set; }
    public int Rating { get; set; }
    public int GamesPlayed { get; set; }
    public int GamesWon { get; set; }
    public int GamesLost { get; set; }
    public int GamesDrawn { get; set; }
    public int BestWinStreak { get; set; }
    public int CurrentWinStreak { get; set; }
}

public class UserAchievementDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int Progress { get; set; }
    public int RequiredProgress { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime UnlockedAt { get; set; }
}

public class LeaderboardEntryDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Rating { get; set; }
    public int Rank { get; set; }
    public int GamesWon { get; set; }
    public int GamesPlayed { get; set; }
    public RatingTier Tier { get; set; }
    public string? Country { get; set; }
}