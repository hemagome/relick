namespace Relick.Core;

/// <summary>Bomba de Tiempo: choose the equipment zone, a random hand card or the Bonus, and destroy a card there.</summary>
internal sealed class TimeBombEffect : ICardEffect
{
    public string CardId => CardCatalog.TimeBomb.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.ZoneTargets(ctx.Actor, excludedHand).Any() ? null : "No hay cartas en equipos, manos o Bonus para destruir.";

    public void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.ZoneTargets().Any()) { ctx.Log($"{ctx.Card.Name} se resuelve sin objetivos disponibles."); return; }
        ctx.Ask(FieldDestruction.Target, DecisionKind.ChooseTarget, "Elige una zona: se destruye una carta de ella", allowsCombos: true, announce: false);
    }

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.ZoneTargets().Select(t => FieldDestruction.Zone(ctx, t)).ToList();

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) => FieldDestruction.Destroy(ctx, choice.Field!);

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => FieldDestruction.Finish(ctx, next, null);
}
