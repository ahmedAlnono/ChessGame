using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;

namespace ChessAPI.Services.Interfaces;

public interface IGameService
{
    Task<GameDto?> CreateGameAsync(Guid playerId, CreateGameRequest request);
    Task<GameDto?> GetGameByIdAsync(Guid gameId);
    Task<GameDto?> GetGameWithMovesAsync(Guid gameId);
    Task<List<GameSummaryDto>> GetUserGamesAsync(Guid userId, int page = 1, int pageSize = 20);
    Task<GameStateDto?> GetGameStateAsync(Guid gameId);
    Task<MoveResult?> MakeMoveAsync(Guid gameId, Guid playerId, MakeMoveRequest request);
    Task<bool> ResignGameAsync(Guid gameId, Guid playerId);
    Task<bool> OfferDrawAsync(Guid gameId, Guid playerId);
    Task<bool> RespondToDrawOfferAsync(Guid gameId, Guid playerId, bool accept);
    Task<bool> AbortGameAsync(Guid gameId, Guid playerId);
    Task<GameDto?> JoinGameAsync(Guid gameId, Guid playerId);
    Task<bool> LeaveGameAsync(Guid gameId, Guid playerId);
    Task<List<GameDto>> GetActiveGamesAsync();
    Task<List<GameDto>> GetUserActiveGamesAsync(Guid userId);
    Task<bool> UpdateGameTimeAsync(Guid gameId, int whiteTime, int blackTime);
    Task<GameResult?> CheckGameTimeoutAsync(Guid gameId);
    Task CleanupAbandonedGamesAsync();
}