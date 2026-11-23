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
        if (reroll && !CanUseRelikarioOnPendingDie(actor))
            return CommandResult.Fail("No puedes relanzar este dado con la habilidad del Relikario.");
        if (!reroll)
        {
            Log($"{p.Name} conserva su dado de ataque: {pending.Die}.");
            if (!pending.Modified) RegisterNaturalSix(p, pending.Die);
            FinishAttackRoll(pending.Die, pending.WasRerolled);
            return CommandResult.Ok();
        }
        p.AttackRerollUsedThisTurn = true;
        OnOwnDieRerolled(actor);
        pending = State.PendingAttackRoll!;
        var rolled = RollAttack(out _, $"{p.Relikario.DisplayName} Lv {p.Level} · Relanzamiento", p.Relikario.ConvertRerolledSix, pending.SuperAttack);
        var die = rolled == 6 && p.Relikario.ConvertRerolledSix ? 5 : rolled;
        var count = pending.Rerolls + 1;
        Log($"{p.Name} activa {p.Relikario.DisplayName} Lv {p.Level}: relanza {pending.Die} y obtiene {rolled}." +
            (rolled != die ? " El 6 se convierte en 5; no es crítico." : ""));
        State.PendingAttackRoll = pending with { Die = die, Rerolls = count, WasRerolled = true, Modified = true, RerollStopped = die == 1 };
        State.LastAttackDie = die;
        State.LastCombat = $"{p.Name} · Dado {die} · Elige si conservas el resultado";
        ProcessAttackDie(die, new EngineContinuation { Kind = ContinuationKind.AttackDieReady, Actor = actor });
        return CommandResult.Ok();
    }

    private void ContinueAttackDie()
    {
        var pending = State.PendingAttackRoll!;
        State.Phase = Phase.AttackRerollChoice;
        if (CanRespondToAttackDie()) return;
        if (!pending.Modified) RegisterNaturalSix(State.Players[pending.Attacker], pending.Die);
        FinishAttackRoll(pending.Die, pending.WasRerolled);
    }

    private bool ResolveUnopposedDie() => State.Phase switch
    {
        Phase.AttackRerollChoice when State.PendingAttackRoll is { } attack && !CanRespondToAttackDie() =>
            ResolveAttackReroll(attack.Attacker, false).Success,
        Phase.DieRerollChoice when State.PendingDieRoll is { } effect && !CanRespondToEffectDie() =>
            ResolveDieReroll(effect.Player, false).Success,
        _ => false
    };

    private void FinishAttackRoll(int die, bool rerolled = false)
    {
        var pending = State.PendingAttackRoll!;
        State.PendingAttackRoll = null;
        State.LastAttackDie = die;
        State.Phase = Phase.Combat;
        pending = pending with { Die = die, WasRerolled = rerolled };
        if (die == 1 && TryBreakCardsOnOwnOne(pending.Attacker,
            new EngineContinuation { Kind = ContinuationKind.GhostAttackDie, Actor = pending.Attacker, Attack = pending })) return;
        if (die is >= 1 and <= 3 && OfferGhostGift(pending.Attacker, new EngineContinuation
            { Kind = ContinuationKind.GhostAttackDie, Actor = pending.Attacker, Attack = pending, Flag = true })) return;
        ResolveConfirmedAttackDie(pending);
    }

    private void ResolveConfirmedAttackDie(PendingAttackRoll pending)
    {
        if (OfferGoldAttackLetter(pending)) return;
        if (!OfferMultiDriveRetry(pending)) ResolveAfterSkillRetry(pending);
    }

    private void ResolveAfterSkillRetry(PendingAttackRoll pending)
    {
        if (!OfferOsamuAttackSearch(pending)) OfferIronwingBonus(pending);
    }

    private void ResolveSkillAttack(PendingAttackRoll pending)
    {
        var die = pending.Die;
        var p = State.Players[pending.Attacker];
        var skill = CardCatalog.Get(pending.SkillId);
        if (CardEffects.For(skill.Id) is IAttackDamageRoll roll && roll.TryRollDamage(EffectFor(pending.Attacker, skill.Id), pending)) return;
        if (RollTurboDamage(pending)) return;
        var attackDamageBonus = pending.SuperAttack ? CardCatalog.SuperAttack.DamageBonus : 0;
        if (pending.NormalDamage is int normal && pending.CriticalDamage is int critical)
            skill = skill with { NormalDamage = normal, CriticalDamage = critical };
        if (!pending.EffectsSuppressed && skill.Id == CardCatalog.GoldDigger.Id)
            skill = skill with { CriticalDamage = 1 + RelikarioLetterCount(p) };
        if (!pending.EffectsSuppressed && skill.Id == CardCatalog.WildDrive.Id)
            skill = skill with { CriticalDamage = Math.Max(4, p.Memory.Count(id => DeckRules.IsAttack(CardCatalog.Get(id)))) };
        if (!pending.EffectsSuppressed && skill.Id == CardCatalog.MultiDrive.Id)
        {
            var hand = p.Hand.Count;
            skill = skill with { NormalDamage = Math.Max(2, (hand + 1) / 2), CriticalDamage = Math.Max(4, hand), Reward = Math.Max(400, 100 * hand) };
        }
        if (skill.Id == CardCatalog.Magnet.Id && (die is 4 or 5 || die < 4 && HasStackedTotem(pending.Attacker)))
            RollWithRelikario(p, DiePurpose.MagnetDamage, new DieEffect { Purpose = DiePurpose.MagnetDamage, Actor = pending.Attacker,
                SkillId = skill.Id, AttackDie = die, Rerolled = pending.WasRerolled, AttackDamageBonus = attackDamageBonus });
        else ResolveAttackResult(pending.Attacker, skill, die, skill.NormalDamage, pending.WasRerolled, pending.EffectsSuppressed, attackDamageBonus);
    }

    private void ResolveAttackResult(int actor, CardDefinition skill, int die, int normal, bool rerolled, bool effectsSuppressed = false, int attackDamageBonus = 0)
    {
        var p = State.Players[actor];
        var converted = die < 4 && HasStackedTotem(actor);
        var damage = DamageFor(converted ? 5 : die, normal, skill.CriticalDamage);
        if (die is >= 4 and <= 6 || converted) damage = DamageWithEquipmentBonus(actor, damage + attackDamageBonus);
        var outcome = converted ? "Normal por Tótem de Salvación" : die is < 4 or > 6 ? "Fallido" : die == 4 ? "Reducido" : die == 5 ? "Normal" : "Crítico";
        var damageText = (die is >= 4 and <= 6 || converted) && !effectsSuppressed && CardEffects.For(skill.Id) is IAttackDamageEffect
            ? $"{damage} de daño base" : $"{damage} de daño";
        State.LastCombat = $"{p.Name} · {outcome} · {damageText}" +
            (attackDamageBonus > 0 ? " · Súper Ataque" : "") +
            (rerolled ? $" · Dado final: {die}" : "");
        Log($"{p.Name} usa {skill.Name} contra {State.Players[1 - actor].Name}: dado {die}, {outcome.ToLowerInvariant()}, {damageText}.");
        if (die is < 4 or > 6 && !converted) return;
        var reward = skill.Reward != CardCatalog.Get(skill.Id).Reward ? skill.Reward : (int?)null;
        State.PendingAttack = new(actor, skill.Id, damage, die == 6, RewardPending: true, NormalFromMiss: converted, EffectsSuppressed: effectsSuppressed, Reward: reward);
        if (!OfferFireDamage(actor)) PresentResolvedAttack();
    }

    private void PresentResolvedAttack()
    {
        var attack = State.PendingAttack!;
        AttackPerformed?.Invoke(attack.Attacker);
        if (CanDefend(1 - attack.Attacker, attack.Critical)) State.Phase = Phase.DefenseChoice;
        else ResolveAttackDamage(false);
    }
}
