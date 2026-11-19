namespace Relick.Core;

internal sealed class MagicTomeEffect : ICardEffect
{
    public string CardId => CardCatalog.MagicTome.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Tomo de Magia necesita colocarse sobre tu Relikario." : null;
    public void Resolve(GameEngine.EffectContext ctx) => ctx.DeployMagicTome();
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }
    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
