namespace ChessAPI.Models.Enums;

public enum GameStatus
{
    WaitingForOpponent = 0,
    InProgress = 1,
    Paused = 2,
    Completed = 3,
    Abandoned = 4,
    Draw = 5,
    Resigned = 6,
    Timeout = 7,
    Disconnected = 8
}