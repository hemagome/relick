namespace Relick.Core;

/// <summary>
/// Robotizar: placed on top of the Relikario. Each copy reduces damage by 1 and, at the start of the action stage,
/// shows the top card of the deck; a code may go to the hand or the exchange. Those triggers live in the engine.
/// </summary>
internal sealed class RobotizeEffect : CardEffect
{
    public override string CardId => CardCatalog.Robotize.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Robotizar necesita colocarse encima del Relikario y no puede activarse con Relikia." : null;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        var attached = ctx.Player.AttachedCards;
        var id = ctx.InstallActivationCard(CardZone.Attached, attached.Count);
        ctx.NotifyPileMove(CardCatalog.Robotize.Id, CardZone.Activation, destination: CardZone.Attached);
        attached.Add(id);
        ctx.Announce($"{ctx.Player.Name} coloca Robotizar encima de su Relikario.");
    }

    // The engine opens this decision at the start of the action stage, when the top card of the deck is a code.
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision)
    {
        var card = CardCatalog.Get(ctx.Player.Deck[0]);
        var hand = new DecisionChoice
        {
            Label = $"Añadir {card.Name} a la mano", Button = "Añadir a mano", CardId = card.Id, Value = 1, Primary = true,
            Public = "añade la carta del tope de su mazo a su mano con Robotizar."
        };
        return ctx.Player.Exchange.Count >= 2 ? [hand] : [hand, new DecisionChoice
        {
            Label = $"Enviar {card.Name} al canje", Button = "Enviar al canje", CardId = card.Id,
            Public = "envía la carta del tope de su mazo al canje con Robotizar."
        }];
    }

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ResolveRobotizeTopCard(decision.Continuation!, choice.Value == 1);
}
