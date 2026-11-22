namespace Relick.Core;

public enum RewardKind { Title, Frame, Sleeve, Dice }

/// <summary>A cosmetic that is earned, and how.</summary>
public sealed record Reward(RewardKind Kind, string Id, string HowToEarn)
{
    public string Key => RewardCatalog.KeyOf(Kind, Id);
}

/// <summary>The cosmetics nobody can simply pick. The server grants them and checks them on every save.</summary>
public static class RewardCatalog
{
    public static readonly Reward SeasonTitle = new(RewardKind.Title, "clasificado", "Termina una temporada con 5 duelos");
    public static readonly Reward LeagueFrame = new(RewardKind.Frame, "liga", "Termina una temporada en Oro o más");
    public static readonly Reward SeasonSleeve = new(RewardKind.Sleeve, "temporada", "Termina una temporada en Platino o más");
    public static readonly Reward ChampionTitle = new(RewardKind.Title, "campeon", "Gana un torneo");
    public static readonly Reward ChampionFrame = new(RewardKind.Frame, "campeon", "Gana un torneo");
    public static readonly Reward ChampionDice = new(RewardKind.Dice, "campeon", "Gana un torneo");

    public static readonly IReadOnlyList<Reward> All = [SeasonTitle, LeagueFrame, SeasonSleeve, ChampionTitle, ChampionFrame, ChampionDice];

    public static string KeyOf(RewardKind kind, string id) => kind.ToString().ToLowerInvariant() + ":" + id;

    /// <summary>The reward behind a cosmetic, or null when anyone may wear it.</summary>
    public static Reward? Of(RewardKind kind, string? id) => All.FirstOrDefault(reward => reward.Kind == kind && reward.Id == id);

    public static IEnumerable<Reward> ForSeason(int rating, uint played)
    {
        if (played < CompetitiveRules.PlacementDuels) yield break;
        yield return SeasonTitle;
        var league = CompetitiveRules.LeagueOf(rating);
        if (league >= League.Gold) yield return LeagueFrame;
        if (league >= League.Platinum) yield return SeasonSleeve;
    }

    public static readonly IReadOnlyList<Reward> ForChampion = [ChampionTitle, ChampionFrame, ChampionDice];
}
