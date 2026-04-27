// Models/Chess/Position.cs
namespace ChessAPI.Models.Chess;

public class Position : IEquatable<Position>
{
    public int Rank { get; set; }
    public int File { get; set; }

    public Position(int rank, int file)
    {
        Rank = rank;
        File = file;
    }

    public override string ToString()
    {
        var fileChar = (char)('a' + File);
        var rankChar = (char)('1' + (7 - Rank));
        return $"{fileChar}{rankChar}";
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Position);
    }

    public bool Equals(Position? other)
    {
        if (other is null) return false;
        return Rank == other.Rank && File == other.File;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Rank, File);
    }

    public static bool operator ==(Position? left, Position? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Position? left, Position? right)
    {
        return !(left == right);
    }
}