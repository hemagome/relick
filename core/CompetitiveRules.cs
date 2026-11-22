namespace Relick.Core;

public enum League { Bronze, Silver, Gold, Platinum, Legend }

/// <summary>
/// The numbers and formulas of the rated modes. Pure, so the server applies them, the client explains them and the
/// engine's tests check them. Nothing here changes a rule of the duel.
/// </summary>
public static class CompetitiveRules
{
    public const int StartRating = 1000, RatingFloor = 100;
    /// <summary>The first duels of a season move the rating twice as far and show no league yet.</summary>
    public const int PlacementDuels = 5;
    private const int PlacementK = 48, SettledK = 24;
    /// <summary>Two players are paired at most this often in a day, so friends cannot feed each other points.</summary>
    public const int PairDuelsPerDay = 5;
    private const int OpeningWindow = 100, WindowPerSecond = 5, AnyoneAfterSeconds = 120;
    public const long DecisionLimit = 300_000_000, PreparationLimit = 300_000_000, CheckInLimit = 600_000_000;
    /// <summary>After this many draws the better seed advances.</summary>
    public const int TournamentDraws = 3;
    public const int MinEntrants = 2, MaxEntrants = 32;

    public static double Expected(int rating, int rival) => 1 / (1 + Math.Pow(10, (rival - rating) / 400.0));

    /// <summary>The rating after a duel. <paramref name="score"/> is 1 for a win, 0.5 for a draw and 0 for a loss.</summary>
    public static int After(int rating, int rival, double score, int played)
    {
        var k = played < PlacementDuels ? PlacementK : SettledK;
        var change = (int)Math.Round(k * (score - Expected(rating, rival)), MidpointRounding.AwayFromZero);
        // A win always pays something and a loss always costs, however lopsided the pairing.
        if (score == 1) change = Math.Max(1, change);
        if (score == 0) change = Math.Min(-1, change);
        return Math.Max(RatingFloor, rating + change);
    }

    /// <summary>How far apart two ratings may be once the longer wait of the two has lasted this many seconds.</summary>
    public static int Window(double waitedSeconds) =>
        waitedSeconds >= AnyoneAfterSeconds ? int.MaxValue : OpeningWindow + (int)Math.Max(0, waitedSeconds) * WindowPerSecond;

    /// <summary>A new season keeps half of the distance to the start.</summary>
    public static int SoftReset(int previous) => Math.Max(RatingFloor, StartRating + (previous - StartRating) / 2);

    public static League LeagueOf(int rating) => rating switch
    {
        < 950 => League.Bronze, < 1100 => League.Silver, < 1250 => League.Gold, < 1400 => League.Platinum, _ => League.Legend
    };

    public static string LeagueName(League league) => league switch
    {
        League.Bronze => "Bronce", League.Silver => "Plata", League.Gold => "Oro", League.Platinum => "Platino", _ => "Leyenda"
    };

    /// <summary>"Oro · 1180", or how many placement duels remain.</summary>
    public static string Standing(int rating, uint played) => played < PlacementDuels
        ? $"Colocación {played}/{PlacementDuels}" : $"{LeagueName(LeagueOf(rating))} · {rating}";

    public static int Rounds(int entrants)
    {
        var rounds = 0;
        while (1 << rounds < entrants) rounds++;
        return rounds;
    }

    /// <summary>
    /// Seeds (from 1) of the first round, in bracket order and two per match. Zero is a bye, which always falls to
    /// the best seeds, and the first two seeds can only meet in the final.
    /// </summary>
    public static int[] FirstRound(int entrants)
    {
        List<int> order = [1];
        while (order.Count < entrants)
        {
            var sum = order.Count * 2 + 1;
            order = order.SelectMany(seed => new[] { seed, sum - seed }).ToList();
        }
        return order.Select(seed => seed > entrants ? 0 : seed).ToArray();
    }

    /// <summary>"Campeón", "2.º" or "Top 4" for someone who left in <paramref name="outRound"/>.</summary>
    public static string Placing(int rounds, int outRound) =>
        outRound > rounds ? "Campeón" : outRound == rounds ? "2.º" : $"Top {1 << (rounds - outRound + 1)}";
}
