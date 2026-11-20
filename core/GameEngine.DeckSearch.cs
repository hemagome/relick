namespace Relick.Core;

public sealed partial class GameEngine
{
    public IEnumerable<int> SearchableDeckCards(int actor) => Enumerable.Range(0, State.Players[actor].Deck.Count)
        .Where(i => CardCatalog.Get(State.Players[actor].Deck[i]).Kind != CardKind.Skill);

}
