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
            .Where(i => p.Skills[i] == CardCatalog.Destiny.Id && p.CountersOnSkill(i) < 2);
    }

    private bool CanDefend(int actor, bool critical) =>
        State.Players[actor].Hand.Any(id => CardCatalog.Get(id).DodgeAttack && State.Players[actor].ActionsLeft >= CardCatalog.Get(id).ActionCost) || State.Players[actor].Equipment == CardCatalog.CardboardBox.Id ||
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
        foreach (var index in indices.OrderDescending())
        {
            SendHandToMemory(p, index);
        }
        p.SkillCounters[skillSlot] = p.CountersOnSkill(skillSlot) + 1;
        Log($"{p.Name} activa Relick Destiny Shuffle (espacio {skillSlot + 1}): descarta {indices.Length} cartas y cancela el daño. Contadores: {p.CountersOnSkill(skillSlot)}/2.");
        ResolveAttackDamage(true);
        return CommandResult.Ok();
    }

    private void ResolveAttackDamage(bool canceled, string source = "Destiny")
    {
        var attack = State.PendingAttack!;
        State.PendingAttack = null;
        State.Phase = Phase.Combat;
        if (canceled) State.LastCombat += $" · Daño cancelado por {source}";
        // The final attack die survives secondary damage dice and the defense decision.
        else DealDamage(1 - attack.Attacker, attack.Damage, State.LastAttackDie);
        // Canceling damage does not undo the critical result or its skill effect.
        if (!State.IsOver && attack.Critical && attack.SkillId == CardCatalog.Magnet.Id &&
            (State.Players[1 - attack.Attacker].Hand.Count > 0 || State.Players[1 - attack.Attacker].Bonus != null))
            State.Phase = Phase.MagnetChoice;
    }

    public CommandResult SkipMagnet(int actor)
    {
        var error = ValidateTurn(actor, Phase.MagnetChoice);
        if (error != null) return CommandResult.Fail(error);
        State.Phase = Phase.Combat;
        Log($"{State.Active.Name} decide no tomar carta por Magnet Shuffle.");
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
        if (fromBonus) TakeBonus(rival, State.Active);
        else
        {
            rival.RemoveHandCard(handIndex);
            State.Active.Hand.Add(id);
        }
        State.Phase = Phase.Combat;
        Log($"{State.Active.Name} toma {CardCatalog.Get(id).Name} de {rival.Name} ({(fromBonus ? "Bonus" : "mano")}) por Magnet Shuffle.");
        if (fromBonus) RefillBonus(rival);
        return CommandResult.Ok();
    }
}
