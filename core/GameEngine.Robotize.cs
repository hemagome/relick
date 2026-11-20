namespace Relick.Core;

public sealed partial class GameEngine
{
    private IEnumerable<FieldTarget> ActiveRobotizeCopies(int actor)
    {
        var attached = State.Players[actor].AttachedCards;
        for (var slot = 0; slot < attached.Count; slot++)
            if (CardCatalog.TextId(attached[slot]) == CardCatalog.Robotize.Id && !EffectsSuppressed(actor, FieldZone.Attached, slot))
                yield return new(actor, FieldZone.Attached, slot, attached[slot]);
    }

    private int ReduceDamageWithRobotize(int target, int amount, int attackDie)
    {
        var reduced = Math.Max(0, amount - ActiveRobotizeCopies(target).Count());
        if (reduced == amount) return amount;
        NotifyGuard(target, attackDie > 0 ? 1 - target : null, CardCatalog.Robotize.Id, amount, reduced, blocked: false);
        State.LastCombat += $" · Robotizar: {reduced} de daño";
        Log($"{State.Players[target].Name} reduce el daño de {amount} a {reduced} con Robotizar.");
        return reduced;
    }

    private void ResolveRobotizeAtActionStart(int actor)
    {
        var count = ActiveRobotizeCopies(actor).Count();
        if (count > 0) ContinueRobotizeActionStart(new EngineContinuation
            { Kind = ContinuationKind.RobotizeActionStart, Actor = actor, Count = count });
    }

    private void ContinueRobotizeActionStart(EngineContinuation next)
    {
        if (next.Count == 0) { CompletePendingPlay(); return; }
        var player = State.Players[next.Actor];
        if (player.Deck.Count == 0) { Exhaust(player, next); return; }
        next.Count--;
        if (CardCatalog.Get(player.Deck[0]).Kind == CardKind.Code)
        {
            AskDecision(new PendingDecision
            {
                Source = CardCatalog.Robotize.Id, Step = "top", Decider = next.Actor, Kind = DecisionKind.Confirm,
                Instruction = "Elige el destino de la carta del tope de tu mazo.", Continuation = next
            });
        }
        else ResolveRobotizeTopCard(next.Actor, next, toHand: false);
    }

    private void ResolveRobotizeTopCard(int actor, EngineContinuation next, bool toHand)
    {
        var player = State.Players[actor];
        if (toHand)
        {
            Log($"{player.Name} añade la carta del tope de su mazo a su mano con Robotizar.");
            MoveTopCard(player, bonus: false, next);
            return;
        }
        if (player.Exchange.Count >= 2)
        {
            Log($"{player.Name} tiene ambos espacios de canje ocupados.");
            CompletePendingPlay();
            return;
        }

        var slot = player.Exchange.Count;
        var id = player.Deck[0];
        if (player.CorruptsOnMemory(CardZone.Deck, 0)) player.MarkForCorruption(CardZone.Exchange, slot);
        player.Deck.RemoveAt(0);
        player.RemoveCorruptionSlots(CardZone.Deck, 0);
        NotifyPileMove(player, id, CardZone.Deck, slot, CardZone.Exchange);
        AddToExchange(player, id);
        var after = new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = actor, Next = next };
        if (player.Deck.Count == 0) Exhaust(player, after);
        else Continue(after);
    }
}
