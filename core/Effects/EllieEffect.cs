using System.Text.RegularExpressions;

namespace Relick.Core;

internal sealed class EllieEffect : ICardEffect
{
    private const string Pick = "pick";
    private const int MaxCards = 2;
    private static readonly Regex LifeWord = new(@"\bvidas?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string CardId => CardCatalog.Ellie.Id;

    internal static bool Matches(CardDefinition card) =>
        !string.Equals(card.Name, CardCatalog.Ellie.Name, StringComparison.OrdinalIgnoreCase) &&
        (LifeWord.IsMatch(card.Name) || LifeWord.IsMatch(card.Text));

    private static IEnumerable<int> MatchingSlots(PlayerState player) =>
        Enumerable.Range(0, player.Deck.Count).Where(slot => Matches(CardCatalog.Get(player.Deck[slot])));

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        MatchingSlots(ctx.Player).Any() ? null : "Tu mazo necesita otra carta con la palabra «vida» o «vidas».";

    public void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        var count = MatchingSlots(ctx.Player).Take(MaxCards).Count();
        if (count > 0) AskForCard(ctx, count);
    }

    private static void AskForCard(GameEngine.EffectContext ctx, int remaining) =>
        ctx.Ask(Pick, DecisionKind.ChooseCard,
            remaining == MaxCards ? "Elige la primera de dos cartas con «vida» o «vidas»" : "Elige una carta con «vida» o «vidas»",
            value: remaining);

    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        MatchingSlots(ctx.Player).Select(slot => new DecisionChoice
        {
            Label = $"Añadir {CardCatalog.Get(ctx.Player.Deck[slot]).Name} · mazo {slot + 1}",
            Button = "Añadir a mano", CardId = ctx.Player.Deck[slot], Value = slot,
            Public = "añade una carta del mazo a su mano con Ellie."
        }).ToArray();

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.Announce($"{ctx.Player.Name} añade una carta de su mazo a su mano con Ellie.");
        if (decision.Value == MaxCards)
        {
            ctx.MoveDeckCardToHand(choice.Value, ctx.Then(Pick));
            return;
        }
        ctx.EndActivation();
        ctx.MoveDeckCardToHand(choice.Value);
    }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) => AskForCard(ctx, 1);
}
