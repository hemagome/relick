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

    private int ReduceDamageWithRobotize(int target, int amount, int attackDie)
    {
        var reduced = Math.Max(0, amount - ActiveRobotizeCopies(target).Count());
        if (reduced == amount) return amount;
        NotifyGuard(target, attackDie > 0 ? 1 - target : null, CardCatalog.Robotize.Id, amount, reduced, blocked: false);
        State.LastCombat += $" · Robotizar: {reduced} de daño";
        Log($"{State.Players[target].Name} reduce el daño de {amount} a {reduced} con Robotizar.");
        return reduced;
    }
}
