namespace Relick.Core;

public sealed partial class GameEngine
{
    public int DefenseDiscardCost => State.PendingAttack?.Critical == true ? 4 : 3;

    public IEnumerable<int> AvailableDefenses(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length) return Array.Empty<int>();
        var p = State.Players[actor];
        if (p.Hand.Count < DefenseDiscardCost) return Array.Empty<int>();
        return Enumerable.Range(0, p.Skills.Count)
            .Where(i => p.Skills[i] == CardCatalog.Destiny.Id && p.CountersOnSkill(i) < 2 && !EffectsSuppressed(actor, FieldZone.Skill, i));
    }

    private bool CanDefend(int actor, bool critical) =>
        HasTurnBuffInHand(actor) ||
        ComboResponsesEnabled(actor) && State.Players[actor].Hand.Any(id => CardCatalog.Get(id).DodgeAttack && State.Players[actor].ActionsLeft >= CardCatalog.Get(id).ActionCost) || ActiveEquipment(actor)?.Id == CardCatalog.CardboardBox.Id ||
        (State.Players[actor].Hand.Count >= (critical ? 4 : 3) && AvailableDefenses(actor).Any());

    public CommandResult PassDefense(int actor)
    {
        if (State.Phase != Phase.DefenseChoice || State.DecisionPlayer != actor)
            return CommandResult.Fail("No te corresponde responder a este ataque.");
        ResolveAttackDamage(false);
        return CommandResult.Ok();
    }

    public CommandResult ResolveDefense(int actor, int skillSlot, IEnumerable<int> discards)
    {
        if (State.Phase != Phase.DefenseChoice || State.DecisionPlayer != actor)
            return CommandResult.Fail("No te corresponde responder a este ataque.");
        var indices = discards.ToArray();
        var p = State.Players[actor];
        if (!AvailableDefenses(actor).Contains(skillSlot) || indices.Length != DefenseDiscardCost ||
            indices.Distinct().Count() != indices.Length || indices.Any(i => !InHand(p, i)))
            return CommandResult.Fail($"Elige una copia disponible de Destiny y exactamente {DefenseDiscardCost} cartas para descartar.");
        var sniperIndices = new List<MemorySelection>();
        foreach (var index in indices.OrderDescending())
        {
            var sniper = CardCatalog.Get(p.Hand[index]).EquipOnDiscard;
            var discarded = SendHandToMemory(p, index);
            if (sniper && discarded != null) sniperIndices.Add(discarded);
        }
        p.SkillCounters[skillSlot] = p.CountersOnSkill(skillSlot) + 1;
        Log($"{p.Name} activa Relick Destiny Shuffle (espacio {p.SkillPosition(skillSlot) + 1}): descarta {indices.Length} cartas y cancela el daño. Contadores: {p.CountersOnSkill(skillSlot)}/2.");
        ResolveAttackDamage(true, deferCompletion: sniperIndices.Count > 0);
        BeginSniperChoices(actor, sniperIndices);
        return CommandResult.Ok();
    }

    private void ResolveAttackDamage(bool canceled, string source = "Destiny", bool deferCompletion = false)
    {
        var attack = State.PendingAttack!;
        State.Phase = Phase.Combat;
        if (canceled) State.LastCombat += $" · Daño cancelado por {source}";
        // The final attack die survives secondary damage dice and the defense decision.
        else
        {
            DealDamage(1 - attack.Attacker, attack.Damage, State.LastAttackDie,
                new EngineContinuation { Kind = ContinuationKind.FinishAttackDamage, Actor = attack.Attacker, Flag = deferCompletion }, source: attack.Attacker);
            return;
        }
        FinishAttackDamage(deferCompletion);
    }

    private void FinishAttackDamage(bool deferCompletion)
    {
        var attack = State.PendingAttack!;
        State.PendingAttack = attack with { DamageResolved = true };
        if (State.IsOver) { State.PendingAttack = null; return; }
        // Canceling damage does not undo the critical result or its skill effect.
        if (!State.IsOver && attack.Critical && !attack.EffectsSuppressed && attack.SkillId == CardCatalog.Magnet.Id &&
            (State.Players[1 - attack.Attacker].Hand.Count > 0 || State.Players[1 - attack.Attacker].Bonus != null))
            State.Phase = Phase.MagnetChoice;
        if (!deferCompletion) CompletePendingPlay();
    }

    private void CompleteAttackIfReady()
    {
        if (State.Phase != Phase.Combat || State.PendingAttack is not { DamageResolved: true } attack ||
            State.PendingSniper != null || _activeActivation != null) return;
        State.PendingAttack = null;
        if (!attack.RewardPending) return;
        var p = State.Players[attack.Attacker]; var reward = CardCatalog.Get(attack.SkillId).Reward;
        p.Relikia += reward;
        Log($"{p.Name} obtiene {reward} PR.");
    }

    public CommandResult SkipMagnet(int actor)
    {
        var error = ValidateTurn(actor, Phase.MagnetChoice);
        if (error != null) return CommandResult.Fail(error);
        State.Phase = Phase.Combat;
        Log($"{State.Active.Name} decide no tomar carta por Magnet Shuffle.");
        CompletePendingPlay();
        return CommandResult.Ok();
    }

    public CommandResult ResolveMagnet(int actor, bool fromBonus, int handIndex = -1)
    {
        var error = ValidateTurn(actor, Phase.MagnetChoice);
        if (error != null) return CommandResult.Fail(error);
        var rival = State.Players[1 - actor];
        if (fromBonus ? rival.Bonus == null : !InHand(rival, handIndex))
            return CommandResult.Fail("Elige una carta de la mano del rival o de su Bonus.");
        var id = fromBonus ? rival.Bonus! : rival.Hand[handIndex];
        State.Phase = Phase.Combat;
        if (fromBonus && TryReplaceOpponentFieldEffect(actor, new(1 - actor, FieldZone.Bonus, -1, id)))
            return CommandResult.Ok();
        if (fromBonus) TakeBonus(rival, State.Active);
        else
        {
            // A copy the rival had borrowed simply comes home.
            var returning = rival.RemoveHandCard(handIndex);
            State.Active.Hand.Add(id);
            if (!returning) State.Active.MarkForeignHandCard(State.Active.Hand.Count - 1);
        }
        Log($"{State.Active.Name} toma {CardCatalog.Get(id).Name} de {rival.Name} ({(fromBonus ? "Bonus" : "mano")}) por Magnet Shuffle.");
        if (fromBonus) RefillBonus(rival);
        CompletePendingPlay();
        return CommandResult.Ok();
    }
}
