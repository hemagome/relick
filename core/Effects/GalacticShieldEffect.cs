namespace Relick.Core;

internal sealed class GalacticShieldEffect : CardEffect
{
    public override string CardId => CardCatalog.GalacticShield.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Escudo Galáctico necesita colocarse sobre el Relikario y no puede activarse con Relikia." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DeployGalacticShield();

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.GalacticShieldChoices(decision);

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveGalacticShieldChoice(decision, choice);
}
