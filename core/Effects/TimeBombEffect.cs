namespace Relick.Core;

/// <summary>Bomba de Tiempo: choose the equipment zone, a random hand card or the Bonus, and destroy a card there.</summary>
internal sealed class TimeBombEffect : CardEffect
{
    public override string CardId => CardCatalog.TimeBomb.Id;

    private const string TargetInstruction = "Elige una zona: se destruye una carta de ella";

    /// <summary>The zone choice as <c>Resolve</c> opens it; duels saved in the former destruction phase become this decision.</summary>
    internal static PendingDecision TargetDecision(int actor, Phase resumePhase) => new()
    {
        Source = CardCatalog.TimeBomb.Id, Step = FieldDestruction.Target, Decider = actor, Kind = DecisionKind.ChooseTarget,
        Instruction = TargetInstruction, AllowsCombos = true, ResumePhase = resumePhase
    };

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.ZoneTargets(ctx.Actor, excludedHand).Any() ? null : "No hay cartas en equipos, manos o Bonus para destruir.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.ZoneTargets().Any()) { ctx.Log($"{ctx.Card.Name} se resuelve sin objetivos disponibles."); return; }
        ctx.Ask(FieldDestruction.Target, DecisionKind.ChooseTarget, TargetInstruction, allowsCombos: true, announce: false);
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.ZoneTargets().Select(t => FieldDestruction.Zone(ctx, t)).ToList();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) => FieldDestruction.Destroy(ctx, choice.Field!);

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => FieldDestruction.Finish(ctx, next, null);
}
