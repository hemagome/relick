namespace Relick.Core;

public sealed partial class GameEngine
{
    public IEnumerable<int> SearchableDeckCards(int actor) => Enumerable.Range(0, State.Players[actor].Deck.Count)
        .Where(i => CardCatalog.Get(State.Players[actor].Deck[i]).Kind != CardKind.Skill);

    public CommandResult ResolveDeckSearch(int actor, int deckIndex)
    {
        // Código Konamy asks through a decision; this command from recorded duels still answers it.
        if (ResolveDecisionWhere(actor, CardCatalog.KonamyCode.Id, c => c.Value == deckIndex) is { } answered) return answered;
        return CommandResult.Fail("No te corresponde buscar una carta en el mazo.");
    }
}
