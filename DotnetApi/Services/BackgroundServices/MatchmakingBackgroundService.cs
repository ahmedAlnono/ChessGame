// Services/BackgroundServices/MatchmakingBackgroundService.cs
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;

namespace ChessAPI.Services.BackgroundServices;

public class MatchmakingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MatchmakingBackgroundService> _logger;
    private readonly TimeSpan _matchmakingInterval = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _queueTimeoutThreshold = TimeSpan.FromMinutes(5);

    public MatchmakingBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MatchmakingBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Matchmaking background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var matchmakingService = scope.ServiceProvider.GetRequiredService<IMatchmakingService>();
                var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
                var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();

                await ProcessMatchmakingAsync(matchmakingService, gameService, connectionManager);
                await CleanupStaleQueueEntriesAsync(matchmakingService, connectionManager);
                
                LogQueueStatistics(matchmakingService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during matchmaking cycle");
            }

            await Task.Delay(_matchmakingInterval, stoppingToken);
        }

        _logger.LogInformation("Matchmaking background service stopped");
    }

    private async Task ProcessMatchmakingAsync(
        IMatchmakingService matchmakingService,
        IGameService gameService,
        IConnectionManager connectionManager)
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            if (mode == GameMode.AiVsHuman || mode == GameMode.Tournament)
                continue;

            var queueEntries = matchmakingService.GetQueueEntries(mode);
            
            if (queueEntries.Count < 2)
                continue;

            var matched = await FindMatchesAsync(queueEntries, gameService, connectionManager);
            
            if (matched)
            {
                _logger.LogInformation("Created matches for mode {Mode}", mode);
            }
        }
    }

    private async Task<bool> FindMatchesAsync(
        List<QueueEntry> queueEntries,
        IGameService gameService,
        IConnectionManager connectionManager)
    {
        var matched = false;
        var usedPlayers = new HashSet<Guid>();

        for (int i = 0; i < queueEntries.Count; i++)
        {
            var player1 = queueEntries[i];
            
            if (usedPlayers.Contains(player1.UserId))
                continue;
            
            if (!connectionManager.IsUserConnected(player1.UserId))
                continue;

            var bestMatch = await FindBestMatchAsync(player1, queueEntries, i + 1, usedPlayers, connectionManager);
            
            if (bestMatch != null)
            {
                var game = await CreateMatchGameAsync(player1, bestMatch, gameService);
                
                if (game != null)
                {
                    usedPlayers.Add(player1.UserId);
                    usedPlayers.Add(bestMatch.UserId);
                    matched = true;
                    
                    _logger.LogInformation(
                        "Matched {Player1} ({Rating1}) vs {Player2} ({Rating2}) in {Mode}",
                        player1.Username, player1.Rating,
                        bestMatch.Username, bestMatch.Rating,
                        player1.Mode);
                }
            }
        }

        return matched;
    }

    private async Task<QueueEntry?> FindBestMatchAsync(
        QueueEntry player,
        List<QueueEntry> queueEntries,
        int startIndex,
        HashSet<Guid> usedPlayers,
        IConnectionManager connectionManager)
    {
        QueueEntry? bestMatch = null;
        var bestScore = double.MaxValue;
        var timeInQueue = (DateTime.UtcNow - player.JoinedAt).TotalSeconds;

        for (int j = startIndex; j < queueEntries.Count; j++)
        {
            var player2 = queueEntries[j];
            
            if (usedPlayers.Contains(player2.UserId))
                continue;
            
            if (!connectionManager.IsUserConnected(player2.UserId))
                continue;
            
            if (player.TimeControl != player2.TimeControl)
                continue;
            
            if (player.Increment != player2.Increment)
                continue;

            var ratingDiff = Math.Abs(player.Rating - player2.Rating);
            var maxRatingDiff = Math.Min(player.RatingRange, player2.RatingRange);
            
            if (timeInQueue > 30)
            {
                maxRatingDiff += (int)(timeInQueue * 2);
            }

            if (ratingDiff > maxRatingDiff)
                continue;

            var score = CalculateMatchScore(player, player2);
            
            if (score < bestScore)
            {
                bestScore = score;
                bestMatch = player2;
            }

            if (bestScore < 50)
                break;
        }

        return bestMatch;
    }

    private double CalculateMatchScore(QueueEntry player1, QueueEntry player2)
    {
        var ratingDiff = Math.Abs(player1.Rating - player2.Rating);
        var timeDiff = Math.Abs((DateTime.UtcNow - player1.JoinedAt).TotalSeconds - 
                               (DateTime.UtcNow - player2.JoinedAt).TotalSeconds);
        
        var waitTime1 = (DateTime.UtcNow - player1.JoinedAt).TotalSeconds;
        var waitTime2 = (DateTime.UtcNow - player2.JoinedAt).TotalSeconds;
        var maxWaitTime = Math.Max(waitTime1, waitTime2);

        var score = ratingDiff * 0.7 + timeDiff * 0.1 - maxWaitTime * 0.2;
        
        return score;
    }

    private async Task<GameDto?> CreateMatchGameAsync(
        QueueEntry player1,
        QueueEntry player2,
        IGameService gameService)
    {
        try
        {
            var request = new CreateGameRequest
            {
                Mode = player1.Mode,
                TimeControl = player1.TimeControl,
                Increment = player1.Increment,
                IsRated = player1.IsRated && player2.IsRated,
                OpponentId = player2.UserId
            };

            var game = await gameService.CreateGameAsync(player1.UserId, request);
            
            if (game != null)
            {
                await gameService.JoinGameAsync(game.Id, player2.UserId);
            }

            return game;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create match game between {Player1} and {Player2}", 
                player1.UserId, player2.UserId);
            return null;
        }
    }

    private async Task CleanupStaleQueueEntriesAsync(
        IMatchmakingService matchmakingService,
        IConnectionManager connectionManager)
    {
        var queueEntries = matchmakingService.GetQueueEntries(GameMode.Casual)
            .Concat(matchmakingService.GetQueueEntries(GameMode.Ranked))
            .Concat(matchmakingService.GetQueueEntries(GameMode.FriendChallenge))
            .ToList();

        foreach (var entry in queueEntries)
        {
            var timeInQueue = DateTime.UtcNow - entry.JoinedAt;
            
            if (timeInQueue > _queueTimeoutThreshold)
            {
                await matchmakingService.CancelMatchmakingForUserAsync(entry.UserId);
                _logger.LogInformation("Removed user {UserId} from queue due to timeout", entry.UserId);
                continue;
            }

            if (!connectionManager.IsUserConnected(entry.UserId))
            {
                await matchmakingService.CancelMatchmakingForUserAsync(entry.UserId);
                _logger.LogInformation("Removed disconnected user {UserId} from queue", entry.UserId);
                continue;
            }

            var userGame = connectionManager.GetUserGame(entry.UserId);
            if (userGame.HasValue)
            {
                await matchmakingService.CancelMatchmakingForUserAsync(entry.UserId);
                _logger.LogInformation("Removed user {UserId} from queue - already in game", entry.UserId);
            }
        }
    }

    private void LogQueueStatistics(IMatchmakingService matchmakingService)
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            var queueSize = matchmakingService.GetQueueEntries(mode).Count;
            
            if (queueSize > 0)
            {
                _logger.LogDebug("Queue status - Mode: {Mode}, Size: {Size}", mode, queueSize);
            }
        }
    }
}