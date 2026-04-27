namespace ChessAPI.Models.Enums;

public enum GameResult
{
    None = 0,
    WhiteWin = 1,
    BlackWin = 2,
    Draw = 3,
    Stalemate = 4,
    ThreefoldRepetition = 5,
    FiftyMoveRule = 6,
    InsufficientMaterial = 7
}