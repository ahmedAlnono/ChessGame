// Models/Entities/Game.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Entities;

[Table("Games")]
public class Game
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid WhitePlayerId { get; set; }
    
    [Required]
    public Guid BlackPlayerId { get; set; }
    
    public Guid? WinnerId { get; set; }
    
    [Required]
    public GameMode Mode { get; set; } = GameMode.Casual;
    
    [Required]
    public GameStatus Status { get; set; } = GameStatus.WaitingForOpponent;
    
    public GameResult Result { get; set; } = GameResult.None;
    
    [Required]
    [MaxLength(100)]
    public string InitialFen { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    
    [Required]
    [MaxLength(100)]
    public string CurrentFen { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    
    public int TimeControl { get; set; } = 600; // seconds
    
    public int Increment { get; set; } = 0; // seconds per move
    
    public int WhiteTimeRemaining { get; set; }
    
    public int BlackTimeRemaining { get; set; }
    
    public int WhiteRating { get; set; }
    
    public int BlackRating { get; set; }
    
    public int? WhiteRatingChange { get; set; }
    
    public int? BlackRatingChange { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? EndedAt { get; set; }
    
    public DateTime? LastMoveAt { get; set; }
    
    public bool IsRated { get; set; } = true;
    
    public bool IsPrivate { get; set; } = false;
    
    public string? TournamentId { get; set; }
    
    public int MoveCount { get; set; } = 0;
    
    [MaxLength(100)]
    public string? TerminationReason { get; set; }
    
    // Navigation properties
    [ForeignKey(nameof(WhitePlayerId))]
    public virtual User WhitePlayer { get; set; } = null!;
    
    [ForeignKey(nameof(BlackPlayerId))]
    public virtual User BlackPlayer { get; set; } = null!;
    
    [ForeignKey(nameof(WinnerId))]
    public virtual User? Winner { get; set; }
    
    public virtual ICollection<Move> Moves { get; set; } = new List<Move>();
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    
    [NotMapped]
    public PieceColor CurrentTurn => CurrentFen.Contains(" w ") ? PieceColor.White : PieceColor.Black;
    
    [NotMapped]
    public bool IsFinished => Status == GameStatus.Completed || 
                              Status == GameStatus.Draw || 
                              Status == GameStatus.Resigned || 
                              Status == GameStatus.Timeout ||
                              Status == GameStatus.Abandoned;
    
    [NotMapped]
    public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - (StartedAt ?? CreatedAt);
}
