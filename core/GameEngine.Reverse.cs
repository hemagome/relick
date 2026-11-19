namespace Relick.Core;

public sealed partial class GameEngine
{
    private void OnOwnDieRerolled(int actor)
    {
        var p = State.Players[actor];
        for (var slot = 0; slot < p.Skills.Count; slot++)
        {
            var skill = CardCatalog.Get(p.Skills[slot]);
            if (skill.DamagePerReroll <= 0 || EffectsSuppressed(actor, FieldZone.Skill, slot)) continue;
            var counters = p.CountersOnSkill(slot) + 1;
            p.SkillCounters[slot] = counters;
            if (State.PendingAttackRoll is { } attack && attack.Attacker == actor && attack.SkillSlot == slot)
                State.PendingAttackRoll = attack with { NormalDamage = skill.NormalDamageAt(counters), CriticalDamage = skill.CriticalDamageAt(counters) };
            Log($"{p.Name} aumenta {skill.Name} (espacio {p.SkillPosition(slot) + 1}): {skill.NormalDamageAt(counters)} normal, {skill.CriticalDamageAt(counters)} crítico.");
        }
    }

    public bool CanRerollLearningDie(int actor) => State.Phase == Phase.DieRerollChoice &&
        State.PendingDieRoll is { Purpose: DiePurpose.SkillLearning, LearningRerolls: < 2 } pending && pending.Player == actor;

    public CommandResult RerollLearningDie(int actor)
    {
        if (!CanRerollLearningDie(actor)) return CommandResult.Fail("No puedes relanzar el dado de aprendizaje.");
        var pending = State.PendingDieRoll!;
        OnOwnDieRerolled(actor);
        var die = Roll("Reverse Shuffle · Aprendizaje");
        State.PendingDieRoll = pending with { Die = die, LearningRerolls = pending.LearningRerolls + 1, WasRerolled = true, Modified = true };
        Log($"{State.Players[actor].Name} relanza por Reverse Shuffle ({pending.LearningRerolls + 1}/2): {die}.");
        if (!CanRespondToEffectDie()) return ResolveDieReroll(actor, false);
        return CommandResult.Ok();
    }

    private void RemoveLearnedSkill(int owner, int slot, FieldTarget? movedTo = null)
    {
        var p = State.Players[owner]; var id = p.Skills[slot];
        var positions = Enumerable.Range(0, p.Skills.Count).Where(index => index != slot).Select(p.SkillPosition).ToArray();
        ChangeZahiraHosts(owner, FieldZone.Skill, slot, movedTo: movedTo);
        p.Skills.RemoveAt(slot);
        p.SkillPositions = positions.Where((position, index) => position != index).Any()
            ? positions.Select((position, index) => (position, index)).ToDictionary(c => c.index, c => c.position) : null;
        p.SkillCounters = p.SkillCounters.Where(kv => kv.Key != slot).ToDictionary(kv => kv.Key > slot ? kv.Key - 1 : kv.Key, kv => kv.Value);
        if (State.PendingAttackRoll is { } attack && attack.Attacker == owner && attack.SkillSlot is int source)
            State.PendingAttackRoll = attack with { SkillSlot = source == slot ? null : source > slot ? source - 1 : source };
        ShiftSavedAttackSlots(State.PendingDestruction?.Completed, owner, slot);
        ShiftSavedAttackSlots(_pendingDieEffect?.Next, owner, slot);
        if (movedTo == null) SendToMemory(p, id, CardZone.Skill, slot);
        p.RemoveCorruptionSlots(CardZone.Skill, slot);
    }

    private static void ShiftSavedAttackSlots(EngineContinuation? next, int owner, int slot)
    {
        if (next == null) return;
        if (next.Attack is { } attack && attack.Attacker == owner && attack.SkillSlot is int source)
            next.Attack = attack with { SkillSlot = source == slot ? null : source > slot ? source - 1 : source };
        ShiftSavedAttackSlots(next.Next, owner, slot);
        ShiftSavedAttackSlots(next.SavedDieEffect?.Next, owner, slot);
    }
}
