namespace Relick.Core;

/// <summary>A title shown under the player's name.</summary>
public sealed record ProfileTitle(string Id, string Name);

/// <summary>The border around the profile portrait. <see cref="Glow"/> adds a soft halo when set.</summary>
public sealed record ProfileFrame(string Id, string Name, string Edge, string? Glow = null);

/// <summary>
/// The cloth under the arena. Only the surfaces change: the own and rival accents keep their meaning on every
/// playmat. Colours are hex strings so the server can validate choices without the client's graphics library.
/// </summary>
public sealed record ProfilePlaymat(string Id, string Name, string Background, string Surface, string Raised, string Frame, string Trace);

/// <summary>One player's results with one Relikario (its base form).</summary>
public sealed record RelikarioRecord(string Relikario, uint Wins, uint Losses, uint Draws)
{
    public uint Played => Wins + Losses + Draws;
}

/// <summary>
/// Purely cosmetic choices of the public profile. Like <see cref="CosmeticCatalog"/>, they never change a rule;
/// an unknown or missing id falls back to the first entry of its list.
/// </summary>
public static class ProfileCatalog
{
    public const string DefaultTitle = "novato", DefaultFrame = "clasico", DefaultPlaymat = "clasico";
    public static string DefaultAvatar => RelikarioCatalog.Yuno.Id;

    /// <summary>Portraits: the base form of every Relikario a deck can be built around.</summary>
    public static IReadOnlyList<string> Avatars => RelikarioCatalog.Playable.Select(r => r.Id).ToList();

    public static readonly IReadOnlyList<ProfileTitle> Titles =
    [
        new(DefaultTitle, "Novato"),
        new("duelista", "Duelista"),
        new("estratega", "Estratega"),
        new("temerario", "Temerario"),
        new("lanzadados", "Lanzadados"),
        new("coleccionista", "Coleccionista"),
        new("guardian", "Guardián del Relikario")
    ];

    public static readonly IReadOnlyList<ProfileFrame> Frames =
    [
        new(DefaultFrame, "Clásico", "46557d"),
        new("menta", "Menta", "4fe3b8", "4fe3b8"),
        new("coral", "Coral", "ff6f8e", "ff6f8e"),
        new("oro", "Oro", "ffc86b", "ffc86b"),
        new("violeta", "Violeta", "b5a2fa", "b5a2fa"),
        new("cian", "Cian", "5fe0f2", "5fe0f2")
    ];

    public static readonly IReadOnlyList<ProfilePlaymat> Playmats =
    [
        new(DefaultPlaymat, "Clásico", "070a14", "121829", "1c2540", "46557d", "6f8fd0"),
        new("abismo", "Abismo", "03100f", "0b1f1d", "13302c", "2f5f58", "4fb8a4"),
        new("carmesi", "Carmesí", "12060a", "221016", "331822", "6b3445", "c8607a"),
        new("aurora", "Aurora", "0b0716", "17112b", "221a3f", "4e3f86", "9c86f0"),
        new("arena", "Arena", "120d05", "211a0e", "302615", "6a5530", "d0a85e")
    ];

    public static bool IsAvatar(string? id) => id != null && Avatars.Contains(id);
    public static bool IsTitle(string? id) => Titles.Any(title => title.Id == id);
    public static bool IsFrame(string? id) => Frames.Any(frame => frame.Id == id);
    public static bool IsPlaymat(string? id) => Playmats.Any(mat => mat.Id == id);

    public static string Avatar(string? id) => IsAvatar(id) ? id! : DefaultAvatar;
    public static ProfileTitle Title(string? id) => Titles.FirstOrDefault(title => title.Id == id) ?? Titles[0];
    public static ProfileFrame Frame(string? id) => Frames.FirstOrDefault(frame => frame.Id == id) ?? Frames[0];
    public static ProfilePlaymat Playmat(string? id) => Playmats.FirstOrDefault(mat => mat.Id == id) ?? Playmats[0];

    /// <summary>Statistics are kept per base form, so an evolved Relikario counts for the one the deck was built around.</summary>
    public static string StatsKey(string? relikario) =>
        RelikarioCatalog.Find(relikario)?.Family ?? RelikarioCatalog.Yuno.Id;

    /// <summary>The most played Relikario; ties go to the one with more wins, then to catalogue order. Null without duels.</summary>
    public static string? Favorite(IEnumerable<RelikarioRecord> records)
    {
        var order = RelikarioCatalog.Playable.Select(r => r.Id).ToList();
        int Rank(string id) => order.IndexOf(id) is var i and >= 0 ? i : int.MaxValue;
        return records.Where(r => r.Played > 0).OrderByDescending(r => r.Played).ThenByDescending(r => r.Wins)
            .ThenBy(r => Rank(r.Relikario)).FirstOrDefault()?.Relikario;
    }

    /// <summary>Whole-number percentage of duels won; draws count as played but not won. Null without duels.</summary>
    public static int? WinRate(uint wins, uint played) => played == 0 ? null : (int)Math.Round(100.0 * wins / played, MidpointRounding.AwayFromZero);
}
