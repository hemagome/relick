namespace Relick.Core;

internal sealed class RobotizeEffect : ICardEffect
{
    public string CardId => CardCatalog.Robotize.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Robotizar necesita colocarse encima del Relikario y no puede activarse con Relikia." : null;

    public void Resolve(GameEngine.EffectContext ctx) => ctx.DeployRobotize();

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => ctx.RobotizeTopCardChoices();

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveRobotizeTopCard(decision.Continuation!, choice.Value == 1);

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
