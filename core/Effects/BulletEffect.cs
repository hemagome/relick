namespace Relick.Core;

internal sealed class BulletEffect : CardRules
{
    public override string CardId => CardCatalog.Bullet.Id;
    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => "Bala se resuelve al descartarla.";
    public override int DrawOnDiscard => 1;
}
