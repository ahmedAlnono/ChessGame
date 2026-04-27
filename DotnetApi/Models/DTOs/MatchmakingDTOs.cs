// Models/DTOs/MatchmakingDTOs.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.DTOs;

public class JoinQueueRequest
{
    public GameMode Mode { get; set; } = GameMode.Casual;
    public int TimeControl { get; set; } = 600;
    public int Increment { get; set; } = 0;
    public bool IsRated { get; set; } = true;
    public int? RatingRange { get; set; } = 200;
}

public class QueueStatusDto
{
    public bool IsInQueue { get; set; }
    public GameMode Mode { get; set; }
    public int TimeControl { get; set; }
    public DateTime JoinedAt { get; set; }
    public int EstimatedWaitTime { get; set; } // seconds
    public int PlayersInQueue { get; set; }
}

public class MatchFoundDto
{
    public Guid GameId { get; set; }
    public UserDto Opponent { get; set; } = null!;
    public PieceColor YourColor { get; set; }
    public GameDto Game { get; set; } = null!;
}