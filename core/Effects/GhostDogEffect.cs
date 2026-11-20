namespace Relick.Core;

internal sealed class GhostDogEffect : CardEffect
{
    public override string CardId => CardCatalog.GhostDog.Id;
    public override void Resolve(GameEngine.EffectContext ctx) => ctx.ResolveGhostDog();
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => ctx.GhostDogChoices(decision);
    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) => ctx.ResolveGhostDogChoice(decision, choice);
    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => ctx.ResolveGhostDogResult(next.Value);
}
