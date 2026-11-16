namespace Relick.Core;

/// <summary>Código Konamy: search your deck for a non-skill and add it to your hand.</summary>
internal sealed class KonamyCodeEffect : ICardEffect
{
    private const string Pick = "pick";

    public string CardId => CardCatalog.KonamyCode.Id;

    public string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.NonSkillDeckCards().Any() ? null : "No hay no-habilidades en tu mazo.";

    public void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.NonSkillDeckCards().Any()) { ctx.Log($"{ctx.Card.Name} se resuelve sin no-habilidades disponibles en el mazo."); return; }
        ctx.Ask(Pick, DecisionKind.ChooseCard, "Elige una no-habilidad de tu mazo", allowsCombos: true);
    }

    // The deck is private: these choices only reach the searching player, and the public line names no card.
    public IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.NonSkillDeckCards().Select(i => new DecisionChoice
        {
            Label = $"Añadir {CardCatalog.Get(ctx.Player.Deck[i]).Name} · mazo {i + 1}", Button = "Añadir a mano",
            CardId = ctx.Player.Deck[i], Value = i, Public = "añade una carta del mazo a su mano."
        }).ToList();

    public void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.Announce($"{ctx.Player.Name} añade una no-habilidad a su mano con {ctx.Card.Name}.");
        // The search is over before the card moves, so emptying the deck recycles outside this activation.
        ctx.EndActivation();
        ctx.MoveDeckCardToHand(choice.Value);
    }

    public void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}
