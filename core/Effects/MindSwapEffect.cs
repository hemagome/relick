namespace Relick.Core;

/// <summary>Cambio de Mentes: swap your whole hand (even empty) with your opponent's. Not on the first turn of the game.</summary>
internal sealed class MindSwapEffect : ICardEffect
{
    public string CardId => CardCatalog.MindSwap.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;
    public void Resolve(GameEngine.EffectContext ctx) => ctx.SwapHands();
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }
    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
