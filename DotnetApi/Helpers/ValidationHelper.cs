// Helpers/ValidationHelper.cs
using System.Text.RegularExpressions;

namespace ChessAPI.Helpers;

public interface IValidationHelper
{
    bool IsValidEmail(string email);
    bool IsValidUsername(string username);
    bool IsValidPassword(string password);
    bool IsValidFen(string fen);
    bool IsValidSquare(string square);
    bool IsValidMove(string move);
    string SanitizeInput(string input);
    bool IsValidGameId(Guid gameId);
}

public class ValidationHelper : IValidationHelper
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_-]{3,20}$",
        RegexOptions.Compiled);

    private static readonly Regex FenRegex = new(
        @"^([rnbqkpRNBQKP1-8]+\/){7}[rnbqkpRNBQKP1-8]+\s[bw]\s(-|K?Q?k?q?)\s(-|[a-h][3-6])\s\d+\s\d+$",
        RegexOptions.Compiled);

    private static readonly Regex SquareRegex = new(
        @"^[a-h][1-8]$",
        RegexOptions.Compiled);

    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        return EmailRegex.IsMatch(email);
    }

    public bool IsValidUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return UsernameRegex.IsMatch(username);
    }

    public bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        var hasUpperCase = password.Any(char.IsUpper);
        var hasLowerCase = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecialChar = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
    }

    public bool IsValidFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            return false;

        return FenRegex.IsMatch(fen);
    }

    public bool IsValidSquare(string square)
    {
        if (string.IsNullOrWhiteSpace(square))
            return false;

        return SquareRegex.IsMatch(square.ToLower());
    }

    public bool IsValidMove(string move)
    {
        if (string.IsNullOrWhiteSpace(move) || move.Length < 4)
            return false;

        var from = move[..2];
        var to = move.Substring(2, 2);

        if (!IsValidSquare(from) || !IsValidSquare(to))
            return false;

        if (move.Length > 4)
        {
            var promotion = move[4];
            if (!"qrbn".Contains(char.ToLower(promotion)))
                return false;
        }

        return true;
    }

    public string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        input = Regex.Replace(input, @"<[^>]*>", string.Empty);
        input = Regex.Replace(input, @"[^\w\s\-_.@]", string.Empty);
        
        return input.Trim();
    }

    public bool IsValidGameId(Guid gameId)
    {
        return gameId != Guid.Empty;
    }
}