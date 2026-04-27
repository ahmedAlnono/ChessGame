// Models/Chess/Piece.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Chess;

public class Piece
{
    public Color Color { get; set; }
    public PieceType Type { get; set; }

    public Piece(Color color, PieceType type)
    {
        Color = color;
        Type = type;
    }

    public override string ToString()
    {
        var ch = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new ArgumentException($"Invalid piece type: {Type}")
        };

        return Color == Color.White ? char.ToUpper(ch).ToString() : ch.ToString();
    }
}