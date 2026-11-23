namespace Relick.Core;

public enum ExchangeMoment { EnteredExchange, RefilledBonus }
public sealed record ExchangeTrigger(int Actor, string CardId, ExchangeMoment Moment);

/// <summary>Effects triggered when their card enters an exchange slot or the replacement Bonus of a completed exchange.</summary>
internal interface IExchangeTrigger
{
    bool Fire(GameEngine.EffectContext ctx, ExchangeMoment moment);
}

public sealed partial class GameEngine
{
    private void QueueExchangeTrigger(int actor, string cardId, ExchangeMoment moment, FieldZone zone, int slot)
    {
        var id = CardCatalog.TextId(cardId);
        if (CardEffects.For(id) is IExchangeTrigger && !EffectsSuppressed(actor, zone, slot))
            (State.ExchangeTriggerQueue ??= []).Add(new(actor, id, moment));
    }

    private bool TryPresentExchangeTrigger()
    {
        var canFire = State.Phase.IsTurnStage() || State.Phase == Phase.WdResolving;
        if (!canFire || _activeActivation != null || State.PendingAttack != null || State.PendingSniper != null)
            return false;

        while (State.ExchangeTriggerQueue is { Count: > 0 } queue)
        {
            var next = queue[0];
            queue.RemoveAt(0);
            if (queue.Count == 0) State.ExchangeTriggerQueue = null;
            if (CardEffects.For(next.CardId) is IExchangeTrigger trigger && trigger.Fire(EffectFor(next.Actor, next.CardId), next.Moment))
                return true;
        }
        return false;
    }
}
