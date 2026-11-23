namespace Relick.Core;

public sealed record RelikarioDefinition(string Id, string Name, int MaxHealth, string AbilityText, bool CanRerollAttack = false, int Level = 1, int MaxRerolls = 1,
    bool CanRerollAnyDie = false, bool ConvertRerolledSix = true, int EvolutionCost = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int EvolutionHealing = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int AttackDieBonusExceptOne = 0)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int StartingDexterity => MaxHealth <= 7 ? 2 : MaxHealth <= 9 ? 1 : 0;

    // Catalog data that is never saved: a saved duel resolves its Relikario again by Id (EngineSnapshot).
    /// <summary>Name used in prompts and logs ("Relanzar con Yuno"); defaults to the full name.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? ShortName { get; init; }
    /// <summary>Id of the form this Relikario evolves into during a duel, or null at its last level.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? EvolvesTo { get; init; }
    /// <summary>Id of the base form that decks, art and music are keyed by; null for a base form.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? BaseForm { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int? EvolutionMaxHealth { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int AttackDieBonus => AttackDieBonusExceptOne + AttackDieBonusIncludingOne;
    [System.Text.Json.Serialization.JsonIgnore]
    public int AttackDieBonusIncludingOne { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int EvolutionDiscards { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DefaultAttackArtId { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName => ShortName ?? Name;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Family => BaseForm ?? Id;
}

/// <summary>A Relikario as listed by the deck builder; <see cref="Definition"/> is null until it is programmed.</summary>
public sealed record RelikarioEntry(string Id, string Name, RelikarioDefinition? Definition = null)
{
    public bool Playable => Definition != null;
}

public static class RelikarioCatalog
{
    public static readonly RelikarioDefinition Yuno = new("yuno", "Yuno Gyambura", 8,
        "Una vez por turno, al lanzar el dado de ataque, si no sacaste 1 puedes relanzarlo. Si este relanzamiento da 6, se convierte en 5.",
        CanRerollAttack: true) { ShortName = "Yuno", EvolvesTo = "yuno-lv2" };
    public static readonly RelikarioDefinition YunoLv2 = new("yuno-lv2", "Yuno Gyambura", Yuno.MaxHealth,
        "Una vez por turno, al lanzar un dado que no resulte en 1, puedes relanzarlo hasta 2 veces más. Si en un relanzamiento sale 1, ya no puedes relanzarlo más.",
        CanRerollAttack: true, Level: 2, MaxRerolls: 2, CanRerollAnyDie: true, ConvertRerolledSix: false, EvolutionCost: 1800,
        EvolutionHealing: 2, AttackDieBonusExceptOne: 1) { ShortName = "Yuno", BaseForm = "yuno" };
    public static readonly RelikarioDefinition Osamu = new("osamu", "Osamu Takahashi", 10,
        "Al realizar un ataque exitoso, puedes buscar Bomba de Tiempo o Agilidad al 100% en tu mazo. Si fallas con un resultado distinto de 1, añade una Agilidad al 100% de tu memoria a tu mano.")
        { ShortName = "Osamu", EvolvesTo = "osamu-lv2" };
    public static readonly RelikarioDefinition OsamuLv2 = new("osamu-lv2", "Osamu Takahashi", Osamu.MaxHealth,
        "Al evolucionar, añade una Agilidad al 100% de tu mazo o memoria a tu mano. Una vez por turno, gasta 1 acción para buscar una carta de tu mazo o memoria. Esa copia se corrompe al ir a memoria. Suma +1 al D6 de ataque.",
        Level: 2, EvolutionCost: 1500) { ShortName = "Osamu", BaseForm = "osamu", EvolutionMaxHealth = 4, AttackDieBonusIncludingOne = 1 };
    public static readonly RelikarioDefinition Ironwing = new("ironwing", "Ironwing CJC-019", 9,
        "Al acertar un ataque, puedes enviar al fondo del mazo las cartas de tu zona de Bonus y reponer el Bonus, hasta tres veces por ataque. En tu D6 de ataque, puedes destruir hasta 4 Ataques de tu mazo y sumar +1 por cada 2, hasta 5. Evoluciona con Agujero Negro como tu Área activa y 1200 PR.")
        { ShortName = "Ironwing", EvolvesTo = "ironwing-lv2", DefaultAttackArtId = "attack-ironwing-ut22" };
    public static readonly RelikarioDefinition IronwingLv2 = new("ironwing-lv2", "Ironwing CJC-019", Ironwing.MaxHealth,
        "Al evolucionar, devuelve hasta 5 Ataques de tu memoria al mazo y barájalo. Suma +1 a tu D6 de ataque. Cuando lances el dado de Agujero Negro, cámbialo a 6. Puedes destruir Ataques de tu mazo para sumar +1 por cada uno a un D6 propio.",
        Level: 2, EvolutionCost: 1200) { ShortName = "Ironwing", BaseForm = "ironwing", AttackDieBonusIncludingOne = 1 };
    public static readonly RelikarioDefinition Wd = new("wd", "WD", 11,
        "Al atacar, puedes descartar cartas Bala o de Ataque para sumar +1 al D6 de ataque por cada carta descartada, hasta 5.")
        { EvolvesTo = "wd-lv2", DefaultAttackArtId = "attack-wd" };
    public static readonly RelikarioDefinition WdLv2 = new("wd-lv2", "WD", Wd.MaxHealth,
        "Al lanzar un dado, puedes descartar cualquier carta para sumar +1 por carta. Suma +1 al D6 de ataque.",
        Level: 2, EvolutionCost: 1600, EvolutionHealing: 3)
        { BaseForm = "wd", EvolutionDiscards = 4, AttackDieBonusIncludingOne = 1 };

    public static readonly RelikarioDefinition YunoOro = new("yuno-oro", "Yuno Oro", 8,
        "Una vez por turno, si tienes dos letras distintas encima de tu Relikario, puedes relanzar uno de tus dados. Al obtener un 6 natural en tu D6 de ataque, busca una letra en tu mazo, añádela a tu mano y baraja.",
        CanRerollAttack: true, CanRerollAnyDie: true, ConvertRerolledSix: false)
        { EvolvesTo = "yuno-oro-lv2", DefaultAttackArtId = "attack-yuno-oro" };
    public static readonly RelikarioDefinition YunoOroLv2 = new("yuno-oro-lv2", "Yuno Oro", 8,
        "Al subir de nivel, coloca una letra de tu mazo, memoria o corruptas encima de tu Relikario. Suma +1 al D6 de ataque.",
        Level: 2, EvolutionCost: 3500) { BaseForm = "yuno-oro", AttackDieBonusIncludingOne = 1 };

    /// <summary>Every programmed form, evolutions included, by Id.</summary>
    public static readonly IReadOnlyDictionary<string, RelikarioDefinition> All =
        new[] { Yuno, YunoLv2, Osamu, OsamuLv2, Ironwing, IronwingLv2, Wd, WdLv2, YunoOro, YunoOroLv2 }.ToDictionary(r => r.Id);

    public static RelikarioDefinition? Find(string? id) => id != null && All.TryGetValue(id, out var r) ? r : null;

    /// <summary>Base forms a deck can be built around; evolutions are reached during the duel.</summary>
    public static readonly IReadOnlyList<RelikarioDefinition> Playable = [Yuno, Osamu, Wd, YunoOro, Ironwing];

    /// <summary>Every Relikario of the game, in the order the deck builder lists them. Only those with a
    /// definition are programmed and can have decks built for them; the rest are shown as coming soon.</summary>
    public static readonly IReadOnlyList<RelikarioEntry> Roster =
    [
        new("yuno", "Yuno", Yuno), new("osamu", "Osamu", Osamu), new("wd", "WD", Wd), new("godkira", "Godkira"), new("jen95", "Jen95"),
        new("yuno-8bits", "Yuno 8 bits"), new("yuno-abogado", "Yuno abogado"), new("yuno-oro", "Yuno Oro", YunoOro), new("senshuni", "Senshuni"),
        new("zii-jay", "Zii Jay"), new("hiperman", "Hiperman"), new("ef-jen", "EF Jen"), new("ef-wd", "EF WD"), new("ironwing", "Ironwing", Ironwing),
        new("shuya", "Shuya"), new("zarpo", "Zarpo"), new("kael", "Kael"), new("kurtz", "Kurtz")
    ];

    public static RelikarioDefinition? EvolutionFor(RelikarioDefinition current) => Find(Find(current.Id)?.EvolvesTo ?? current.EvolvesTo);

    /// <summary>Display name of a Relikario or roster entry by any form's Id ("yuno-lv2" is still Yuno).</summary>
    public static string NameOf(string? id) => Find(id)?.Name ?? Roster.FirstOrDefault(r => r.Id == id)?.Name ?? id ?? "";
    /// <summary>Short name for prompts ("Relanzar con Yuno") by any form's Id.</summary>
    public static string DisplayNameOf(string? id) => Find(id)?.DisplayName ?? NameOf(id);
}
