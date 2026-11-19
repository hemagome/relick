namespace Relick.Core;

internal sealed class GalacticShieldEffect : ICardEffect
{
    public string CardId => CardCatalog.GalacticShield.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Escudo Galáctico necesita colocarse sobre el Relikario y no puede activarse con Relikia." : null;

    public void Resolve(GameEngine.EffectContext ctx) => ctx.DeployGalacticShield();

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.GalacticShieldChoices(decision);

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveGalacticShieldChoice(decision, choice);

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
