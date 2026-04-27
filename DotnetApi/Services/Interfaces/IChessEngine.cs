using ChessAPI.Models.Enums;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;

namespace ChessAPI.Services.Interfaces;

public interface IChessEngine
{
    Task<MoveValidationResult> ValidateMoveAsync(Guid gameId, string from, string to, PieceType? promotionPiece = null);
    Task<MoveResult> MakeMoveAsync(Guid gameId, Guid playerId, string from, string to, PieceType? promotionPiece = null);
    string GetInitialFen();
    bool IsValidFen(string fen);
    List<string> GetLegalMoves(string fen, string square);
    GameStateDto GetGameState(Guid gameId, string fen);
    bool IsCheckmate(string fen);
    bool IsStalemate(string fen);
    bool IsDraw(string fen);
    bool IsCheck(string fen);
    PieceColor GetCurrentTurn(string fen);
    GameResult DetermineGameResult(string fen, Game game);
    string GenerateSanMove(string fen, string from, string to, PieceType? promotion = null);
}

public class MoveValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCheck { get; set; }
    public bool IsCheckmate { get; set; }
    public bool IsStalemate { get; set; }
    public bool IsDraw { get; set; }
    public string? ResultingFen { get; set; }
    public MoveDto? Move { get; set; }
}

public class MoveResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public MoveDto? Move { get; set; }
    public string NewFen { get; set; } = string.Empty;
    public bool IsCheck { get; set; }
    public bool IsCheckmate { get; set; }
    public bool IsStalemate { get; set; }
    public bool IsDraw { get; set; }
    public GameResult? GameResult { get; set; }
}