namespace Relick.Core;

/// <summary>Código Konamy: search your deck for a non-skill and add it to your hand.</summary>
internal sealed class KonamyCodeEffect : CardEffect
{
    private const string Pick = "pick";
    private const string Instruction = "Elige una no-habilidad de tu mazo";

    /// <summary>The search as <c>Resolve</c> opens it; duels saved in the former deck search phase become this decision.</summary>
    internal static PendingDecision SearchDecision(int actor, Phase resumePhase) => new()
    {
        Source = CardCatalog.KonamyCode.Id, Step = Pick, Decider = actor, Kind = DecisionKind.ChooseCard,
        Instruction = Instruction, AllowsCombos = true, ResumePhase = resumePhase
    };

    public override string CardId => CardCatalog.KonamyCode.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.NonSkillDeckCards().Any() ? null : "No hay no-habilidades en tu mazo.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.NonSkillDeckCards().Any()) { ctx.Log($"{ctx.Card.Name} se resuelve sin no-habilidades disponibles en el mazo."); return; }
        ctx.Ask(Pick, DecisionKind.ChooseCard, Instruction, allowsCombos: true);
    }

    // The deck is private: these choices only reach the searching player, and the public line names no card.
    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.NonSkillDeckCards().Select(i => new DecisionChoice
        {
            Label = $"Añadir {CardCatalog.Get(ctx.Player.Deck[i]).Name} · mazo {i + 1}", Button = "Añadir a mano",
            CardId = ctx.Player.Deck[i], Value = i, Public = "añade una carta del mazo a su mano."
        }).ToList();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.Announce($"{ctx.Player.Name} añade una no-habilidad a su mano con {ctx.Card.Name}.");
        // The search is over before the card moves, so emptying the deck recycles outside this activation.
        ctx.EndActivation();
        ctx.MoveDeckCardToHand(choice.Value);
    }
}
