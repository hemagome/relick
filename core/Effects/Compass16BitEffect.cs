namespace Relick.Core;

internal sealed class Compass16BitEffect : CardEffect
{
    public override string CardId => CardCatalog.Compass16Bit.Id;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        ctx.ShuffleBonusZoneIntoDecks();
        if (ctx.Player.Deck.Count == 0) return;

        ctx.Log($"{ctx.Player.Name} renueva su zona de Bonus con {ctx.Card.Name}.");
        ctx.Ask("pick", DecisionKind.ChooseCard, "Elige una carta de tu mazo para colocarla como Bonus.");
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.Player.Deck.Select((id, index) => (Id: id, Index: index))
            .OrderBy(card => card.Id, StringComparer.Ordinal)
            .Select(card => new DecisionChoice
            {
                Label = $"Colocar {CardCatalog.Get(card.Id).Name} como Bonus",
                Button = "Colocar como Bonus", CardId = card.Id, Value = card.Index,
                Public = "elige una carta de su mazo para colocarla como Bonus."
            }).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.EndActivation();
        ctx.MoveDeckCardToBonus(choice.Value);
    }
}
