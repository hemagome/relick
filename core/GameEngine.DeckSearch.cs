namespace Relick.Core;

public sealed partial class GameEngine
{
    public IEnumerable<int> SearchableDeckCards(int actor) => Enumerable.Range(0, State.Players[actor].Deck.Count)
        .Where(i => CardCatalog.Get(State.Players[actor].Deck[i]).Kind != CardKind.Skill);

    public CommandResult ResolveDeckSearch(int actor, int deckIndex)
    {
        if (State.Phase != Phase.DeckSearchChoice || actor != State.DecisionPlayer ||
            _activeActivation is not { Kind: ActivationKind.Code } frame || frame.Actor != actor || !CardCatalog.Get(frame.CardId).SearchNonSkill)
            return CommandResult.Fail("No te corresponde buscar una carta en el mazo.");
        if (!SearchableDeckCards(actor).Contains(deckIndex))
            return CommandResult.Fail("Elige una carta que no sea habilidad de tu mazo.");

        var p = State.Players[actor];
        State.Phase = frame.ResumePhase;
        // The selection is private, just like drawing a card. Keep its identity out of public messages.
        State.LastCombat = $"{p.Name} añade una no-habilidad a su mano con {CardCatalog.Get(frame.CardId).Name}.";
        Log(State.LastCombat);
        _activeActivation = null;
        MoveDeckCard(p, deckIndex, false);
        return CommandResult.Ok();
    }
}
