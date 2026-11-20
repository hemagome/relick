namespace Relick.Core;

/// <summary>
/// Equipment that lowers the damage its holder receives, at the Guard step of the damage chain: Hoguera prevents it,
/// Casco de Diamantes and Capirote Metálico subtract 2. Their other rules (healing on equip, breaking on a 1, the lost
/// action) are still in the engine.
/// </summary>
internal sealed class GuardEquipmentEffect : CardRules, IDamageTrigger
{
    public static GuardEquipmentEffect Bonfire { get; } = new(CardCatalog.Bonfire.Id, prevents: true);
    public static GuardEquipmentEffect DiamondHelmet { get; } = new(CardCatalog.DiamondHelmet.Id, reduction: 2);
    public static GuardEquipmentEffect MetalHood { get; } = new(CardCatalog.MetalHood.Id, reduction: 2);

    private readonly bool _prevents;
    private readonly int _reduction;

    private GuardEquipmentEffect(string cardId, bool prevents = false, int reduction = 0)
    {
        CardId = cardId; _prevents = prevents; _reduction = reduction;
    }

    public override string CardId { get; }

    public DamageStep Step => DamageStep.Guard;

    public int Modify(GameEngine.EffectContext ctx, DamageEvent damage)
    {
        if (ctx.ActiveEquipment()?.Id != CardId) return damage.Amount;
        var reduced = _prevents ? 0 : Math.Max(0, damage.Amount - _reduction);
        ctx.NotifyGuard(damage.Attacker, damage.Amount, reduced, blocked: _prevents);
        ctx.Log($"{ctx.Player.Name} reduce el daño de {damage.Amount} a {reduced} con {ctx.Card.Name}.");
        ctx.State.LastCombat += $" · {ctx.Card.Name}: {reduced} daño recibido";
        return reduced;
    }
}
