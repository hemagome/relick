namespace Relick.Core;

internal sealed class ComboParryEffect : ICardEffect
{
    public string CardId => CardCatalog.ComboParry.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => ctx.State.Phase switch
    {
        Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final or Phase.AttackRerollChoice or Phase.DieRerollChoice or
        Phase.DefenseChoice or Phase.EffectDamageChoice or Phase.MagnetChoice or Phase.CodeResponse or Phase.Decision => null,
        Phase.ErrorResponse when ctx.CanParryActivation() => null,
        _ => "Completa la resolución pendiente antes de jugar Combo Parry."
    };

    public void Resolve(GameEngine.EffectContext ctx)
    {
        var rival = 1 - ctx.Actor;
        if (ctx.State.Players[rival].Hand.Count == 0) { ctx.FinishComboParry(); return; }
        ctx.Ask("reveal", DecisionKind.Confirm, "Revisa la mano revelada y confirma para devolver los COMBO al mazo.", showsHandOf: rival);
    }

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        [new() { Label = "Continuar", RequiresAcknowledgement = true, Public = "confirma la resolución de Combo Parry." }];

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) => ctx.FinishComboParry();

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
