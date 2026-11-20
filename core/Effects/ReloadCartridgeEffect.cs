namespace Relick.Core;

/// <summary>
/// Cartucho de Re-carga: both players discard their hands (at least 1 card each) and draw 4. Not on the first turn.
/// The discards and draws run through the engine's reload sequence, which owns the Mira de Sniper triggers in between.
/// </summary>
internal sealed class ReloadCartridgeEffect : CardEffect
{
    public override string CardId => CardCatalog.ReloadCartridge.Id;

    // While in hand the activating copy still counts, so the actor needs a second card.
    public override string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) =>
        ctx.Player.Hand.Count <= (excludedHand < 0 ? 0 : 1) || ctx.State.Players[1 - ctx.Actor].Hand.Count == 0
            ? "Ambos jugadores necesitan al menos una carta que descartar, además del Cartucho que se activa." : null;

    public override void Resolve(GameEngine.EffectContext ctx) => ctx.ReloadBothHands();
}
