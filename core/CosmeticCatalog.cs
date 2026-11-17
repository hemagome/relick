namespace Relick.Core;

/// <summary>
/// A card sleeve. <see cref="Light"/> and <see cref="Dark"/> recolour the printed card back as a duotone and
/// <see cref="Edge"/> frames it; the classic sleeve keeps the printed back untouched. Colours are hex strings so
/// the server can validate choices without the client's graphics library.
/// </summary>
public sealed record SleeveStyle(string Id, string Name, string? Light = null, string? Dark = null, string? Edge = null)
{
    public bool Printed => Light == null;
}

/// <summary>A die's body and pip colours.</summary>
public sealed record DiceStyle(string Id, string Name, string Body, string Pips);

/// <summary>
/// Purely cosmetic choices saved with each deck. They never change a rule: the engine only carries them so
/// both players see each other's sleeves and dice.
/// </summary>
public static class CosmeticCatalog
{
    public const string DefaultSleeve = "clasica", DefaultDice = "clasico";

    public static readonly IReadOnlyList<SleeveStyle> Sleeves =
    [
        new(DefaultSleeve, "Transparente"),
        new("menta", "Menta", "4fe3b8", "06241d", "4fe3b8"),
        new("coral", "Coral", "ff6f8e", "2a0812", "ff6f8e"),
        new("oro", "Oro", "ffc86b", "2b1a02", "ffc86b"),
        new("violeta", "Violeta", "b5a2fa", "171034", "b5a2fa"),
        new("medianoche", "Medianoche", "7c8fbf", "070a14", "ffc86b")
    ];

    public static readonly IReadOnlyList<DiceStyle> Dice =
    [
        new(DefaultDice, "Clásico", "eee4d1", "172438"),
        new("menta", "Menta", "4fe3b8", "06241d"),
        new("coral", "Coral", "ff6f8e", "2a0812"),
        new("oro", "Oro", "ffc86b", "2b1a02"),
        new("violeta", "Violeta", "b5a2fa", "171034"),
        new("obsidiana", "Obsidiana", "1d2130", "ffc86b")
    ];

    public static bool IsSleeve(string? id) => Sleeves.Any(style => style.Id == id);
    public static bool IsDice(string? id) => Dice.Any(style => style.Id == id);

    /// <summary>The sleeve with this id, or the classic one for an unknown or missing id.</summary>
    public static SleeveStyle Sleeve(string? id) => Sleeves.FirstOrDefault(style => style.Id == id) ?? Sleeves[0];

    /// <summary>The die with this id, or the classic one for an unknown or missing id.</summary>
    public static DiceStyle DiceFor(string? id) => Dice.FirstOrDefault(style => style.Id == id) ?? Dice[0];
}
