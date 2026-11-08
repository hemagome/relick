namespace Relick.Core;

public sealed partial class GameEngine
{
    public CommandResult ResolveAttackReroll(int actor, bool reroll)
    {
        var error = ValidateTurn(actor, Phase.AttackRerollChoice);
        if (error != null) return CommandResult.Fail(error);
        var pending = State.PendingAttackRoll;
        if (pending == null || pending.Attacker != actor) return CommandResult.Fail("No hay una tirada de ataque pendiente.");
        var p = State.Active;
        if (reroll && !CanUseYunoOnPendingDie(actor))
            return CommandResult.Fail("No puedes relanzar este dado con la habilidad del Relikario.");
        if (!reroll)
        {
            Log($"{p.Name} conserva su dado de ataque: {pending.Die}.");
            if (!pending.Modified) RegisterNaturalSix(p, pending.Die);
            FinishAttackRoll(pending.Die, pending.WasRerolled);
            return CommandResult.Ok();
        }
        p.AttackRerollUsedThisTurn = true;
        var rolled = RollAttack(out _, $"Yuno Lv {p.Level} · Relanzamiento", p.Relikario.ConvertRerolledSix);
        var die = rolled == 6 && p.Relikario.ConvertRerolledSix ? 5 : rolled;
        var count = pending.Rerolls + 1;
        Log($"{p.Name} activa Yuno Lv {p.Level}: relanza {pending.Die} y obtiene {rolled}." +
            (rolled != die ? " El 6 se convierte en 5; no es crítico." : ""));
        State.PendingAttackRoll = pending with { Die = die, Rerolls = count, WasRerolled = true, Modified = true, YunoStopped = die == 1 };
        State.LastAttackDie = die;
        if (CanRespondToAttackDie())
            State.LastCombat = $"{p.Name} · Dado {die} · Elige si conservas el resultado";
        else FinishAttackRoll(die, true);
        return CommandResult.Ok();
    }

    private void FinishAttackRoll(int die, bool rerolled = false)
    {
        var pending = State.PendingAttackRoll!;
        State.PendingAttackRoll = null;
        State.LastAttackDie = die;
        State.Phase = Phase.Combat;
        var p = State.Players[pending.Attacker];
        var skill = CardCatalog.Get(pending.SkillId);
        if (skill.Id == CardCatalog.Magnet.Id && die is 4 or 5)
            RollWithRelikario(p, DiePurpose.MagnetDamage, new DieEffect { Purpose = DiePurpose.MagnetDamage, Actor = pending.Attacker, SkillId = skill.Id, AttackDie = die, Rerolled = rerolled });
        else ResolveAttackResult(pending.Attacker, skill, die, skill.NormalDamage, rerolled);
    }

    private void ResolveAttackResult(int actor, CardDefinition skill, int die, int normal, bool rerolled)
    {
        var p = State.Players[actor];
        var damage = DamageFor(die, normal, skill.CriticalDamage);
        if (die >= 4 && p.Equipment is { } equipment) damage += CardCatalog.Get(equipment).DamageBonus;
        var outcome = die < 4 ? "Fallido" : die == 4 ? "Reducido" : die == 5 ? "Normal" : "Crítico";
        State.LastCombat = $"{p.Name} · {outcome} · {damage} de daño" +
            (rerolled ? $" · Dado final: {die}" : "");
        Log($"{p.Name} usa {skill.Name} contra {State.Players[1 - actor].Name}: dado {die}, {outcome.ToLowerInvariant()}, {damage} de daño.");
        if (die >= 4) AttackPerformed?.Invoke(actor);
        if (damage <= 0) return;
        p.Relikia += skill.Reward;
        Log($"{p.Name} obtiene {skill.Reward} PR.");
        State.PendingAttack = new(actor, skill.Id, damage, die == 6);
        if (CanDefend(1 - actor, die == 6)) State.Phase = Phase.DefenseChoice;
        else ResolveAttackDamage(false);
    }
}
