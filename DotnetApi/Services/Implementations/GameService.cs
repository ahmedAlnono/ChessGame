using ChessAPI.Data;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services.Implementations;

public class GameService(
    ApplicationDbContext context,
    IChessEngine chessEngine,
    ILogger<GameService> logger) : IGameService
{
    public async Task<GameDto?> CreateGameAsync(Guid playerId, CreateGameRequest request)
    {
        var player = await context.Users.FindAsync(playerId);
        if (player == null) return null;

        var game = new Game
        {
            WhitePlayerId = playerId,
            BlackPlayerId = request.OpponentId ?? Guid.Empty,
            Mode = request.Mode,
            TimeControl = request.TimeControl,
            Increment = request.Increment,
            IsRated = request.IsRated,
            IsPrivate = request.IsPrivate,
            // Status = request.OpponentId.HasValue ? GameStatus.WaitingForOpponent : GameStatus.WaitingForOpponent,
            Status = GameStatus.InProgress,
            WhiteTimeRemaining = request.TimeControl,
            BlackTimeRemaining = request.TimeControl,
            WhiteRating = player.Rating,
            CreatedAt = DateTime.UtcNow,
            CurrentFen = chessEngine.GetInitialFen(),
            InitialFen = chessEngine.GetInitialFen()
        };

        context.Games.Add(game);
        await context.SaveChangesAsync();

        player.Status = UserStatus.InGame;
        await context.SaveChangesAsync();

        return await GetGameByIdAsync(game.Id);
    }

    public async Task<GameDto?> GetGameByIdAsync(Guid gameId)
    {
        var game = await context.Games
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .Include(g => g.Winner)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        return game != null ? MapToGameDto(game) : null;
    }

    public async Task<GameDto?> GetGameWithMovesAsync(Guid gameId)
    {
        var game = await context.Games
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .Include(g => g.Winner)
            .Include(g => g.Moves.OrderBy(m => m.MoveNumber))
            .FirstOrDefaultAsync(g => g.Id == gameId);

        return game != null ? MapToGameDto(game) : null;
    }

    public async Task<List<GameSummaryDto>> GetUserGamesAsync(Guid userId, int page = 1, int pageSize = 20)
    {
        return await context.Games
            .Where(g => g.WhitePlayerId == userId || g.BlackPlayerId == userId)
            .Where(g => g.Status == GameStatus.Completed || g.Status == GameStatus.Draw || g.Status == GameStatus.Resigned)
            .OrderByDescending(g => g.EndedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GameSummaryDto
            {
                Id = g.Id,
                WhitePlayerName = g.WhitePlayer.Username,
                BlackPlayerName = g.BlackPlayer.Username,
                WinnerName = g.Winner != null ? g.Winner.Username : null,
                Result = g.Result,
                Mode = g.Mode,
                CreatedAt = g.CreatedAt,
                MoveCount = g.MoveCount,
                TerminationReason = g.TerminationReason ?? string.Empty
            })
            .ToListAsync();
    }

    public async Task<GameStateDto?> GetGameStateAsync(Guid gameId)
    {
        var game = await context.Games
            .Include(g => g.Moves.OrderByDescending(m => m.MoveNumber).Take(1))
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null) return null;

        var state = chessEngine.GetGameState(gameId, game.CurrentFen);
        state.Status = game.Status;
        state.Result = game.Result;
        state.WhiteTimeRemaining = game.WhiteTimeRemaining;
        state.BlackTimeRemaining = game.BlackTimeRemaining;

        if (game.Moves.Any())
        {
            var lastMove = game.Moves.First();
            state.LastMove = new MoveDto
            {
                Id = lastMove.Id,
                MoveNumber = lastMove.MoveNumber,
                From = lastMove.From,
                To = lastMove.To,
                Piece = lastMove.Piece,
                Color = lastMove.Color,
                San = lastMove.San,
                IsCheck = lastMove.IsCheck,
                IsCheckmate = lastMove.IsCheckmate,
                IsCastle = lastMove.IsCastle
            };
        }

        return state;
    }

    public async Task<MoveResult?> MakeMoveAsync(Guid gameId, Guid playerId, MakeMoveRequest request)
    {
        return await chessEngine.MakeMoveAsync(gameId, playerId, request.From, request.To, request.PromotionPiece);
    }

    public async Task<bool> ResignGameAsync(Guid gameId, Guid playerId)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null || game.Status != GameStatus.InProgress) return false;

        if (playerId != game.WhitePlayerId && playerId != game.BlackPlayerId) return false;

        game.Status = GameStatus.Resigned;
        game.Result = playerId == game.WhitePlayerId ? GameResult.BlackWin : GameResult.WhiteWin;
        game.WinnerId = playerId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;
        game.EndedAt = DateTime.UtcNow;
        game.TerminationReason = "Resignation";

        await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
        await UpdatePlayerAfterGameAsync(game.BlackPlayerId);

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> OfferDrawAsync(Guid gameId, Guid playerId)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null || game.Status != GameStatus.InProgress) return false;

        if (playerId != game.WhitePlayerId && playerId != game.BlackPlayerId) return false;

        return true;
    }

    public async Task<bool> RespondToDrawOfferAsync(Guid gameId, Guid playerId, bool accept)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null || game.Status != GameStatus.InProgress) return false;

        if (playerId != game.WhitePlayerId && playerId != game.BlackPlayerId) return false;

        if (accept)
        {
            game.Status = GameStatus.Draw;
            game.Result = GameResult.Draw;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "Draw by agreement";

            await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
            await UpdatePlayerAfterGameAsync(game.BlackPlayerId);
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AbortGameAsync(Guid gameId, Guid playerId)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null) return false;

        if (game.MoveCount < 2 && (playerId == game.WhitePlayerId || playerId == game.BlackPlayerId))
        {
            game.Status = GameStatus.Abandoned;
            game.EndedAt = DateTime.UtcNow;
            
            await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
            await UpdatePlayerAfterGameAsync(game.BlackPlayerId);
            
            await context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<GameDto?> JoinGameAsync(Guid gameId, Guid playerId)
    {
        var game = await context.Games.FindAsync(gameId);
        var player = await context.Users.FindAsync(playerId);

        if (game == null || player == null) return null;

        if (game.BlackPlayerId == Guid.Empty)
        {
            game.BlackPlayerId = playerId;
            game.BlackRating = player.Rating;
            game.Status = GameStatus.InProgress;
            game.StartedAt = DateTime.UtcNow;

            player.Status = UserStatus.InGame;
            
            await context.SaveChangesAsync();
            return await GetGameByIdAsync(gameId);
        }

        return null;
    }

    public async Task<bool> LeaveGameAsync(Guid gameId, Guid playerId)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null) return false;

        if (game.Status == GameStatus.InProgress)
        {
            game.Status = GameStatus.Abandoned;
            game.Result = playerId == game.WhitePlayerId ? GameResult.BlackWin : GameResult.WhiteWin;
            game.WinnerId = playerId == game.WhitePlayerId ? game.BlackPlayerId : game.WhitePlayerId;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "Abandoned";
        }

        await UpdatePlayerAfterGameAsync(playerId);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<GameDto>> GetActiveGamesAsync()
    {
        return await context.Games
            .Where(g => g.Status == GameStatus.InProgress)
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .Select(g => MapToGameDto(g))
            .ToListAsync();
    }

    public async Task<List<GameDto>> GetUserActiveGamesAsync(Guid userId)
    {
        return await context.Games
            .Where(g => (g.WhitePlayerId == userId || g.BlackPlayerId == userId) && 
                       g.Status == GameStatus.InProgress)
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .Select(g => MapToGameDto(g))
            .ToListAsync();
    }

    public async Task<bool> UpdateGameTimeAsync(Guid gameId, int whiteTime, int blackTime)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null) return false;

        game.WhiteTimeRemaining = whiteTime;
        game.BlackTimeRemaining = blackTime;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<GameResult?> CheckGameTimeoutAsync(Guid gameId)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null || game.Status != GameStatus.InProgress) return null;

        if (game.WhiteTimeRemaining <= 0)
        {
            game.Status = GameStatus.Timeout;
            game.Result = GameResult.BlackWin;
            game.WinnerId = game.BlackPlayerId;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "White timeout";
            
            await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
            await UpdatePlayerAfterGameAsync(game.BlackPlayerId);
            
            await context.SaveChangesAsync();
            return GameResult.BlackWin;
        }

        if (game.BlackTimeRemaining <= 0)
        {
            game.Status = GameStatus.Timeout;
            game.Result = GameResult.WhiteWin;
            game.WinnerId = game.WhitePlayerId;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "Black timeout";
            
            await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
            await UpdatePlayerAfterGameAsync(game.BlackPlayerId);
            
            await context.SaveChangesAsync();
            return GameResult.WhiteWin;
        }

        return null;
    }

    public async Task CleanupAbandonedGamesAsync()
    {
        var timeoutThreshold = DateTime.UtcNow.AddMinutes(-30);
        
        var abandonedGames = await context.Games
            .Where(g => g.Status == GameStatus.InProgress && 
                       g.LastMoveAt < timeoutThreshold)
            .ToListAsync();

        foreach (var game in abandonedGames)
        {
            game.Status = GameStatus.Abandoned;
            game.EndedAt = DateTime.UtcNow;
            game.TerminationReason = "Abandoned due to inactivity";
            
            await UpdatePlayerAfterGameAsync(game.WhitePlayerId);
            await UpdatePlayerAfterGameAsync(game.BlackPlayerId);
        }

        await context.SaveChangesAsync();
    }

    private async Task UpdatePlayerAfterGameAsync(Guid playerId)
    {
        var player = await context.Users.FindAsync(playerId);
        if (player != null)
        {
            player.Status = UserStatus.Online;
        }
    }

    private static GameDto MapToGameDto(Game game)
    {
        return new GameDto
        {
            Id = game.Id,
            WhitePlayer = game.WhitePlayer != null ? MapToUserDto(game.WhitePlayer) : null!,
            BlackPlayer = game.BlackPlayer != null ? MapToUserDto(game.BlackPlayer) : null!,
            Winner = game.Winner != null ? MapToUserDto(game.Winner) : null,
            Mode = game.Mode,
            Status = game.Status,
            Result = game.Result,
            CurrentFen = game.CurrentFen,
            TimeControl = game.TimeControl,
            Increment = game.Increment,
            WhiteTimeRemaining = game.WhiteTimeRemaining,
            BlackTimeRemaining = game.BlackTimeRemaining,
            WhiteRating = game.WhiteRating,
            BlackRating = game.BlackRating,
            WhiteRatingChange = game.WhiteRatingChange,
            BlackRatingChange = game.BlackRatingChange,
            CreatedAt = game.CreatedAt,
            StartedAt = game.StartedAt,
            EndedAt = game.EndedAt,
            MoveCount = game.MoveCount,
            CurrentTurn = game.CurrentTurn,
            IsFinished = game.IsFinished
        };
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Country = user.Country,
            Rating = user.Rating,
            PeakRating = user.PeakRating,
            GamesPlayed = user.GamesPlayed,
            GamesWon = user.GamesWon,
            GamesLost = user.GamesLost,
            GamesDrawn = user.GamesDrawn,
            Status = user.Status,
            Tier = user.Tier,
            IsOnline = user.IsOnline,
            WinRate = user.WinRate
        };
    }
}