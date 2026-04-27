using System.Collections.Concurrent;
using ChessAPI.Data;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;

namespace ChessAPI.Services.Implementations;

public class MatchmakingService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<MatchmakingService> logger) : IMatchmakingService
{
    private static readonly ConcurrentDictionary<Guid, QueueEntry> _queue = new();
    private readonly Dictionary<Guid, MatchFoundDto> _matches = [];

    public async Task<QueueStatusDto> JoinQueueAsync(Guid userId, JoinQueueRequest request)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        if (_queue.ContainsKey(userId))
        {
            return GetQueueStatus(userId);
        }

        var entry = new QueueEntry
        {
            UserId = userId,
            Username = user.Username,
            Rating = user.Rating,
            Mode = request.Mode,
            TimeControl = request.TimeControl,
            Increment = request.Increment,
            IsRated = request.IsRated,
            RatingRange = request.RatingRange ?? 200,
            JoinedAt = DateTime.UtcNow
        };

        _queue[userId] = entry;
        user.Status = UserStatus.InQueue;
        await context.SaveChangesAsync();

        logger.LogInformation("User {Username} joined matchmaking queue for {Mode}", user.Username, request.Mode);

        return new QueueStatusDto
        {
            IsInQueue = true,
            Mode = request.Mode,
            TimeControl = request.TimeControl,
            JoinedAt = entry.JoinedAt,
            EstimatedWaitTime = CalculateEstimatedWaitTime(request.Mode),
            PlayersInQueue = GetQueueEntries(request.Mode).Count
        };
    }

    public Task<bool> LeaveQueueAsync(Guid userId)
    {
        if (_queue.TryRemove(userId, out _))
        {
            Task.Run(async () =>
            {
                using var scope = serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var user = await context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.Status = UserStatus.Online;
                    await context.SaveChangesAsync();
                }
            });

            logger.LogInformation("User {UserId} left matchmaking queue", userId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<MatchFoundDto?> CheckMatchAsync(Guid userId)
    {
        if (_matches.TryGetValue(userId, out var match))
        {
            _matches.Remove(userId);
            return Task.FromResult<MatchFoundDto?>(match);
        }

        return Task.FromResult<MatchFoundDto?>(null);
    }

    public QueueStatusDto GetQueueStatus(Guid userId)
    {
        if (_queue.TryGetValue(userId, out var entry))
        {
            return new QueueStatusDto
            {
                IsInQueue = true,
                Mode = entry.Mode,
                TimeControl = entry.TimeControl,
                JoinedAt = entry.JoinedAt,
                EstimatedWaitTime = CalculateEstimatedWaitTime(entry.Mode),
                PlayersInQueue = GetQueueEntries(entry.Mode).Count
            };
        }

        return new QueueStatusDto { IsInQueue = false };
    }

    public List<QueueEntry> GetQueueEntries(GameMode mode)
    {
        return _queue.Values
            .Where(e => e.Mode == mode)
            .OrderBy(e => e.JoinedAt)
            .ToList();
    }

    public async Task ProcessMatchmakingQueueAsync()
    {
        foreach (var mode in Enum.GetValues<GameMode>())
        {
            var queueEntries = GetQueueEntries(mode);
            
            if (queueEntries.Count < 2) continue;

            for (int i = 0; i < queueEntries.Count - 1; i++)
            {
                for (int j = i + 1; j < queueEntries.Count; j++)
                {
                    var player1 = queueEntries[i];
                    var player2 = queueEntries[j];

                    if (await CanMatchAsync(player1, player2))
                    {
                        var game = await CreateMatchAsync(player1, player2);
                        
                        if (game != null)
                        {
                            _queue.TryRemove(player1.UserId, out _);
                            _queue.TryRemove(player2.UserId, out _);

                            var match1 = new MatchFoundDto
                            {
                                GameId = game.Id,
                                Opponent = await GetUserDtoAsync(player2.UserId),
                                YourColor = PieceColor.White,
                                Game = game
                            };

                            var match2 = new MatchFoundDto
                            {
                                GameId = game.Id,
                                Opponent = await GetUserDtoAsync(player1.UserId),
                                YourColor = PieceColor.Black,
                                Game = game
                            };

                            _matches[player1.UserId] = match1;
                            _matches[player2.UserId] = match2;

                            logger.LogInformation("Match found: {Player1} vs {Player2}", 
                                player1.Username, player2.Username);
                        }
                    }
                }
            }
        }
    }

    public async Task CancelMatchmakingForUserAsync(Guid userId)
    {
        await LeaveQueueAsync(userId);
    }

    private static int CalculateEstimatedWaitTime(GameMode mode)
    {
        var queueSize = _queue.Values.Count(e => e.Mode == mode);
        
        return queueSize switch
        {
            < 2 => 60,
            < 5 => 30,
            < 10 => 15,
            _ => 5
        };
    }

    private async Task<bool> CanMatchAsync(QueueEntry player1, QueueEntry player2)
    {
        if (player1.UserId == player2.UserId) return false;
        if (player1.Mode != player2.Mode) return false;
        if (player1.TimeControl != player2.TimeControl) return false;
        if (player1.Increment != player2.Increment) return false;

        var ratingDiff = Math.Abs(player1.Rating - player2.Rating);
        var maxRatingDiff = Math.Min(player1.RatingRange, player2.RatingRange);
        
        if (ratingDiff > maxRatingDiff) return false;

        var timeInQueue = (DateTime.UtcNow - player1.JoinedAt).TotalSeconds;
        var timeInQueue2 = (DateTime.UtcNow - player2.JoinedAt).TotalSeconds;
        
        if (timeInQueue > 60 || timeInQueue2 > 60)
        {
            maxRatingDiff += 100;
            if (ratingDiff > maxRatingDiff) return false;
        }

        return true;
    }

    private async Task<GameDto?> CreateMatchAsync(QueueEntry player1, QueueEntry player2)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
        
        var request = new CreateGameRequest
        {
            Mode = player1.Mode,
            TimeControl = player1.TimeControl,
            Increment = player1.Increment,
            IsRated = player1.IsRated,
            OpponentId = player2.UserId
        };

        var game = await gameService.CreateGameAsync(player1.UserId, request);
        
        if (game != null)
        {
            await gameService.JoinGameAsync(game.Id, player2.UserId);
        }

        return game;
    }

    private async Task<UserDto?> GetUserDtoAsync(Guid userId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await context.Users.FindAsync(userId);
        
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Rating = user.Rating,
            Status = user.Status,
            Tier = user.Tier
        };
    }
}