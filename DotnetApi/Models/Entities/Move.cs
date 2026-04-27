// Models/Entities/Move.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Entities;

[Table("Moves")]
public class Move
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid GameId { get; set; }
    
    [Required]
    public Guid PlayerId { get; set; }
    
    [Required]
    public int MoveNumber { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string From { get; set; } = string.Empty; // e.g., "e2"
    
    [Required]
    [MaxLength(10)]
    public string To { get; set; } = string.Empty; // e.g., "e4"
    
    [Required]
    public PieceType Piece { get; set; }
    
    [Required]
    public PieceColor Color { get; set; }
    
    public PieceType? PromotionPiece { get; set; }
    
    public PieceType? CapturedPiece { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string San { get; set; } = string.Empty; // Standard Algebraic Notation
    
    [MaxLength(100)]
    public string Fen { get; set; } = string.Empty;
    
    public MoveType Type { get; set; } = MoveType.Normal;
    
    public bool IsCheck { get; set; }
    
    public bool IsCheckmate { get; set; }
    
    public bool IsCastle { get; set; }
    
    [MaxLength(10)]
    public string? RookFrom { get; set; }
    
    [MaxLength(10)]
    public string? RookTo { get; set; }
    
    public int TimeSpent { get; set; } // milliseconds
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;
    
    [ForeignKey(nameof(PlayerId))]
    public virtual User Player { get; set; } = null!;
}