using Relick.Core;

static class SelectedCards
{
    public static IEnumerable<int> For(DuelSnapshot state, DuelOption choice, int start = 0) =>
        choice.Move.Kind == DuelMoveKind.GoldenTotem
            ? state.Players[state.MySeat].Hand.Select((id, index) => (id, index))
                .Where(c => c.id == CardCatalog.Totem.Id).Take(choice.RequiredDiscards).Select(c => c.index)
            : Enumerable.Range(start, choice.RequiredDiscards);
}
