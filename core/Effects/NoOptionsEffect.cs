namespace Relick.Core;

internal sealed class NoOptionsEffect : CardEffect, ITargetResponse
{
    public override string CardId => CardCatalog.NoOptions.Id;

    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => ctx.CanRespondToTarget()
        ? null : "Solo puedes responder a un efecto rival que te seleccione a ti o a una carta que controlas.";

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DiscardHandExceptRandomCard(1 - ctx.Actor);
}
