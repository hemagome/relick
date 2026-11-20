namespace Relick.Core;

/// <summary>Huevo Azul Explosivo: destroy a non-skill on the field, then you may pay 200 PR to draw 1 card.</summary>
internal sealed class BlueEggEffect : CardEffect
{
    public const int DrawCost = 200;
    private const string Draw = "draw";

    public override string CardId => CardCatalog.BlueEgg.Id;

    private const string TargetInstruction = "Elige una no-habilidad del campo para destruir";


    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.NonSkillFieldCards().Any() ? null : "No hay no-habilidades en el campo para destruir.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (!ctx.NonSkillFieldCards().Any()) { ctx.Log($"{ctx.Card.Name} se resuelve sin objetivos disponibles."); return; }
        ctx.Ask(FieldDestruction.Target, DecisionKind.ChooseTarget, TargetInstruction, allowsCombos: true, announce: false);
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => decision.Step switch
    {
        FieldDestruction.Target => ctx.NonSkillFieldCards().Select(t => FieldDestruction.Card(ctx, t)).ToList(),
        Draw => (ctx.Player.Relikia >= DrawCost
            ? [new DecisionChoice { Label = $"Robar 1 carta · {DrawCost} PR", Value = 1, Primary = true,
                Public = $"paga {DrawCost} PR para robar con {ctx.Card.Name}." }]
            : Array.Empty<DecisionChoice>())
            .Append(new DecisionChoice { Label = "Continuar sin robar", Public = $"continúa sin robar con {ctx.Card.Name}." }).ToList(),
        _ => []
    };

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (decision.Step == FieldDestruction.Target) { FieldDestruction.Destroy(ctx, choice.Field!); return; }
        if (choice.Value != 1) return;
        ctx.Player.Relikia -= DrawCost;
        ctx.Log($"{ctx.Player.Name} paga {DrawCost} PR para robar una carta con {ctx.Card.Name}.");
        ctx.Draw(1);
    }

    public override void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next)
    {
        if (step == FieldDestruction.Destroyed)
            FieldDestruction.Finish(ctx, next, ctx.Then(Draw));
        else if (step == Draw && ctx.Player.Relikia >= DrawCost)
            ctx.Ask(Draw, DecisionKind.Confirm, $"Decide si pagas {DrawCost} PR para robar una carta.", announce: false);
    }
}
