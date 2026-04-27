// Helpers/ChessNotationHelper.cs
using System.Text;
using ChessAPI.Models.Chess;
using ChessAPI.Models.Entities;
using ChessAPI.Models.Enums;

namespace ChessAPI.Helpers;

public interface IChessNotationHelper
{
    string GenerateSanMove(ChessBoard board, MoveInfo move);
    string GenerateFen(ChessBoard board);
    string MoveToUci(Position from, Position to, PieceType? promotion = null);
    (Position from, Position to, PieceType? promotion) UciToMove(string uci);
    string FormatTime(int seconds);
    int ParseTimeControl(string timeControl);
    string GeneratePgn(Game game, List<Move> moves);
    List<string> ParsePgn(string pgn);
}

public class ChessNotationHelper : IChessNotationHelper
{
    public string GenerateSanMove(ChessBoard board, MoveInfo move)
    {
        var piece = board.GetPiece(move.From);
        if (piece == null) return move.ToString();

        var san = new StringBuilder();

        if (piece.Type != PieceType.Pawn)
        {
            san.Append(GetPieceLetter(piece.Type));
        }

        if (piece.Type == PieceType.King && Math.Abs(move.To.File - move.From.File) == 2)
        {
            return move.To.File == 6 ? "O-O" : "O-O-O";
        }

        var ambiguousMoves = board.GetLegalMoves()
            .Where(m => m.To.Equals(move.To) && 
                       !m.From.Equals(move.From) &&
                       board.GetPiece(m.From)?.Type == piece.Type)
            .ToList();

        if (ambiguousMoves.Any())
        {
            var sameFile = ambiguousMoves.Any(m => m.From.File == move.From.File);
            var sameRank = ambiguousMoves.Any(m => m.From.Rank == move.From.Rank);

            if (!sameFile)
            {
                san.Append(move.From.ToString()[0]);
            }
            else if (!sameRank)
            {
                san.Append(move.From.ToString()[1]);
            }
            else
            {
                san.Append(move.From.ToString());
            }
        }

        if (board.GetPiece(move.To) != null || 
            (piece.Type == PieceType.Pawn && move.From.File != move.To.File))
        {
            if (piece.Type == PieceType.Pawn)
            {
                san.Append(move.From.ToString()[0]);
            }
            san.Append('x');
        }

        san.Append(move.To.ToString());

        if (move.Promotion.HasValue)
        {
            san.Append('=');
            san.Append(GetPieceLetter(move.Promotion.Value));
        }

        var testBoard = new ChessBoard(board.GetFen());
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

    public string GenerateFen(ChessBoard board)
    {
        return board.GetFen();
    }

    public string MoveToUci(Position from, Position to, PieceType? promotion = null)
    {
        var uci = $"{from}{to}";
        
        if (promotion.HasValue)
        {
            uci += GetPieceLetter(promotion.Value).ToString().ToLower();
        }
        
        return uci;
    }

    public (Position from, Position to, PieceType? promotion) UciToMove(string uci)
    {
        var from = ParseSquare(uci[..2]);
        var to = ParseSquare(uci.Substring(2, 2));
        PieceType? promotion = null;

        if (uci.Length > 4)
        {
            promotion = uci[4] switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => null
            };
        }

        return (from, to, promotion);
    }

    public string FormatTime(int seconds)
    {
        var hours = seconds / 3600;
        var minutes = (seconds % 3600) / 60;
        var secs = seconds % 60;

        if (hours > 0)
        {
            return $"{hours}:{minutes:D2}:{secs:D2}";
        }
        
        return $"{minutes}:{secs:D2}";
    }

    public int ParseTimeControl(string timeControl)
    {
        var parts = timeControl.Split('+');
        var baseTime = ParseTimeToSeconds(parts[0]);
        var increment = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        
        return baseTime;
    }

    public string GeneratePgn(Game game, List<Move> moves)
    {
        var pgn = new StringBuilder();

        pgn.AppendLine($"[Event \"{GetEventName(game.Mode)}\"]");
        pgn.AppendLine($"[Site \"ChessAPI\"]");
        pgn.AppendLine($"[Date \"{game.StartedAt:yyyy.MM.dd}\"]");
        pgn.AppendLine($"[Round \"-\"]");
        pgn.AppendLine($"[White \"{game.WhitePlayer.Username}\"]");
        pgn.AppendLine($"[Black \"{game.BlackPlayer.Username}\"]");
        pgn.AppendLine($"[Result \"{GetResultString(game.Result)}\"]");
        pgn.AppendLine($"[TimeControl \"{game.TimeControl}+{game.Increment}\"]");
        
        if (!string.IsNullOrEmpty(game.TerminationReason))
        {
            pgn.AppendLine($"[Termination \"{game.TerminationReason}\"]");
        }

        pgn.AppendLine();

        var moveNumber = 1;
        for (int i = 0; i < moves.Count; i += 2)
        {
            pgn.Append($"{moveNumber}. {moves[i].San} ");
            
            if (i + 1 < moves.Count)
            {
                pgn.Append($"{moves[i + 1].San} ");
            }
            
            moveNumber++;
        }

        pgn.Append(GetResultString(game.Result));

        return pgn.ToString();
    }

    public List<string> ParsePgn(string pgn)
    {
        var moves = new List<string>();
        var lines = pgn.Split('\n');
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('['))
                continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (!token.Contains('.') && !IsResultToken(token))
                {
                    moves.Add(token);
                }
            }
        }

        return moves;
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

    private static Position ParseSquare(string square)
    {
        var file = square[0] - 'a';
        var rank = 8 - (square[1] - '0');
        return new Position(rank, file);
    }

    private static int ParseTimeToSeconds(string time)
    {
        if (time.Contains(':'))
        {
            var parts = time.Split(':');
            if (parts.Length == 2)
            {
                return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
            }
            if (parts.Length == 3)
            {
                return int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2]);
            }
        }
        
        return int.Parse(time) * 60;
    }

    private static string GetEventName(GameMode mode)
    {
        return mode switch
        {
            GameMode.Casual => "Casual Game",
            GameMode.Ranked => "Ranked Game",
            GameMode.Tournament => "Tournament Game",
            GameMode.AiVsHuman => "vs AI",
            GameMode.FriendChallenge => "Friend Challenge",
            _ => "Chess Game"
        };
    }

    private static string GetResultString(GameResult result)
    {
        return result switch
        {
            GameResult.WhiteWin => "1-0",
            GameResult.BlackWin => "0-1",
            GameResult.Draw => "1/2-1/2",
            GameResult.Stalemate => "1/2-1/2",
            _ => "*"
        };
    }

    private static bool IsResultToken(string token)
    {
        return token == "1-0" || token == "0-1" || token == "1/2-1/2" || token == "*";
    }
}