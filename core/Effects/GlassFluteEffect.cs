namespace Relick.Core;

internal sealed class GlassFluteEffect : CardEffect
{
    public override string CardId => CardCatalog.GlassFlute.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.ReturnableFieldCards().Any() ? null : "Necesitas una carta del campo que pueda salir.";

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        if (ctx.ReturnableFieldCards().Any())
            ctx.Ask("target", DecisionKind.ChooseTarget, "Elige una carta del campo para enviarla a la mano de su dueño.");
        else ctx.Log("Flauta de Vidrio se resuelve sin objetivos disponibles.");
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        ctx.ReturnableFieldCards().Select(target => new DecisionChoice
        {
            Label = $"Devolver {ctx.FieldName(target)} · {ctx.FieldPlace(target)}",
            Button = "Devolver", Field = target, CardId = target.CardId,
            Public = $"elige devolver {ctx.FieldName(target)} a la mano de su dueño."
        }).ToArray();

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) =>
        ctx.ReturnFieldCard(choice.Field!);
}
