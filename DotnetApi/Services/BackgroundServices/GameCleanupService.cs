// Services/BackgroundServices/GameCleanupService.cs
using ChessAPI.Data;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services.BackgroundServices;

public class GameCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<GameCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _gameTimeoutThreshold = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _abandonedGameThreshold = TimeSpan.FromMinutes(10);

    public GameCleanupService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<GameCleanupService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAbandonedGamesAsync();
                await CleanupStuckGamesAsync();
                await CleanupExpiredRefreshTokensAsync();
                await ArchiveCompletedGamesAsync();
                await UpdatePlayerStatusesAsync();
                
                _logger.LogInformation("Game cleanup cycle completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game cleanup cycle");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }

        _logger.LogInformation("Game cleanup service stopped");
    }

    private async Task CleanupAbandonedGamesAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();

        var cutoff = DateTime.UtcNow - _abandonedGameThreshold;
        
        var abandonedGames = await context.Games
            .Where(g => g.Status == GameStatus.InProgress && 
                       g.LastMoveAt < cutoff &&
                       g.CreatedAt < cutoff)
            .ToListAsync();

        foreach (var game in abandonedGames)
        {
            var whiteConnected = connectionManager.IsUserConnected(game.WhitePlayerId);
            var blackConnected = connectionManager.IsUserConnected(game.BlackPlayerId);

            if (!whiteConnected && !blackConnected)
            {
                game.Status = GameStatus.Abandoned;
                game.EndedAt = DateTime.UtcNow;
                game.TerminationReason = "Both players disconnected";
                
                _logger.LogInformation("Game {GameId} abandoned - both players disconnected", game.Id);
            }
            else if (!whiteConnected)
            {
                game.Status = GameStatus.Abandoned;
                game.Result = GameResult.BlackWin;
                game.WinnerId = game.BlackPlayerId;
                game.EndedAt = DateTime.UtcNow;
                game.TerminationReason = "White player disconnected";
                
                _logger.LogInformation("Game {GameId} abandoned - white player disconnected", game.Id);
            }
            else if (!blackConnected)
            {
                game.Status = GameStatus.Abandoned;
                game.Result = GameResult.WhiteWin;
                game.WinnerId = game.WhitePlayerId;
                game.EndedAt = DateTime.UtcNow;
                game.TerminationReason = "Black player disconnected";
                
                _logger.LogInformation("Game {GameId} abandoned - black player disconnected", game.Id);
            }
        }

        await context.SaveChangesAsync();
        
        if (abandonedGames.Any())
        {
            _logger.LogInformation("Cleaned up {Count} abandoned games", abandonedGames.Count);
        }
    }

    private async Task CleanupStuckGamesAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow - _gameTimeoutThreshold;
        
        var stuckGames = await context.Games
            .Where(g => g.Status == GameStatus.InProgress && 
                       g.StartedAt < cutoff &&
                       g.MoveCount == 0)
            .ToListAsync();

        foreach (var game in stuckGames)
        {
            game.Status = GameStatus.Abandoned;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "Game never started";
            
            _logger.LogInformation("Game {GameId} cleaned up - never started", game.Id);
        }

        await context.SaveChangesAsync();
        
        if (stuckGames.Any())
        {
            _logger.LogInformation("Cleaned up {Count} stuck games", stuckGames.Count);
        }
    }

    private async Task CleanupExpiredRefreshTokensAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var users = await context.Users
            .Where(u => u.RefreshTokenExpiryTime < DateTime.UtcNow && 
                       u.RefreshToken != null)
            .ToListAsync();

        foreach (var user in users)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
        }

        await context.SaveChangesAsync();
        
        if (users.Any())
        {
            _logger.LogInformation("Cleaned up refresh tokens for {Count} users", users.Count);
        }
    }

    private async Task ArchiveCompletedGamesAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var archiveThreshold = DateTime.UtcNow.AddDays(-30);
        
        var gamesToArchive = await context.Games
            .Where(g => (g.Status == GameStatus.Completed || 
                        g.Status == GameStatus.Draw ||
                        g.Status == GameStatus.Resigned) &&
                        g.EndedAt < archiveThreshold)
            .Take(100)
            .ToListAsync();

        foreach (var game in gamesToArchive)
        {
            game.IsPrivate = true;
        }

        await context.SaveChangesAsync();
        
        if (gamesToArchive.Any())
        {
            _logger.LogInformation("Archived {Count} completed games", gamesToArchive.Count);
        }
    }

    private async Task UpdatePlayerStatusesAsync()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connectionManager = scope.ServiceProvider.GetRequiredService<IConnectionManager>();

        var users = await context.Users
            .Where(u => u.Status != UserStatus.Offline)
            .ToListAsync();

        foreach (var user in users)
        {
            var isConnected = connectionManager.IsUserConnected(user.Id);
            
            if (!isConnected && user.Status != UserStatus.Offline)
            {
                user.Status = UserStatus.Offline;
                user.LastActiveAt = DateTime.UtcNow;
                
                _logger.LogDebug("Updated status for user {UserId} to Offline", user.Id);
            }
        }

        await context.SaveChangesAsync();
    }
}