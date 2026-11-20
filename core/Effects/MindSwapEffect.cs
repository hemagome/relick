namespace Relick.Core;

/// <summary>Cambio de Mentes: swap your whole hand (even empty) with your opponent's. Not on the first turn of the game.</summary>
internal sealed class MindSwapEffect : CardEffect
{
    public override string CardId => CardCatalog.MindSwap.Id;
    public override void Resolve(GameEngine.EffectContext ctx) => ctx.SwapHands();
}
