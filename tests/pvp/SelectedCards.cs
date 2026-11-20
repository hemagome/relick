using Relick.Core;

static class SelectedCards
{
    public static IEnumerable<int> For(DuelSnapshot state, DuelOption choice, int start = 0) =>
        Enumerable.Range(start, choice.RequiredDiscards);
}
