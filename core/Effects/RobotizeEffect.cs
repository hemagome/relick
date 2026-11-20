namespace Relick.Core;

/// <summary>
/// Robotizar: placed on top of the Relikario. At the start of the action stage, each active copy shows the top card of
/// the deck; a code may go to the hand or the exchange, anything else goes to the exchange. Each active copy also
/// subtracts 1 from the damage its owner receives.
/// </summary>
internal sealed class RobotizeEffect : CardEffect, ITurnTrigger, IDamageTrigger
{
    private const string Top = "top";
    /// <summary>The step that handles the next copy; duels saved with the old continuation kind resume here too.</summary>
    internal const string TopCards = "top-cards";

    public override string CardId => CardCatalog.Robotize.Id;

    public TurnMoment Moment => TurnMoment.ActionStart;
    // After Perro Fantasma.
    public int Order => 1;

    DamageStep IDamageTrigger.Step => DamageStep.Reduce;

    public int Modify(GameEngine.EffectContext ctx, DamageEvent damage)
    {
        var reduced = Math.Max(0, damage.Amount - ctx.ActiveAttachedCopies());
        if (reduced == damage.Amount) return damage.Amount;
        ctx.NotifyGuard(damage.Attacker, damage.Amount, reduced, blocked: false);
        ctx.State.LastCombat += $" · Robotizar: {reduced} de daño";
        ctx.Log($"{ctx.Player.Name} reduce el daño de {damage.Amount} a {reduced} con Robotizar.");
        return reduced;
    }

    /// <summary>Counts the active copies once, at the start; a copy destroyed meanwhile still shows its card.</summary>
    public bool Fire(GameEngine.EffectContext ctx)
    {
        var count = ctx.ActiveAttachedCopies();
        if (count == 0) return false;
        var next = ctx.Then(TopCards);
        next.Count = count;
        NextTopCard(ctx, next);
        return true;
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => NextTopCard(ctx, next);

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
        ResolveTopCard(ctx, decision.Continuation!, toHand: choice.Value == 1);

    private static void NextTopCard(GameEngine.EffectContext ctx, EngineContinuation next)
    {
        if (next.Count == 0) { ctx.FinishTurnTrigger(TurnMoment.ActionStart); return; }
        if (ctx.Player.Deck.Count == 0) { ctx.Exhaust(next); return; }
        next.Count--;
        if (CardCatalog.Get(ctx.Player.Deck[0]).Kind == CardKind.Code)
            ctx.Ask(Top, DecisionKind.Confirm, "Elige el destino de la carta del tope de tu mazo.", continuation: next);
        else ResolveTopCard(ctx, next, toHand: false);
    }

    private static void ResolveTopCard(GameEngine.EffectContext ctx, EngineContinuation next, bool toHand)
    {
        if (toHand)
        {
            ctx.Log($"{ctx.Player.Name} añade la carta del tope de su mazo a su mano con Robotizar.");
            ctx.MoveTopCardToHand(next);
            return;
        }
        if (ctx.Player.Exchange.Count >= 2)
        {
            // Nothing moved, so every remaining copy would find this same card and the same full exchange: ending the
            // chain here gives the same result as letting each copy try.
            ctx.Log($"{ctx.Player.Name} tiene ambos espacios de canje ocupados.");
            ctx.FinishTurnTrigger(TurnMoment.ActionStart);
            return;
        }
        ctx.SendTopCardToExchange(next);
    }
}
