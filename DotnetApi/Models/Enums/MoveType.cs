namespace ChessAPI.Models.Enums;

public enum MoveType
{
    Normal = 0,
    Capture = 1,
    Castle = 2,
    EnPassant = 3,
    Promotion = 4,
    Check = 5,
    Checkmate = 6
}
