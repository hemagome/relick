namespace Relick.Core;

/// <summary>Senshuni: draw 2 cards.</summary>
internal sealed class SenshuniEffect : CardEffect
{
    public override string CardId => CardCatalog.Senshuni.Id;

    public override void Resolve(GameEngine.EffectContext ctx)
    {
        ctx.CloseErrorWindow();
        ctx.Draw(ctx.Card.Draw);
    }
}
