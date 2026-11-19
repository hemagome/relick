using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// Where a Relikario's art lives. Every form of a Relikario shares its base form's folder
/// (<c>res://assets/characters/&lt;family&gt;/</c>), so "yuno-lv2" draws from "yuno". A Relikario
/// without a file simply has no art: callers fall back to initials or an empty stand.
/// </summary>
public static class RelikarioArt
{
    public static string Family(string? relikario) =>
        RelikarioCatalog.Find(relikario)?.Family ?? relikario ?? RelikarioCatalog.Yuno.Id;

    public static string PathOf(string? relikario, string file) => $"res://assets/characters/{Family(relikario)}/{file}.png";

    public static Texture2D? Load(string? relikario, string file)
    {
        var path = PathOf(relikario, file);
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    /// <summary>The portrait cut from its skill cards, shown beside decks.</summary>
    public static Texture2D? Thumbnail(string? relikario) => Load(relikario, "thumbnail") ?? Portrait(relikario);
    /// <summary>The square portrait of the Relikario picker.</summary>
    public static Texture2D? Portrait(string? relikario) => Load(relikario, "portrait") ??
        (Family(relikario) == RelikarioCatalog.Ironwing.Id ? Load(relikario, "sprite") : null);
    /// <summary>The printed Relikario card at a level.</summary>
    public static Texture2D? Card(string? relikario, int level) => level >= 1 ? Load(relikario, $"card-lv{level}") : null;
}
