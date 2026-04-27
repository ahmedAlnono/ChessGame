using ChessAPI.Models.DTOs;
using ChessAPI.Models.Enums;

namespace ChessAPI.Services.Interfaces;

public interface IMatchmakingService
{
    Task<QueueStatusDto> JoinQueueAsync(Guid userId, JoinQueueRequest request);
    Task<bool> LeaveQueueAsync(Guid userId);
    Task<MatchFoundDto?> CheckMatchAsync(Guid userId);
    QueueStatusDto GetQueueStatus(Guid userId);
    List<QueueEntry> GetQueueEntries(GameMode mode);
    Task ProcessMatchmakingQueueAsync();
    Task CancelMatchmakingForUserAsync(Guid userId);
}

public class QueueEntry
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Rating { get; set; }
    public GameMode Mode { get; set; }
    public int TimeControl { get; set; }
    public int Increment { get; set; }
    public bool IsRated { get; set; }
    public int RatingRange { get; set; }
    public DateTime JoinedAt { get; set; }
}