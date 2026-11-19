namespace Relick.Core;

internal sealed class GhostDogEffect : ICardEffect
{
    public string CardId => CardCatalog.GhostDog.Id;
    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;
    public void Resolve(GameEngine.EffectContext ctx) => ctx.ResolveGhostDog();
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => ctx.GhostDogChoices(decision);
    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) => ctx.ResolveGhostDogChoice(decision, choice);
    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => ctx.ResolveGhostDogResult(next.Value);
}
