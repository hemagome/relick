namespace Relick.Core;

public sealed record RelikarioDefinition(string Id, string Name, int MaxHealth, string AbilityText, bool CanRerollAttack = false, int Level = 1, int MaxRerolls = 1,
    bool CanRerollAnyDie = false, bool ConvertRerolledSix = true, int EvolutionCost = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int EvolutionHealing = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int AttackDieBonusExceptOne = 0)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int StartingDexterity => MaxHealth <= 7 ? 2 : MaxHealth <= 9 ? 1 : 0;
}

public static class RelikarioCatalog
{
    public static readonly RelikarioDefinition Yuno = new("yuno", "Yuno Gyambura", 8,
        "Una vez por turno, al lanzar el dado de ataque, si no sacaste 1 puedes relanzarlo. Si este relanzamiento da 6, se convierte en 5.",
        CanRerollAttack: true);
    public static readonly RelikarioDefinition YunoLv2 = new("yuno-lv2", "Yuno Gyambura", Yuno.MaxHealth,
        "Una vez por turno, al lanzar un dado que no resulte en 1, puedes relanzarlo hasta 2 veces más. Si en un relanzamiento sale 1, ya no puedes relanzarlo más.",
        CanRerollAttack: true, Level: 2, MaxRerolls: 2, CanRerollAnyDie: true, ConvertRerolledSix: false, EvolutionCost: 1800,
        EvolutionHealing: 2, AttackDieBonusExceptOne: 1);

    /// <summary>Base forms a deck can be built around; evolutions are reached during the duel.</summary>
    public static readonly IReadOnlyList<RelikarioDefinition> Playable = [Yuno];

    public static RelikarioDefinition? EvolutionFor(RelikarioDefinition current) => current.Id == Yuno.Id ? YunoLv2 : null;
}
