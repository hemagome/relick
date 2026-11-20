namespace Relick.Core;

/// <summary>
/// Zahira Flair (trap placed on a card in play): in the final stage of its owner's rival, the owner rolls 1D6 for each
/// copy. 1–2: destroy Zahira Flair; 3–4: move it onto another card; 5–6: 2 damage to the Relikario under it, or destroy
/// the other card under it. The roll's outcome is still resolved in the engine (GameEngine.Zahira.cs).
/// </summary>
internal sealed class ZahiraFlairEffect : CardRules, ITurnTrigger
{
    public override string CardId => CardCatalog.ZahiraFlair.Id;

    public TurnMoment Moment => TurnMoment.FinalStage;
    public int Order => 0;

    /// <summary>Rolls for the next copy that has not rolled this final stage; each roll comes back through the pending play.</summary>
    public bool Fire(GameEngine.EffectContext ctx)
    {
        var state = ctx.State;
        var owner = 1 - state.CurrentPlayer;
        var p = state.Players[owner];
        // Snapshots taken before a second copy existed only recorded that the single roll was done.
        for (var slot = Math.Max(state.ZahiraFinalSlot, state.ZahiraRolledThisFinal ? 1 : 0); slot < PlayerState.ZahiraSlots; slot++)
        {
            if (p.Zahira(slot) == null || ctx.EffectsSuppressed(owner, FieldZone.Zahira, GameEngine.ZahiraIndex(slot))) continue;
            state.ZahiraRolledThisFinal = true;
            state.ZahiraFinalSlot = slot + 1;
            ctx.RollResolvedByEngine(owner, DiePurpose.Zahira, slot);
            return true;
        }
        return false;
    }
}
