namespace Relick.Core;

public enum RelikiaEffect { ChangeBonus, DrawAndPlace, ReplayCode, ClaimBonus }
public enum RelikiaChoice { PlaceHandCard, BonusReward, CorruptCode }
public enum RelikiaReward { OpponentDiscards, CorruptCodes }
public enum CardDestination { DeckBottom, Exchange }

public sealed record RelikiaEffectDefinition(RelikiaEffect Id, string Name, int Cost, int Actions, string Text)
{
    public string ActionLabel => Actions == 1 ? "1 acción" : $"{Actions} acciones";
}

public static class RelikiaEffects
{
    public static IReadOnlyList<RelikiaEffectDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new RelikiaEffectDefinition(RelikiaEffect.ChangeBonus, "Cambiar el Bonus", 100, 1, "Sin cartas en canje: mueve tu Bonus a canje y coloca el siguiente Bonus del mazo."),
        new RelikiaEffectDefinition(RelikiaEffect.DrawAndPlace, "Robar una carta", 300, 1, "Roba una carta; después elige una de tu mano y envíala al fondo del mazo o a canje."),
        new RelikiaEffectDefinition(RelikiaEffect.ReplayCode, "Reciclar un código", 400, 2, "Activa un código de tu memoria y corrómpelo. Consume las dos acciones."),
        new RelikiaEffectDefinition(RelikiaEffect.ClaimBonus, "Robar el Bonus", 500, 1, "Roba tu Bonus y lanza un D6. Con 5 o 6 eliges: el rival descarta dos cartas o corrompes dos códigos de cualquier memoria.")
    });
    public static RelikiaEffectDefinition Get(RelikiaEffect effect) => All.Single(e => e.Id == effect);
}
