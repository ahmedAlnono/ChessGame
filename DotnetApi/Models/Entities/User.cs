using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Entities{
    
}

[Table("Users")]
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
    
    [MaxLength(50)]
    public string? Country { get; set; }
    
    [MaxLength(500)]
    public string? Bio { get; set; }
    
    [Required]
    public string Role { get; set; } = "User"; // Admin, User, Moderator
    
    [Required]
    public int Rating { get; set; } = 1200;
    
    public int PeakRating { get; set; } = 1200;
    
    public int GamesPlayed { get; set; } = 0;
    
    public int GamesWon { get; set; } = 0;
    
    public int GamesLost { get; set; } = 0;
    
    public int GamesDrawn { get; set; } = 0;
    
    public UserStatus Status { get; set; } = UserStatus.Offline;
    
    public RatingTier Tier { get; set; } = RatingTier.Bronze;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginAt { get; set; }
    
    public DateTime? LastActiveAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public bool IsBanned { get; set; } = false;
    
    public DateTime? BannedUntil { get; set; }
    
    public string? BanReason { get; set; }
    
    public string? RefreshToken { get; set; }
    
    public DateTime? RefreshTokenExpiryTime { get; set; }
    
    public string? ConnectionId { get; set; }
    
    // Navigation properties
    public virtual ICollection<Game> WhiteGames { get; set; } = new List<Game>();
    public virtual ICollection<Game> BlackGames { get; set; } = new List<Game>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<Friend> Friends { get; set; } = new List<Friend>();
    public virtual ICollection<Friend> FriendOf { get; set; } = new List<Friend>();
    public virtual ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    public virtual ICollection<UserStatistics> Statistics { get; set; } = new List<UserStatistics>();
    
    [NotMapped]
    public int WinRate => GamesPlayed > 0 ? (int)((GamesWon / (double)GamesPlayed) * 100) : 0;
    
    [NotMapped]
    public bool IsOnline => Status != UserStatus.Offline && LastActiveAt > DateTime.UtcNow.AddMinutes(-5);
}