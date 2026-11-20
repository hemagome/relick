namespace Relick.Core;

public sealed partial class GameEngine
{
    public int DefenseDiscardCost => State.PendingAttack?.Critical == true ? 4 : 3;

    private bool TryFinishWithGameOver()
    {
        if (State.IsOver) return false;
        // The non-active player's effect is added last and resolves first if both trigger together.
        var active = State.CurrentPlayer;
        int winner;
        if (HasGameOverSkills(1 - active)) winner = 1 - active;
        else if (HasGameOverSkills(active)) winner = active;
        else return false;
        Finish(winner, $"{State.Players[winner].Name} gana con Game en 500 PR y Over en 1000 PR.");
        return true;
    }

    private bool HasGameOverSkills(int owner)
    {
        var player = State.Players[owner];
        if (player.Skills.Count < 2) return false;
        var hasGame = false;
        var hasOver = false;
        for (var index = 0; index < player.Skills.Count; index++)
        {
            var cost = SkillSlotCosts[player.SkillPosition(index)];
            var id = player.Skills[index];
            if (cost == 500 && id == CardCatalog.Game.Id) hasGame = true;
            if (cost == 1000 && id == CardCatalog.Over.Id && !EffectsSuppressed(owner, FieldZone.Skill, index)) hasOver = true;
        }
        return hasGame && hasOver;
    }

    public IEnumerable<int> AvailableDefenses(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length) return Array.Empty<int>();
        var p = State.Players[actor];
        if (PlayableCards(actor).Count() < DefenseDiscardCost) return Array.Empty<int>();
        return Enumerable.Range(0, p.Skills.Count)
            .Where(i => p.Skills[i] == CardCatalog.Destiny.Id && p.CountersOnSkill(i) < 2 && !EffectsSuppressed(actor, FieldZone.Skill, i));
    }

    private bool CanDefend(int actor, bool critical) =>
        HasTurnBuffInHand(actor) || HasComboParry(actor) || HasDestinyPickResponse(actor) ||
        ComboResponsesEnabled(actor) && PlayableCards(actor).Any(card => card.Id == CardCatalog.Dodge.Id && AvailableActions(actor) >= CardCatalog.Dodge.ActionCost) || ActiveEquipment(actor)?.Id == CardCatalog.CardboardBox.Id ||
        IronwingRollDefenses(actor).Any() ||
        (PlayableCards(actor).Count() >= (critical ? 4 : 3) && AvailableDefenses(actor).Any());

    /// <summary>
    /// Closes a defense window that no longer offers anything: after a response resolves without ending the
    /// attack (Esquivar 1–3, a response canceled by Error de Carga), the defender would otherwise have to
    /// confirm damage they can no longer avoid. Runs once the whole move has resolved, so a card drawn by
    /// the response (another Esquivar) still keeps the window open.
    /// </summary>
    internal bool SettleDefenseWindow()
    {
        if (State.IsOver || State.Phase != Phase.DefenseChoice || _activeActivation != null || State.PendingAttack is not { } attack ||
            CanDefend(1 - attack.Attacker, attack.Critical)) return false;
        ResolveAttackDamage(false);
        return true;
    }

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
            indices.Distinct().Count() != indices.Length || indices.Any(i => PlayableHandCard(p, i) == null))
            return CommandResult.Fail($"Elige una copia disponible de Destiny y exactamente {DefenseDiscardCost} cartas para descartar.");
        var sniperIndices = new List<MemorySelection>();
        foreach (var index in indices.OrderDescending())
        {
            var sniper = CardCatalog.Get(PlayableHandCard(p, index)!).EquipOnDiscard;
            var discarded = SendHandToMemory(p, index);
            if (sniper && discarded != null) sniperIndices.Add(discarded);
        }
        p.SkillCounters[skillSlot] = p.CountersOnSkill(skillSlot) + 1;
        Log($"{p.Name} activa Relick Destiny Shuffle (espacio {p.SkillPosition(skillSlot) + 1}): descarta {indices.Length} cartas y cancela el daño. Contadores: {p.CountersOnSkill(skillSlot)}/2.");
        ResolveAttackDamage(true, deferCompletion: sniperIndices.Count > 0, cardId: p.Skills[skillSlot]);
        BeginSniperChoices(actor, sniperIndices);
        return CommandResult.Ok();
    }

    private void ResolveAttackDamage(bool canceled, string source = "Destiny", bool deferCompletion = false, string? cardId = null)
    {
        var attack = State.PendingAttack!;
        State.Phase = Phase.Combat;
        if (canceled)
        {
            State.LastCombat += $" · Daño cancelado por {source}";
            NotifyGuard(1 - attack.Attacker, attack.Attacker, cardId ?? CardCatalog.Destiny.Id, attack.Damage, 0, blocked: true);
        }
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
        if (attack.Critical && !attack.EffectsSuppressed && attack.SkillId == CardCatalog.TurboOverlap.Id &&
            OfferOsamuSearch(attack.Attacker, attack.SkillId, OsamuAgilitySearch)) return;
        // Canceling damage does not undo the critical result or its skill effect.
        if (!State.IsOver && attack.Critical && !attack.EffectsSuppressed && attack.SkillId == CardCatalog.Magnet.Id &&
            (State.Players[1 - attack.Attacker].Hand.Count > 0 || State.Players[1 - attack.Attacker].Bonus != null ||
             State.Players[1 - attack.Attacker].Area != null))
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

    // Presentation signal: owner, recipient, slot in the owner's hand, slot in the recipient's hand. Never carries identities.
    public event Action<int, int, int, int>? HandCardTaken;

    public CommandResult ResolveMagnet(int actor, bool fromBonus, int handIndex = -1, bool fromArea = false)
    {
        var error = ValidateTurn(actor, Phase.MagnetChoice);
        if (error != null) return CommandResult.Fail(error);
        var rival = State.Players[1 - actor];
        var zone = fromArea ? FieldZone.Area : FieldZone.Bonus;
        var fieldCard = fromArea ? rival.Area : rival.Bonus;
        if (fromArea && !fromBonus || (fromBonus ? fieldCard == null : !InHand(rival, handIndex)))
            return CommandResult.Fail("Elige una carta de la mano del rival o de su Bonus.");
        var id = fromBonus ? fieldCard! : rival.Hand[handIndex];
        State.Phase = Phase.Combat;
        if (fromBonus && TryReplaceOpponentFieldEffect(actor, new(1 - actor, zone, -1, id)))
            return CommandResult.Ok();
        if (fromArea) TakeArea(rival, State.Active);
        else if (fromBonus) TakeBonus(rival, State.Active);
        else MoveCardBetweenHands(rival, State.Active, handIndex);
        Log($"{State.Active.Name} toma {CardCatalog.Get(id).Name} de {rival.Name} ({(fromArea ? "Área" : fromBonus ? "Bonus" : "mano")}) por Magnet Shuffle.");
        if (fromBonus && !fromArea) RefillBonus(rival);
        CompletePendingPlay();
        return CommandResult.Ok();
    }
}
