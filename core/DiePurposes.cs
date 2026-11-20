namespace Relick.Core;

/// <summary>
/// What each die purpose shows: the caption under the die and the card the resolution view names while the roll waits.
/// One table, so a new purpose is added in one place.
/// </summary>
internal static class DiePurposes
{
    private sealed record Info(string Caption, string? CardId);

    private static readonly IReadOnlyDictionary<DiePurpose, Info> Table = new Dictionary<DiePurpose, Info>
    {
        [DiePurpose.Zahira] = new("Zahira Flair · Etapa final", CardCatalog.ZahiraFlair.Id),
        [DiePurpose.QuickLoad] = new("Carga Rápida · Robo o descarte", CardCatalog.QuickLoad.Id),
        [DiePurpose.FieldCombat] = new("Jessica Lee · Efecto de canje/Bonus", CardCatalog.Jessica.Id),
        [DiePurpose.SkillLearning] = new("Reverse Shuffle · Aprendizaje", CardCatalog.Reverse.Id),
        [DiePurpose.PcStore] = new("PC 8BITS · Valor guardado", CardCatalog.Pc.Id),
        [DiePurpose.Dodge] = new("Esquivar · Defensa", CardCatalog.Dodge.Id),
        [DiePurpose.ExtraLife] = new("Vida Extra · Curación", CardCatalog.ExtraLife.Id),
        [DiePurpose.PointsChange] = new("Aumento de Relick Points", CardCatalog.PointsIncrease.Id),
        [DiePurpose.TurboSwordDefense] = new("Turbo Sword · Defensa", CardCatalog.TurboSword.Id),
        [DiePurpose.TurboSwordDamage] = new("Turbo Sword · Daño", CardCatalog.TurboSword.Id),
        [DiePurpose.TurboOverlapDamage] = new("Turbo Overlap · Dado de daño", CardCatalog.TurboOverlap.Id),
        // The resolution view never named a card from these two purposes; they keep it that way (null) until
        // someone decides it should.
        [DiePurpose.BlackHole] = new("Agujero Negro · Dado de Área", null),
        [DiePurpose.GhostDog] = new("Perro Fantasma · Recuperación", null),
    };

    /// <summary>The caption under the die; Turbo Overlap numbers its two damage dice.</summary>
    public static string Caption(DiePurpose purpose, DieEffect effect) =>
        !Table.TryGetValue(purpose, out var info) ? ""
        : purpose == DiePurpose.TurboOverlapDamage ? $"{info.Caption} {(effect.Slot == 0 ? 1 : 2)}"
        : info.Caption;

    /// <summary>The card a pending roll of this purpose belongs to, or null when the purpose names none.</summary>
    public static string? CardId(DiePurpose purpose) => Table.TryGetValue(purpose, out var info) ? info.CardId : null;
}
