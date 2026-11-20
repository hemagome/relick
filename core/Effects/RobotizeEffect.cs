namespace Relick.Core;

internal sealed class RobotizeEffect : CardEffect
{
    public override string CardId => CardCatalog.Robotize.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Robotizar necesita colocarse encima del Relikario y no puede activarse con Relikia." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DeployRobotize();

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => ctx.RobotizeTopCardChoices();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveRobotizeTopCard(decision.Continuation!, choice.Value == 1);
}
