// Models/Entities/UserAchievement.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models.Entities;

[Table("UserAchievements")]
public class UserAchievement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Guid AchievementId { get; set; }
    
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    
    public int Progress { get; set; } = 0;
    
    public bool IsCompleted { get; set; } = false;
    
    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
    
    [ForeignKey(nameof(AchievementId))]
    public virtual Achievement Achievement { get; set; } = null!;
}

[Table("Achievements")]
public class Achievement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? IconUrl { get; set; }
    
    public int RequiredProgress { get; set; }
    
    public int Points { get; set; }
    
    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}