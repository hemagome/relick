namespace Relick.Core;

/// <summary>
/// Era Futurista (Area): whoever places it chooses who gets side A. Side A activates Robotizar from outside the game for
/// that player; side B enables the D10. The engine opens the decision when the Area enters or moves.
/// </summary>
internal sealed class FutureEraEffect : CardRules
{
    public override string CardId => CardCatalog.FutureEra.Id;

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        GameEngine.AreaSideChoices(ctx.Actor);

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.Player.AreaSideA = choice.Value;
        if (decision.Step == "moved" || ctx.EffectsSuppressed(ctx.Actor, FieldZone.Area))
        {
            ctx.CompletePendingPlay();
            return;
        }
        ctx.Log($"{ctx.PlayerName(choice.Value)} activa Robotizar de fuera de la partida con Era Futurista.");
        ctx.ActivateOutOfGameCopy(choice.Value, CardCatalog.Robotize.Id);
    }
}
