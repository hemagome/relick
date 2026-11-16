namespace Relick.Core;

/// <summary>Senshuni: draw 2 cards.</summary>
internal sealed class SenshuniEffect : ICardEffect
{
    public string CardId => CardCatalog.Senshuni.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;

    public void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        ctx.Draw(ctx.Card.Draw);
    }

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }
    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
