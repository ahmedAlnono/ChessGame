// Models/Chess/MoveInfo.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Models.Chess;

public class MoveInfo
{
    public Position From { get; set; }
    public Position To { get; set; }
    public PieceType? Promotion { get; set; }

    public MoveInfo(Position from, Position to, PieceType? promotion = null)
    {
        From = from;
        To = to;
        Promotion = promotion;
    }

    public override string ToString()
    {
        var move = $"{From}{To}";
        if (Promotion.HasValue)
        {
            var promoChar = Promotion.Value switch
            {
                PieceType.Queen => 'q',
                PieceType.Rook => 'r',
                PieceType.Bishop => 'b',
                PieceType.Knight => 'n',
                _ => 'q'
            };
            move += promoChar;
        }
        return move;
    }
}