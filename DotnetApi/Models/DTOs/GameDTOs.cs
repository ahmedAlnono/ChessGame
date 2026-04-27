using System.ComponentModel.DataAnnotations;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.DTOs;

public class CreateGameRequest
{
    [Required]
    public GameMode Mode { get; set; } = GameMode.Casual;
    
    public int TimeControl { get; set; } = 600;
    
    public int Increment { get; set; } = 0;
    
    public bool IsRated { get; set; } = true;
    
    public bool IsPrivate { get; set; } = false;
    
    public Guid? OpponentId { get; set; } // For challenging a specific player
}

public class GameDto
{
    public Guid Id { get; set; }
    public UserDto WhitePlayer { get; set; } = null!;
    public UserDto BlackPlayer { get; set; } = null!;
    public UserDto? Winner { get; set; }
    public GameMode Mode { get; set; }
    public GameStatus Status { get; set; }
    public GameResult Result { get; set; }
    public string CurrentFen { get; set; } = string.Empty;
    public int TimeControl { get; set; }
    public int Increment { get; set; }
    public int WhiteTimeRemaining { get; set; }
    public int BlackTimeRemaining { get; set; }
    public int WhiteRating { get; set; }
    public int BlackRating { get; set; }
    public int? WhiteRatingChange { get; set; }
    public int? BlackRatingChange { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int MoveCount { get; set; }
    public PieceColor CurrentTurn { get; set; }
    public bool IsFinished { get; set; }
}

public class GameSummaryDto
{
    public Guid Id { get; set; }
    public string WhitePlayerName { get; set; } = string.Empty;
    public string BlackPlayerName { get; set; } = string.Empty;
    public string? WinnerName { get; set; }
    public GameResult Result { get; set; }
    public GameMode Mode { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MoveCount { get; set; }
    public string TerminationReason { get; set; } = string.Empty;
}

public class MoveDto
{
    public Guid Id { get; set; }
    public int MoveNumber { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public PieceType Piece { get; set; }
    public PieceColor Color { get; set; }
    public PieceType? PromotionPiece { get; set; }
    public PieceType? CapturedPiece { get; set; }
    public string San { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public MoveType Type { get; set; }
    public bool IsCheck { get; set; }
    public bool IsCheckmate { get; set; }
    public bool IsCastle { get; set; }
    public string? RookFrom { get; set; }
    public string? RookTo { get; set; }
    public int TimeSpent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MakeMoveRequest
{
    [Required]
    public Guid GameId { get; set; }
    
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string From { get; set; } = string.Empty;
    
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string To { get; set; } = string.Empty;
    
    public PieceType? PromotionPiece { get; set; }
}

public class GameStateDto
{
    public Guid GameId { get; set; }
    public string Fen { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public GameResult Result { get; set; }
    public PieceColor CurrentTurn { get; set; }
    public int WhiteTimeRemaining { get; set; }
    public int BlackTimeRemaining { get; set; }
    public bool IsCheck { get; set; }
    public bool IsCheckmate { get; set; }
    public bool IsStalemate { get; set; }
    public bool IsDraw { get; set; }
    public List<string> LegalMoves { get; set; } = new();
    public MoveDto? LastMove { get; set; }
}

public class ResignRequest
{
    [Required]
    public Guid GameId { get; set; }
}

public class OfferDrawRequest
{
    [Required]
    public Guid GameId { get; set; }
}

public class DrawResponse
{
    [Required]
    public Guid GameId { get; set; }
    
    [Required]
    public bool Accept { get; set; }
}