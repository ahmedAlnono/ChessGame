// Helpers/RatingHelper.cs
using ChessAPI.Models.Enums;

namespace ChessAPI.Helpers;

public interface IRatingHelper
{
    (int whiteChange, int blackChange) CalculateRatingChange(int whiteRating, int blackRating, GameResult result, int kFactor = 32);
    int CalculateExpectedScore(int rating, int opponentRating);
    RatingTier GetRatingTier(int rating);
    int GetKFactor(int rating, int gamesPlayed);
    double GetWinProbability(int rating, int opponentRating);
    int CalculatePerformanceRating(List<int> opponentRatings, double score);
}

public class RatingHelper : IRatingHelper
{
    public (int whiteChange, int blackChange) CalculateRatingChange(int whiteRating, int blackRating, GameResult result, int kFactor = 32)
    {
        var expectedWhite = CalculateExpectedScore(whiteRating, blackRating);
        var expectedBlack = 1 - expectedWhite;

        double actualWhite = result switch
        {
            GameResult.WhiteWin => 1.0,
            GameResult.BlackWin => 0.0,
            _ => 0.5
        };
        double actualBlack = 1 - actualWhite;

        var whiteChange = (int)Math.Round(kFactor * (actualWhite - expectedWhite));
        var blackChange = (int)Math.Round(kFactor * (actualBlack - expectedBlack));

        return (whiteChange, blackChange);
    }

    public int CalculateExpectedScore(int rating, int opponentRating)
    {
        return (int)Math.Round(1000 / (1 + Math.Pow(10, (opponentRating - rating) / 400.0)));
    }

    public RatingTier GetRatingTier(int rating)
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

    public int GetKFactor(int rating, int gamesPlayed)
    {
        if (gamesPlayed < 30)
            return 40;
        
        if (rating < 2400)
            return 20;
        
        return 10;
    }

    public double GetWinProbability(int rating, int opponentRating)
    {
        return 1.0 / (1.0 + Math.Pow(10, (opponentRating - rating) / 400.0));
    }

    public int CalculatePerformanceRating(List<int> opponentRatings, double score)
    {
        if (opponentRatings.Count == 0 || score == 0 || score == opponentRatings.Count)
            return 0;

        var averageOpponentRating = (int)opponentRatings.Average();
        var winPercentage = score / opponentRatings.Count;

        if (winPercentage == 1.0)
            return averageOpponentRating + 800;
        
        if (winPercentage == 0.0)
            return averageOpponentRating - 800;

        var performanceRating = averageOpponentRating + 
            (int)(400 * Math.Log10(winPercentage / (1 - winPercentage)));

        return performanceRating;
    }
}