using System.Drawing;
using System.Text;
using ChessAPI.Data;
using ChessAPI.Models.Chess;
using ChessAPI.Models.DTOs;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;
using ChessAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChessAPI.Services.Implementations;

public class ChessEngine(ApplicationDbContext context, ILogger<ChessEngine> logger) : IChessEngine
{

    public async Task<MoveValidationResult> ValidateMoveAsync(Guid gameId, string from, string to, PieceType? promotionPiece = null)
    {
        var game = await context.Games.FindAsync(gameId);
        if (game == null)
        {
            return new MoveValidationResult { IsValid = false, ErrorMessage = "Game not found" };
        }

        var chess = new ChessBoard();
        chess.LoadFen(game.CurrentFen);

        var move = GenerateMoveString(from, to, promotionPiece);

        try
        {
            if (!chess.IsValidMove(move))
            {
                return new MoveValidationResult { IsValid = false, ErrorMessage = "Invalid move" };
            }

            chess.Move(move);

            return new MoveValidationResult
            {
                IsValid = true,
                IsCheck = chess.IsCheck(),
                IsCheckmate = chess.IsCheckmate(),
                IsStalemate = chess.IsStalemate(),
                IsDraw = chess.IsDraw(),
                ResultingFen = chess.GetFen()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating move for game {GameId}", gameId);
            return new MoveValidationResult { IsValid = false, ErrorMessage = "Invalid move format" };
        }
    }

    public async Task<MoveResult> MakeMoveAsync(Guid gameId, Guid playerId, string from, string to, PieceType? promotionPiece = null)
    {
        var game = await context.Games
            .Include(g => g.WhitePlayer)
            .Include(g => g.BlackPlayer)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        if (game == null)
        {
            return new MoveResult { Success = false, ErrorMessage = "Game not found" };
        }

        if (game.Status != GameStatus.InProgress)
        {
            return new MoveResult { Success = false, ErrorMessage = "Game is not in progress" };
        }

        var isWhiteTurn = game.CurrentFen.Contains(" w ");
        if ((isWhiteTurn && playerId != game.WhitePlayerId) ||
            (!isWhiteTurn && playerId != game.BlackPlayerId))
        {
            return new MoveResult { Success = false, ErrorMessage = "Not your turn" };
        }

        var chess = new ChessBoard();
        chess.LoadFen(game.CurrentFen);

        var piece = chess.GetPiece(ParseSquare(from));
        if (piece == null)
        {
            return new MoveResult { Success = false, ErrorMessage = "No piece at source square" };
        }

        var move = GenerateMoveString(from, to, promotionPiece);

        try
        {
            if (!chess.IsValidMove(move))
            {
                return new MoveResult { Success = false, ErrorMessage = "Invalid move" };
            }

            var capturedPiece = chess.GetPiece(ParseSquare(to));
            var isCastle = piece.Type == PieceType.King && Math.Abs(to[0] - from[0]) == 2;

            string? rookFrom = null;
            string? rookTo = null;

            if (isCastle)
            {
                if (to == "g1") { rookFrom = "h1"; rookTo = "f1"; }
                else if (to == "c1") { rookFrom = "a1"; rookTo = "d1"; }
                else if (to == "g8") { rookFrom = "h8"; rookTo = "f8"; }
                else if (to == "c8") { rookFrom = "a8"; rookTo = "d8"; }
            }

            chess.Move(move);

            var moveType = DetermineMoveType(piece.Type, capturedPiece != null, isCastle, chess.IsCheck(), chess.IsCheckmate());
            var san = GenerateSanMove(game.CurrentFen, from, to, promotionPiece);

            var moveEntity = new Move
            {
                GameId = gameId,
                PlayerId = playerId,
                MoveNumber = game.MoveCount + 1,
                From = from,
                To = to,
                Piece = MapPieceType(piece.Type),
                Color = piece.Color == Models.Chess.Color.White ? PieceColor.White : PieceColor.Black,
                PromotionPiece = promotionPiece,
                CapturedPiece = capturedPiece != null ? MapPieceType(capturedPiece.Type) : null,
                San = san,
                Fen = chess.GetFen(),
                Type = moveType,
                IsCheck = chess.IsCheck(),
                IsCheckmate = chess.IsCheckmate(),
                IsCastle = isCastle,
                RookFrom = rookFrom,
                RookTo = rookTo,
                CreatedAt = DateTime.UtcNow
            };

            context.Moves.AddAsync(moveEntity);

            game.CurrentFen = chess.GetFen();
            game.MoveCount++;
            game.LastMoveAt = DateTime.UtcNow;

            var gameResult = DetermineGameResult(game.CurrentFen, game);

            if (gameResult != GameResult.None)
            {
                game.Status = GameStatus.Completed;
                game.Result = gameResult;
                game.EndedAt = DateTime.UtcNow;
                game.WinnerId = DetermineWinner(gameResult, game);

                await UpdatePlayerRatingsAsync(game);
                await UpdatePlayerStatisticsAsync(game);
            }
            await context.SaveChangesAsync();

            var moveDto = MapToMoveDto(moveEntity);

            return new MoveResult
            {
                Success = true,
                Move = moveDto,
                NewFen = game.CurrentFen,
                IsCheck = chess.IsCheck(),
                IsCheckmate = chess.IsCheckmate(),
                IsStalemate = chess.IsStalemate(),
                IsDraw = chess.IsDraw(),
                GameResult = gameResult != GameResult.None ? gameResult : null
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error making move for game {GameId}", gameId);
            return new MoveResult { Success = false, ErrorMessage = "Error processing move" };
        }
    }

    public string GetInitialFen()
    {
        return "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    }

    public bool IsValidFen(string fen)
    {
        try
        {
            var chess = new ChessBoard();
            chess.LoadFen(fen);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetLegalMoves(string fen, string square)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);

        var moves = chess.GetLegalMoves();
        return moves.Where(m => m.From.ToString() == square)
                    .Select(m => m.To.ToString())
                    .ToList();
    }

    public GameStateDto GetGameState(Guid gameId, string fen)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);

        return new GameStateDto
        {
            GameId = gameId,
            Fen = fen,
            CurrentTurn = chess.Turn == Models.Chess.Color.White ? PieceColor.White : PieceColor.Black,
            IsCheck = chess.IsCheck(),
            IsCheckmate = chess.IsCheckmate(),
            IsStalemate = chess.IsStalemate(),
            IsDraw = chess.IsDraw(),
            LegalMoves = chess.GetLegalMoves().Select(m => m.ToString().Substring(2, 2)).Distinct().ToList()
        };
    }

    public bool IsCheckmate(string fen)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);
        return chess.IsCheckmate();
    }

    public bool IsStalemate(string fen)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);
        return chess.IsStalemate();
    }

    public bool IsDraw(string fen)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);
        return chess.IsDraw();
    }

    public bool IsCheck(string fen)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);
        return chess.IsCheck();
    }

    public PieceColor GetCurrentTurn(string fen)
    {
        return fen.Contains(" w ") ? PieceColor.White : PieceColor.Black;
    }

    public GameResult DetermineGameResult(string fen, Game game)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);

        if (chess.IsCheckmate())
        {
            return chess.Turn == Models.Chess.Color.White ? GameResult.BlackWin : GameResult.WhiteWin;
        }

        if (chess.IsStalemate())
        {
            return GameResult.Stalemate;
        }

        if (chess.IsDraw())
        {
            if (chess.IsThreefoldRepetition())
                return GameResult.ThreefoldRepetition;
            if (chess.IsFiftyMoveRule())
                return GameResult.FiftyMoveRule;
            if (chess.IsInsufficientMaterial())
                return GameResult.InsufficientMaterial;

            return GameResult.Draw;
        }

        return GameResult.None;
    }

    public string GenerateSanMove(string fen, string from, string to, PieceType? promotion = null)
    {
        var chess = new ChessBoard();
        chess.LoadFen(fen);

        var fromPos = ParseSquare(from);
        var toPos = ParseSquare(to);

        var moves = chess.GetLegalMoves();
        var matchedMove = moves.FirstOrDefault(m =>
            m.From.Equals(fromPos) &&
            m.To.Equals(toPos) &&
            m.Promotion == promotion);

        if (matchedMove != null)
        {
            // Generate SAN notation
            return GenerateSanFromMove(chess, matchedMove);
        }

        return $"{from}-{to}";
    }

    private string GenerateSanFromMove(ChessBoard chess, MoveInfo move)
    {
        var piece = chess.GetPiece(move.From);
        if (piece == null) return move.ToString();

        var san = new StringBuilder();

        // Add piece letter (except for pawns)
        if (piece.Type != PieceType.Pawn)
        {
            san.Append(GetPieceLetter(piece.Type));
        }

        // Handle castling
        if (piece.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2)
        {
            return move.To.File == 6 ? "O-O" : "O-O-O";
        }

        // Check for ambiguity
        var ambiguousMoves = chess.GetLegalMoves()
            .Where(m => m.To.Equals(move.To) &&
                       !m.From.Equals(move.From) &&
                       chess.GetPiece(m.From)?.Type == piece.Type)
            .ToList();

        if (ambiguousMoves.Any())
        {
            var sameFile = ambiguousMoves.Any(m => m.From.File == move.From.File);
            var sameRank = ambiguousMoves.Any(m => m.From.Rank == move.From.Rank);

            if (!sameFile)
            {
                san.Append(move.From.ToString()[0]); // Add file letter
            }
            else if (!sameRank)
            {
                san.Append(move.From.ToString()[1]); // Add rank number
            }
            else
            {
                san.Append(move.From.ToString()); // Add full square
            }
        }
        else if (piece.Type == PieceType.Pawn && move.From.File != move.To.File)
        {
            san.Append(move.From.ToString()[0]);
        }

        if (chess.GetPiece(move.To) != null ||
            (piece.Type == PieceType.Pawn && move.From.File != move.To.File))
        {
            if (piece.Type == PieceType.Pawn)
            {
                san.Append(move.From.ToString()[0]);
            }
            san.Append('x');
        }

        // Add destination square
        san.Append(move.To.ToString());

        // Add promotion
        if (move.Promotion.HasValue)
        {
            san.Append('=');
            san.Append(GetPieceLetter(move.Promotion.Value));
        }

        // Check for check/checkmate
        var testBoard = new ChessBoard(chess.GetFen());
        testBoard.Move(move.From, move.To, move.Promotion);

        if (testBoard.IsCheckmate())
        {
            san.Append('#');
        }
        else if (testBoard.IsCheck())
        {
            san.Append('+');
        }

        return san.ToString();
    }

    private static char GetPieceLetter(PieceType type)
    {
        return type switch
        {
            PieceType.King => 'K',
            PieceType.Queen => 'Q',
            PieceType.Rook => 'R',
            PieceType.Bishop => 'B',
            PieceType.Knight => 'N',
            PieceType.Pawn => 'P',
            _ => throw new ArgumentException($"Invalid piece type: {type}")
        };
    }

    private static string GenerateMoveString(string from, string to, PieceType? promotion)
    {
        var move = $"{from}{to}";

        if (promotion.HasValue)
        {
            var promotionChar = promotion.Value switch
            {
                PieceType.Queen => 'q',
                PieceType.Rook => 'r',
                PieceType.Bishop => 'b',
                PieceType.Knight => 'n',
                _ => 'q'
            };
            move += promotionChar;
        }

        return move;
    }

    private static Position ParseSquare(string square)
    {
        var file = square[0] - 'a';
        var rank = 8 - (square[1] - '0');
        return new Position(rank, file);
    }

    private static PieceType MapPieceType(PieceType type) => type;

    private static MoveType DetermineMoveType(PieceType piece, bool isCapture, bool isCastle, bool isCheck, bool isCheckmate)
    {
        if (isCheckmate) return MoveType.Checkmate;
        if (isCheck) return MoveType.Check;
        if (isCastle) return MoveType.Castle;
        if (isCapture) return MoveType.Capture;
        return MoveType.Normal;
    }

    private static Guid? DetermineWinner(GameResult result, Game game)
    {
        return result switch
        {
            GameResult.WhiteWin => game.WhitePlayerId,
            GameResult.BlackWin => game.BlackPlayerId,
            _ => null
        };
    }

    private async Task UpdatePlayerRatingsAsync(Game game)
    {
        if (!game.IsRated) return;

        var whitePlayer = await context.Users.FindAsync(game.WhitePlayerId);
        var blackPlayer = await context.Users.FindAsync(game.BlackPlayerId);

        if (whitePlayer == null || blackPlayer == null) return;

        var (whiteChange, blackChange) = CalculateRatingChange(
            whitePlayer.Rating,
            blackPlayer.Rating,
            game.Result);

        whitePlayer.Rating += whiteChange;
        blackPlayer.Rating += blackChange;

        if (whitePlayer.Rating > whitePlayer.PeakRating)
            whitePlayer.PeakRating = whitePlayer.Rating;

        if (blackPlayer.Rating > blackPlayer.PeakRating)
            blackPlayer.PeakRating = blackPlayer.Rating;

        whitePlayer.Tier = DetermineRatingTier(whitePlayer.Rating);
        blackPlayer.Tier = DetermineRatingTier(blackPlayer.Rating);

        game.WhiteRatingChange = whiteChange;
        game.BlackRatingChange = blackChange;
    }

    private static (int whiteChange, int blackChange) CalculateRatingChange(
        int whiteRating,
        int blackRating,
        GameResult result)
    {
        const int K = 32;

        var expectedWhite = 1.0 / (1.0 + Math.Pow(10, (blackRating - whiteRating) / 400.0));
        var expectedBlack = 1.0 - expectedWhite;

        double actualWhite = result switch
        {
            GameResult.WhiteWin => 1.0,
            GameResult.BlackWin => 0.0,
            _ => 0.5
        };
        double actualBlack = 1.0 - actualWhite;

        var whiteChange = (int)Math.Round(K * (actualWhite - expectedWhite));
        var blackChange = (int)Math.Round(K * (actualBlack - expectedBlack));

        return (whiteChange, blackChange);
    }

    private static RatingTier DetermineRatingTier(int rating)
    {
        return rating switch
        {
            < 1200 => RatingTier.Bronze,
            < 1400 => RatingTier.Silver,
            < 1600 => RatingTier.Gold,
            < 1800 => RatingTier.Platinum,
            < 2000 => RatingTier.Diamond,
            < 2200 => RatingTier.Master,
            _ => RatingTier.Grandmaster
        };
    }

    private async Task UpdatePlayerStatisticsAsync(Game game)
    {
        var whitePlayer = await context.Users.FindAsync(game.WhitePlayerId);
        var blackPlayer = await context.Users.FindAsync(game.BlackPlayerId);

        if (whitePlayer == null || blackPlayer == null) return;

        whitePlayer.GamesPlayed++;
        blackPlayer.GamesPlayed++;

        switch (game.Result)
        {
            case GameResult.WhiteWin:
                whitePlayer.GamesWon++;
                blackPlayer.GamesLost++;
                break;
            case GameResult.BlackWin:
                blackPlayer.GamesWon++;
                whitePlayer.GamesLost++;
                break;
            default:
                whitePlayer.GamesDrawn++;
                blackPlayer.GamesDrawn++;
                break;
        }
    }

    private static MoveDto MapToMoveDto(Move move)
    {
        return new MoveDto
        {
            Id = move.Id,
            MoveNumber = move.MoveNumber,
            From = move.From,
            To = move.To,
            Piece = move.Piece,
            Color = move.Color,
            PromotionPiece = move.PromotionPiece,
            CapturedPiece = move.CapturedPiece,
            San = move.San,
            Fen = move.Fen,
            Type = move.Type,
            IsCheck = move.IsCheck,
            IsCheckmate = move.IsCheckmate,
            IsCastle = move.IsCastle,
            RookFrom = move.RookFrom,
            RookTo = move.RookTo,
            TimeSpent = move.TimeSpent,
            CreatedAt = move.CreatedAt
        };
    }
}