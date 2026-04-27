// Models/Chess/ChessBoard.cs
using System.Text;
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Chess;

public class ChessBoard
{
    private Piece?[,] _board = new Piece?[8, 8];
    private Color _turn = Color.White;
    private Dictionary<Color, bool> _castlingRights = new()
    {
        { Color.White, true },
        { Color.Black, true }
    };
    private Position? _enPassantTarget;
    private int _halfMoveClock;
    private int _fullMoveNumber = 1;
    private List<string> _positionHistory = new();
    private Dictionary<string, int> _positionCount = new();

    public ChessBoard()
    {
        InitializeBoard();
    }

    public ChessBoard(string fen)
    {
        LoadFen(fen);
    }

    public Color Turn => _turn;
    public Piece?[,] Board => _board;
    public Position? EnPassantTarget => _enPassantTarget;
    public Dictionary<Color, bool> CastlingRights => _castlingRights;
    public int HalfMoveClock => _halfMoveClock;
    public int FullMoveNumber => _fullMoveNumber;

    private void InitializeBoard()
    {
        LoadFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
    }

    public void LoadFen(string fen)
    {
        var parts = fen.Split(' ');
        if (parts.Length < 4) throw new ArgumentException("Invalid FEN string");

        _board = new Piece?[8, 8];
        var ranks = parts[0].Split('/');

        for (int rank = 0; rank < 8; rank++)
        {
            var file = 0;
            foreach (var ch in ranks[rank])
            {
                if (char.IsDigit(ch))
                {
                    file += int.Parse(ch.ToString());
                }
                else
                {
                    var color = char.IsUpper(ch) ? Color.White : Color.Black;
                    var type = char.ToLower(ch) switch
                    {
                        'p' => PieceType.Pawn,
                        'n' => PieceType.Knight,
                        'b' => PieceType.Bishop,
                        'r' => PieceType.Rook,
                        'q' => PieceType.Queen,
                        'k' => PieceType.King,
                        _ => throw new ArgumentException($"Invalid piece character: {ch}")
                    };
                    _board[rank, file] = new Piece(color, type);
                    file++;
                }
            }
        }

        _turn = parts[1] == "w" ? Color.White : Color.Black;

        _castlingRights[Color.White] = parts[2].Contains('K') || parts[2].Contains('Q');
        _castlingRights[Color.Black] = parts[2].Contains('k') || parts[2].Contains('q');

        if (parts.Length > 3 && parts[3] != "-")
        {
            _enPassantTarget = ParseSquare(parts[3]);
        }
        else
        {
            _enPassantTarget = null;
        }

        _halfMoveClock = parts.Length > 4 ? int.Parse(parts[4]) : 0;
        _fullMoveNumber = parts.Length > 5 ? int.Parse(parts[5]) : 1;

        UpdatePositionHistory();
    }

    public string GetFen()
    {
        var fen = new StringBuilder();

        for (int rank = 0; rank < 8; rank++)
        {
            var emptyCount = 0;
            for (int file = 0; file < 8; file++)
            {
                var piece = _board[rank, file];
                if (piece == null)
                {
                    emptyCount++;
                }
                else
                {
                    if (emptyCount > 0)
                    {
                        fen.Append(emptyCount);
                        emptyCount = 0;
                    }
                    fen.Append(piece.ToString());
                }
            }
            if (emptyCount > 0)
            {
                fen.Append(emptyCount);
            }
            if (rank < 7) fen.Append('/');
        }

        fen.Append(' ');
        fen.Append(_turn == Color.White ? 'w' : 'b');
        fen.Append(' ');

        var castling = "";
        if (_castlingRights[Color.White])
        {
            if (CanCastleKingside(Color.White)) castling += "K";
            if (CanCastleQueenside(Color.White)) castling += "Q";
        }
        if (_castlingRights[Color.Black])
        {
            if (CanCastleKingside(Color.Black)) castling += "k";
            if (CanCastleQueenside(Color.Black)) castling += "q";
        }
        fen.Append(string.IsNullOrEmpty(castling) ? "-" : castling);

        fen.Append(' ');
        fen.Append(_enPassantTarget != null ? _enPassantTarget.ToString() : "-");
        fen.Append(' ');
        fen.Append(_halfMoveClock);
        fen.Append(' ');
        fen.Append(_fullMoveNumber);

        return fen.ToString();
    }

    public Piece? GetPiece(Position pos)
    {
        if (!IsValidPosition(pos)) return null;
        return _board[pos.Rank, pos.File];
    }

    public Piece? GetPiece(string square)
    {
        return GetPiece(ParseSquare(square));
    }

    public bool IsValidMove(string move)
    {
        if (move.Length < 4) return false;

        var from = ParseSquare(move.Substring(0, 2));
        var to = ParseSquare(move.Substring(2, 2));
        var promotion = move.Length > 4 ? CharToPieceType(move[4]) : (PieceType?)null;

        return IsValidMove(from, to, promotion);
    }

    public bool IsValidMove(Position from, Position to, PieceType? promotion = null)
    {
        var piece = GetPiece(from);
        if (piece == null || piece.Color != _turn) return false;

        var legalMoves = GetLegalMovesFrom(from);
        return legalMoves.Any(m => m.To.Equals(to) &&
                                   (!promotion.HasValue || m.Promotion == promotion));
    }

    public bool Move(string move)
    {
        if (!IsValidMove(move)) return false;

        var from = ParseSquare(move.Substring(0, 2));
        var to = ParseSquare(move.Substring(2, 2));
        var promotion = move.Length > 4 ? CharToPieceType(move[4]) : (PieceType?)null;

        return Move(from, to, promotion);
    }

    public bool Move(Position from, Position to, PieceType? promotion = null)
    {
        var piece = GetPiece(from);
        if (piece == null) return false;

        var capturedPiece = GetPiece(to);
        var isCapture = capturedPiece != null;
        var isPawnMove = piece.Type == PieceType.Pawn;
        var isCastle = piece.Type == PieceType.King && Math.Abs(to.File - from.File) == 2;

        _board[to.Rank, to.File] = _board[from.Rank, from.File];
        _board[from.Rank, from.File] = null;

        if (isCastle)
        {
            HandleCastling(from, to);
        }

        if (isPawnMove && to.Equals(_enPassantTarget))
        {
            var captureRank = piece.Color == Color.White ? to.Rank + 1 : to.Rank - 1;
            _board[captureRank, to.File] = null;
        }

        if (isPawnMove && (to.Rank == 0 || to.Rank == 7))
        {
            var promoteTo = promotion ?? PieceType.Queen;
            _board[to.Rank, to.File] = new Piece(piece.Color, promoteTo);
        }

        _enPassantTarget = null;
        if (isPawnMove && Math.Abs(to.Rank - from.Rank) == 2)
        {
            var enPassantRank = (from.Rank + to.Rank) / 2;
            _enPassantTarget = new Position(enPassantRank, from.File);
        }

        UpdateCastlingRights(from, to);

        _halfMoveClock = (isPawnMove || isCapture) ? 0 : _halfMoveClock + 1;

        if (_turn == Color.Black)
        {
            _fullMoveNumber++;
        }

        _turn = _turn == Color.White ? Color.Black : Color.White;

        UpdatePositionHistory();

        return true;
    }

    public List<MoveInfo> GetLegalMoves()
    {
        var moves = new List<MoveInfo>();

        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var piece = _board[rank, file];
                if (piece != null && piece.Color == _turn)
                {
                    moves.AddRange(GetLegalMovesFrom(new Position(rank, file)));
                }
            }
        }

        return moves;
    }

    public List<MoveInfo> GetLegalMovesFrom(Position from)
    {
        var moves = new List<MoveInfo>();
        var piece = GetPiece(from);
        if (piece == null || piece.Color != _turn) return moves;

        var possibleMoves = piece.Type switch
        {
            PieceType.Pawn => GetPawnMoves(from),
            PieceType.Knight => GetKnightMoves(from),
            PieceType.Bishop => GetBishopMoves(from),
            PieceType.Rook => GetRookMoves(from),
            PieceType.Queen => GetQueenMoves(from),
            PieceType.King => GetKingMoves(from),
            _ => new List<Position>()
        };

        foreach (var to in possibleMoves)
        {
            if (IsLegalMove(from, to))
            {
                if (piece.Type == PieceType.Pawn && (to.Rank == 0 || to.Rank == 7))
                {
                    foreach (var promo in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
                    {
                        moves.Add(new MoveInfo(from, to, promo));
                    }
                }
                else
                {
                    moves.Add(new MoveInfo(from, to));
                }
            }
        }

        return moves;
    }

    public bool IsCheck()
    {
        var kingPos = FindKing(_turn);
        return IsSquareAttacked(kingPos, _turn == Color.White ? Color.Black : Color.White);
    }

    public bool IsCheckmate()
    {
        if (!IsCheck()) return false;
        return GetLegalMoves().Count == 0;
    }

    public bool IsStalemate()
    {
        if (IsCheck()) return false;
        return GetLegalMoves().Count == 0;
    }

    public bool IsDraw()
    {
        return IsStalemate() ||
               IsThreefoldRepetition() ||
               IsFiftyMoveRule() ||
               IsInsufficientMaterial();
    }

    public bool IsThreefoldRepetition()
    {
        return _positionCount.Values.Any(count => count >= 3);
    }

    public bool IsFiftyMoveRule()
    {
        return _halfMoveClock >= 100;
    }

    public bool IsInsufficientMaterial()
    {
        var pieces = new List<Piece>();
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var piece = _board[rank, file];
                if (piece != null) pieces.Add(piece);
            }
        }

        if (pieces.Count == 2) return true;

        if (pieces.Count == 3)
        {
            return pieces.Any(p => p.Type == PieceType.Bishop || p.Type == PieceType.Knight);
        }

        return false;
    }

    private bool IsSquareAttacked(Position pos, Color attackerColor)
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var piece = _board[rank, file];
                if (piece != null && piece.Color == attackerColor)
                {
                    var from = new Position(rank, file);
                    var moves = piece.Type switch
                    {
                        PieceType.Pawn => GetPawnAttacks(from, piece.Color),
                        PieceType.Knight => GetKnightMoves(from),
                        PieceType.Bishop => GetBishopMoves(from),
                        PieceType.Rook => GetRookMoves(from),
                        PieceType.Queen => GetQueenMoves(from),
                        PieceType.King => GetKingMoves(from),
                        _ => new List<Position>()
                    };

                    if (moves.Any(m => m.Equals(pos))) return true;
                }
            }
        }
        return false;
    }

    private bool IsLegalMove(Position from, Position to)
    {
        var piece = GetPiece(from);
        if (piece == null) return false;

        var originalBoard = (Piece?[,])_board.Clone();
        var originalTurn = _turn;
        var originalEnPassant = _enPassantTarget;
        var originalCastling = new Dictionary<Color, bool>(_castlingRights);

        Move(from, to);

        var kingPos = FindKing(originalTurn);
        var isLegal = !IsSquareAttacked(kingPos, originalTurn == Color.White ? Color.Black : Color.White);

        _board = originalBoard;
        _turn = originalTurn;
        _enPassantTarget = originalEnPassant;
        _castlingRights = originalCastling;

        return isLegal;
    }

    private Position FindKing(Color color)
    {
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                var piece = _board[rank, file];
                if (piece != null && piece.Color == color && piece.Type == PieceType.King)
                {
                    return new Position(rank, file);
                }
            }
        }
        throw new InvalidOperationException("King not found on board");
    }

    private void HandleCastling(Position from, Position to)
    {
        if (to.File == 6)
        {
            _board[to.Rank, 5] = _board[to.Rank, 7];
            _board[to.Rank, 7] = null;
        }
        else if (to.File == 2)
        {
            _board[to.Rank, 3] = _board[to.Rank, 0];
            _board[to.Rank, 0] = null;
        }
    }

    private void UpdateCastlingRights(Position from, Position to)
    {
        var piece = GetPiece(to);

        if (piece?.Type == PieceType.King)
        {
            _castlingRights[piece.Color] = false;
        }

        if (piece?.Type == PieceType.Rook)
        {
            if (from.Rank == (piece.Color == Color.White ? 7 : 0))
            {
                if (from.File == 0 || from.File == 7)
                {
                    _castlingRights[piece.Color] = false;
                }
            }
        }

        var capturedPiece = GetPiece(to);
        if (capturedPiece?.Type == PieceType.Rook)
        {
            if (to.Rank == (capturedPiece.Color == Color.White ? 7 : 0))
            {
                if (to.File == 0 || to.File == 7)
                {
                    _castlingRights[capturedPiece.Color] = false;
                }
            }
        }
    }

    private bool CanCastleKingside(Color color)
    {
        var rank = color == Color.White ? 7 : 0;
        return _board[rank, 7]?.Type == PieceType.Rook &&
               _board[rank, 7]?.Color == color &&
               _board[rank, 5] == null &&
               _board[rank, 6] == null &&
               !IsSquareAttacked(new Position(rank, 4), color == Color.White ? Color.Black : Color.White) &&
               !IsSquareAttacked(new Position(rank, 5), color == Color.White ? Color.Black : Color.White) &&
               !IsSquareAttacked(new Position(rank, 6), color == Color.White ? Color.Black : Color.White);
    }

    private bool CanCastleQueenside(Color color)
    {
        var rank = color == Color.White ? 7 : 0;
        return _board[rank, 0]?.Type == PieceType.Rook &&
               _board[rank, 0]?.Color == color &&
               _board[rank, 1] == null &&
               _board[rank, 2] == null &&
               _board[rank, 3] == null &&
               !IsSquareAttacked(new Position(rank, 4), color == Color.White ? Color.Black : Color.White) &&
               !IsSquareAttacked(new Position(rank, 3), color == Color.White ? Color.Black : Color.White) &&
               !IsSquareAttacked(new Position(rank, 2), color == Color.White ? Color.Black : Color.White);
    }

    private void UpdatePositionHistory()
    {
        var fen = GetFen().Split(' ').Take(4).Aggregate((a, b) => $"{a} {b}");
        _positionHistory.Add(fen);

        if (!_positionCount.ContainsKey(fen))
        {
            _positionCount[fen] = 0;
        }
        _positionCount[fen]++;
    }

    private List<Position> GetPawnMoves(Position from)
    {
        var moves = new List<Position>();
        var piece = GetPiece(from);
        if (piece == null) return moves;

        var direction = piece.Color == Color.White ? -1 : 1;
        var startRank = piece.Color == Color.White ? 6 : 1;

        if (IsValidPosition(new Position(from.Rank + direction, from.File)) &&
            GetPiece(new Position(from.Rank + direction, from.File)) == null)
        {
            moves.Add(new Position(from.Rank + direction, from.File));

            if (from.Rank == startRank &&
                GetPiece(new Position(from.Rank + 2 * direction, from.File)) == null)
            {
                moves.Add(new Position(from.Rank + 2 * direction, from.File));
            }
        }

        foreach (var fileOffset in new[] { -1, 1 })
        {
            var to = new Position(from.Rank + direction, from.File + fileOffset);
            if (IsValidPosition(to))
            {
                var targetPiece = GetPiece(to);
                if (targetPiece != null && targetPiece.Color != piece.Color)
                {
                    moves.Add(to);
                }
                if (to.Equals(_enPassantTarget))
                {
                    moves.Add(to);
                }
            }
        }

        return moves;
    }

    private List<Position> GetPawnAttacks(Position from, Color color)
    {
        var moves = new List<Position>();
        var direction = color == Color.White ? -1 : 1;

        foreach (var fileOffset in new[] { -1, 1 })
        {
            var to = new Position(from.Rank + direction, from.File + fileOffset);
            if (IsValidPosition(to))
            {
                moves.Add(to);
            }
        }

        return moves;
    }

    private List<Position> GetKnightMoves(Position from)
    {
        var moves = new List<Position>();
        var offsets = new[] { (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };

        foreach (var (rankOffset, fileOffset) in offsets)
        {
            var to = new Position(from.Rank + rankOffset, from.File + fileOffset);
            if (IsValidPosition(to))
            {
                var targetPiece = GetPiece(to);
                if (targetPiece == null || targetPiece.Color != _turn)
                {
                    moves.Add(to);
                }
            }
        }

        return moves;
    }

    private List<Position> GetBishopMoves(Position from)
    {
        return GetSlidingMoves(from, new[] { (-1, -1), (-1, 1), (1, -1), (1, 1) });
    }

    private List<Position> GetRookMoves(Position from)
    {
        return GetSlidingMoves(from, new[] { (-1, 0), (1, 0), (0, -1), (0, 1) });
    }

    private List<Position> GetQueenMoves(Position from)
    {
        var moves = GetBishopMoves(from);
        moves.AddRange(GetRookMoves(from));
        return moves;
    }

    private List<Position> GetKingMoves(Position from)
    {
        var moves = new List<Position>();

        for (int rankOffset = -1; rankOffset <= 1; rankOffset++)
        {
            for (int fileOffset = -1; fileOffset <= 1; fileOffset++)
            {
                if (rankOffset == 0 && fileOffset == 0) continue;

                var to = new Position(from.Rank + rankOffset, from.File + fileOffset);
                if (IsValidPosition(to))
                {
                    var targetPiece = GetPiece(to);
                    if (targetPiece == null || targetPiece.Color != _turn)
                    {
                        moves.Add(to);
                    }
                }
            }
        }

        if (CanCastleKingside(_turn))
        {
            moves.Add(new Position(from.Rank, 6));
        }
        if (CanCastleQueenside(_turn))
        {
            moves.Add(new Position(from.Rank, 2));
        }

        return moves;
    }

    private List<Position> GetSlidingMoves(Position from, (int rankOffset, int fileOffset)[] directions)
    {
        var moves = new List<Position>();
        var piece = GetPiece(from);

        foreach (var (rankOffset, fileOffset) in directions)
        {
            for (int i = 1; i < 8; i++)
            {
                var to = new Position(from.Rank + i * rankOffset, from.File + i * fileOffset);
                if (!IsValidPosition(to)) break;

                var targetPiece = GetPiece(to);
                if (targetPiece == null)
                {
                    moves.Add(to);
                }
                else
                {
                    if (targetPiece.Color != piece?.Color)
                    {
                        moves.Add(to);
                    }
                    break;
                }
            }
        }

        return moves;
    }

    private static bool IsValidPosition(Position pos)
    {
        return pos.Rank >= 0 && pos.Rank < 8 && pos.File >= 0 && pos.File < 8;
    }

    private static Position ParseSquare(string square)
    {
        if (square.Length < 2) throw new ArgumentException($"Invalid square: {square}");

        var file = square[0] - 'a';
        var rank = 8 - (square[1] - '0');

        return new Position(rank, file);
    }

    private static PieceType CharToPieceType(char ch)
    {
        return char.ToLower(ch) switch
        {
            'q' => PieceType.Queen,
            'r' => PieceType.Rook,
            'b' => PieceType.Bishop,
            'n' => PieceType.Knight,
            _ => PieceType.Queen
        };
    }
}