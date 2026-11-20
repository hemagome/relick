namespace Relick.Core;

public sealed partial class GameEngine
{
    /// <summary>Copies of <paramref name="cardId"/> on top of the Relikario whose effects are not suppressed.</summary>
    private IEnumerable<FieldTarget> ActiveAttachedCopies(int actor, string cardId)
    {
        var attached = State.Players[actor].AttachedCards;
        for (var slot = 0; slot < attached.Count; slot++)
            if (CardCatalog.TextId(attached[slot]) == cardId && !EffectsSuppressed(actor, FieldZone.Attached, slot))
                yield return new(actor, FieldZone.Attached, slot, attached[slot]);
    }

    private IEnumerable<FieldTarget> ActiveRobotizeCopies(int actor) => ActiveAttachedCopies(actor, CardCatalog.Robotize.Id);

}
