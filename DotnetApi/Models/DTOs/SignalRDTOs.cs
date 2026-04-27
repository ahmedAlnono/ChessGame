// Models/DTOs/SignalRDTOs.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.DTOs;

public class ChessHubMessage
{
    public string Type { get; set; } = string.Empty;
    public object? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GameUpdateMessage
{
    public Guid GameId { get; set; }
    public string UpdateType { get; set; } = string.Empty; // Move, TimeUpdate, GameEnd, etc.
    public object? Data { get; set; }
}

public class TimeUpdateDto
{
    public Guid GameId { get; set; }
    public int WhiteTimeRemaining { get; set; }
    public int BlackTimeRemaining { get; set; }
}

public class PlayerConnectionDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public Guid? CurrentGameId { get; set; }
}

public class ErrorMessageDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}