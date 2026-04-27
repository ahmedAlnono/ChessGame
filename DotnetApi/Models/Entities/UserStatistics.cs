// Models/Entities/UserStatistics.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChessAPI.Models.Entities;

[Table("UserStatistics")]
public class UserStatistics
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    
    public int Rating { get; set; }
    
    public int GamesPlayed { get; set; }
    
    public int GamesWon { get; set; }
    
    public int GamesLost { get; set; }
    
    public int GamesDrawn { get; set; }
    
    public int TotalMoves { get; set; }
    
    public long TotalTimePlayed { get; set; } // seconds
    
    public int BestWinStreak { get; set; }
    
    public int CurrentWinStreak { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}