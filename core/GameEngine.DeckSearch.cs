namespace Relick.Core;

public sealed record PendingSearchActivation(Phase ResumePhase, EngineContinuation? Then, PendingSearchActivation? Parent);

public sealed partial class GameEngine
{
    public IEnumerable<int> SearchableDeckCards(int actor) => Enumerable.Range(0, State.Players[actor].Deck.Count)
        .Where(i => CardCatalog.Get(State.Players[actor].Deck[i]).Kind != CardKind.Skill);

    private (CardRules Card, IDeckSearchTrigger Trigger)? DeckSearchTrigger(int actor)
    {
        var player = State.Players[actor];
        if (player.SearchActivationUsed) return null;
        for (var slot = 0; slot < player.Skills.Count; slot++)
            if (CardEffects.For(player.Skills[slot]) is CardRules rules && rules is IDeckSearchTrigger trigger &&
                !EffectsSuppressed(actor, FieldZone.Skill, slot)) return (rules, trigger);
        return null;
    }

    private void MoveSearchedDeckCardToHand(PlayerState player, int deckIndex, EngineContinuation? then = null,
        bool mark = false, bool finishSearch = true)
    {
        var actor = Seat(player);
        if (DeckSearchTrigger(actor) != null && CardCatalog.Get(player.Deck[deckIndex]).Kind is CardKind.Code or CardKind.Equipment)
            (player.SearchedHandSlots ??= []).Add(player.Hand.Count);
        if (finishSearch && player.SearchedHandSlots != null)
            then = new EngineContinuation { Kind = ContinuationKind.DeckSearchFinished, Actor = actor, Next = then };
        MoveDeckCard(player, deckIndex, false, then, mark);
    }

    private void FinishDeckSearch(int actor, EngineContinuation? then)
    {
        var player = State.Players[actor];
        var slots = player.SearchedHandSlots?.Order().ToArray() ?? [];
        player.SearchedHandSlots = null;
        if (DeckSearchTrigger(actor) is { } entry && entry.Trigger.TryOffer(EffectFor(actor, entry.Card.CardId), slots, then)) return;
        Continue(then);
    }

    private bool CanActivateSearchedHandCard(int actor, int index)
    {
        var player = State.Players[actor];
        if (!State.Phase.IsTurnStage() || PlayableHandCard(player, index) is not { } id) return false;
        var card = CardCatalog.Get(id);
        return card.Kind == CardKind.Equipment ||
            card.Kind == CardKind.Code && CodeEffectUnavailable(player, card, excludedHand: index) == null;
    }

    private void ActivateSearchedHandCard(int actor, int index, EngineContinuation? then, bool onOpponent)
    {
        State.SearchActivation = new(State.Phase, then, State.SearchActivation);
        var card = CardCatalog.Get(State.Players[actor].Hand[index]);
        if (card.Kind == CardKind.Equipment) EquipHandCard(actor, index, onOpponent, actionCost: 0);
        else ActivateHandCode(actor, index, actionCost: 0);
    }

    private bool TryResumeSearchActivation()
    {
        if (_activeActivation != null || State.SearchActivation is not { } activation || State.Phase != activation.ResumePhase)
            return false;
        State.SearchActivation = activation.Parent;
        Continue(activation.Then);
        return true;
    }
}
