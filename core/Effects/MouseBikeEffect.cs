namespace Relick.Core;

/// <summary>
/// Vehículo Moto-Ratón (equipment): at the start of its owner's turn, choose one extra card in the draw or one extra
/// action.
/// </summary>
internal sealed class MouseBikeEffect : CardRules, ITurnTrigger
{
    public override string CardId => CardCatalog.MouseBike.Id;

    public TurnMoment Moment => TurnMoment.TurnStart;
    public int Order => 0;

    public bool Fire(GameEngine.EffectContext ctx)
    {
        if (ctx.ActiveEquipment()?.Id != CardId) return false;
        ctx.State.Phase = Phase.Draw;
        ctx.Ask("turn-start", DecisionKind.Confirm, "Elige la bonificación de Moto-Ratón para este turno.");
        return true;
    }

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
    [
        new() { Label = "Robar una carta extra", Value = 1, Public = "elige robar una carta extra con Moto-Ratón." },
        new() { Label = "Ganar una acción extra", Public = "elige ganar una acción extra con Moto-Ratón." }
    ];

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value == 0) ctx.State.ActionsLeft++;
        // The draw closes the turn start, and it depends on this answer, so the module starts it itself. A later
        // turn-start trigger would need the extra card passed on instead (docs/CARD_TRIGGERS.md).
        ctx.DrawAtTurnStart(extraCards: choice.Value);
    }
}
