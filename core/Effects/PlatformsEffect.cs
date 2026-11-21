namespace Relick.Core;

internal sealed class PlatformsEffect : CardRules, IAreaActionModifier
{
    public override string CardId => CardCatalog.Platforms.Id;

    public override IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) =>
        GameEngine.AreaSideChoices(ctx.Actor);

    public override void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice)
    {
        ctx.Player.AreaSideA = choice.Value;
        ctx.CompletePendingPlay();
    }

    public (int Extra, bool Blocked) ActionModifier(bool sideA, bool ownTurn) =>
        (sideA && ownTurn ? 1 : 0, !sideA && !ownTurn);
}
