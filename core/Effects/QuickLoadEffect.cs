namespace Relick.Core;

/// <summary>Carga Rápida: draw 2, then roll 1D6. 1–2: discard 2; 3–4: discard 1; 5: draw 1; 6: draw 2.</summary>
internal sealed class QuickLoadEffect : ICardEffect
{
    private const string Roll = "roll", Result = "result";

    public string CardId => CardCatalog.QuickLoad.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;
    public void Resolve(GameEngine.EffectContext ctx) => ctx.Draw(ctx.Card.Draw, ctx.Then(Roll));
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == Roll) { ctx.Roll(DiePurpose.QuickLoad, Result); return; }
        var die = next.Value;
        ctx.CloseErrorWindow();
        if (die > 6) return;
        if (die >= 5)
        {
            var count = die == 5 ? 1 : 2;
            ctx.Announce($"{ctx.Player.Name} · Carga Rápida · D6 = {die} · Roba {count} carta(s) adicional(es).");
            ctx.Draw(count);
            return;
        }
        var discards = Math.Min(die <= 2 ? 2 : 1, ctx.Player.Hand.Count);
        ctx.Announce($"{ctx.Player.Name} · Carga Rápida · D6 = {die} · Elige {discards} carta(s) para descartar.");
        // The chosen result is committed. The shared discard flow owns its choices and triggers.
        ctx.RequireDiscard(discards);
    }
}
