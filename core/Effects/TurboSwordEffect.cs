namespace Relick.Core;

/// <summary>
/// Turbo Sword (skill), defense: before receiving damage, each taught copy with fewer than 2 counters rolls 1D6, in
/// skill-slot order. 5: cancel the damage and place a counter; 6: cancel it and return half; otherwise the next copy
/// tries. The die's outcome, and the attack damage die, are still resolved in the engine (GameEngine.Osamu.cs).
/// </summary>
internal sealed class TurboSwordEffect : CardRules, IDamageTrigger
{
    private const int MaxCounters = 2;

    public override string CardId => CardCatalog.TurboSword.Id;

    // After the equipment's guard, before Robotizar.
    public DamageStep Step => DamageStep.Respond;

    public bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed)
    {
        if (DefenseRoll(ctx.State, ctx.Actor, damage, completed, afterPosition: -1, ctx.EffectsSuppressed) is not { } roll) return false;
        ctx.RollResolvedByEngine(roll);
        return true;
    }

    /// <summary>The defense die of the next eligible copy after <paramref name="afterPosition"/>, or null when none can roll.</summary>
    internal static DieEffect? DefenseRoll(GameState state, int target, DamageEvent damage, EngineContinuation? completed, int afterPosition,
        Func<int, FieldZone, int, bool> suppressed)
    {
        var p = state.Players[target];
        var slot = Enumerable.Range(0, p.Skills.Count).OrderBy(p.SkillPosition).FirstOrDefault(i =>
            p.SkillPosition(i) > afterPosition && p.Skills[i] == CardCatalog.TurboSword.Id &&
            p.CountersOnSkill(i) < MaxCounters && !suppressed(target, FieldZone.Skill, i), -1);
        return slot < 0 ? null : new DieEffect
        {
            Purpose = DiePurpose.TurboSwordDefense, Actor = target, SkillId = CardCatalog.TurboSword.Id,
            Slot = p.SkillPosition(slot), AttackDie = damage.AttackDie, IncomingDamage = damage.Amount, Next = completed
        };
    }
}
