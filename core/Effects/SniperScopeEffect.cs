namespace Relick.Core;

/// <summary>
/// Mira de Sniper (equipment): each time its holder receives damage, place a counter on it; with 3 counters it is
/// destroyed and the damage goes straight to health. Equipping it when discarded and its damage bonus are still in
/// the engine.
/// </summary>
internal sealed class SniperScopeEffect : CardRules, IDamageTrigger
{
    private const int CounterLimit = 3;

    public override string CardId => CardCatalog.Sniper.Id;

    public DamageStep Step => DamageStep.Counters;

    public bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed)
    {
        if (ctx.ActiveEquipment()?.Id != CardId) return false;
        if (++ctx.Player.EquipmentCounters < CounterLimit) return false;
        // Destroying the only equipment slot also rules out Tótem de Salvación, so the damage skips that step.
        ctx.DestroyEquipment(ctx.HealthDamage(damage, completed));
        return true;
    }
}
