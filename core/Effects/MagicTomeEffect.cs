namespace Relick.Core;

internal sealed class MagicTomeEffect : CardEffect
{
    public override string CardId => CardCatalog.MagicTome.Id;
    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        excludedHand < 0 ? "Tomo de Magia necesita colocarse sobre tu Relikario." : null;
    public override void Resolve(GameEngine.EffectContext ctx) => ctx.DeployMagicTome();
}
