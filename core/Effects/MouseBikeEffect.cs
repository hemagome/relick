namespace Relick.Core;

/// <summary>
/// Vehículo Moto-Ratón (equipment): at the start of its owner's turn, choose one extra card in the draw or one extra
/// action. The engine opens the decision at turn start; this module answers it.
/// </summary>
internal sealed class MouseBikeEffect : CardRules
{
    public override string CardId => CardCatalog.MouseBike.Id;

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
    [
        new() { Label = "Robar una carta extra", Value = 1, Public = "elige robar una carta extra con Moto-Ratón." },
        new() { Label = "Ganar una acción extra", Public = "elige ganar una acción extra con Moto-Ratón." }
    ];

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        if (choice.Value == 0) ctx.State.ActionsLeft++;
        ctx.DrawAtTurnStart(extraCards: choice.Value);
    }
}
