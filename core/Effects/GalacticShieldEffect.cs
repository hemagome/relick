namespace Relick.Core;

internal sealed class GalacticShieldEffect : CardEffect, IFieldLeaveReplacement
{
    public override string CardId => CardCatalog.GalacticShield.Id;

    // First: the shield answers before Tótem de Oro or Reverse Shuffle.
    public LeaveStep Step => LeaveStep.Protect;

    /// <summary>Every shield on the player's Relikario, as a field target that keeps the copy's own instance id.</summary>
    internal static IEnumerable<FieldTarget> Shields(GameState state, int owner) =>
        Enumerable.Range(0, state.Players[owner].GalacticShields?.Count ?? 0)
            .Select(index => new FieldTarget(owner, FieldZone.GalacticShield, index,
                state.Players[owner].GalacticShields![index].Instance ?? CardCatalog.GalacticShield.Id));

    /// <summary>The shields that still hold face-down cards and keep their effects.</summary>
    internal static IEnumerable<FieldTarget> ChargedShields(GameState state, int owner, Func<int, FieldZone, int, bool> suppressed) =>
        Shields(state, owner).Where(shield =>
            state.Players[owner].GalacticShields![shield.Index].Cards.Count > 0 && !suppressed(owner, shield.Zone, shield.Index));

    /// <summary>
    /// Another card on top of the owner's Relikario would leave the field: the owner may send a face-down card to memory
    /// instead. Declining lets the destruction go on from the next step.
    /// </summary>
    public bool TryReplace(GameEngine.EffectContext ctx, FieldTarget target, FieldLeave leave, EngineContinuation? then)
    {
        if (ctx.RelikarioOwnerBelow(target) is not int owner ||
            !ChargedShields(ctx.State, owner, ctx.EffectsSuppressed).Any(shield => shield != target)) return false;
        ctx.Ask("protect", DecisionKind.Confirm,
            $"Puedes enviar una carta boca abajo a tu memoria para evitar que {ctx.FieldName(target)} salga del campo.",
            decider: owner, continuation: new EngineContinuation { Actor = owner, Target = target, Next = then });
        return true;
    }

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Escudo Galáctico necesita colocarse sobre el Relikario y no puede activarse con Relikia." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DeployGalacticShield();

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.GalacticShieldChoices(decision);

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveGalacticShieldChoice(decision, choice);
}
