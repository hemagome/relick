namespace Relick.Core;

public sealed partial class GameEngine
{
    public int DefenseDiscardCost => State.PendingAttack?.Critical == true ? 4 : 3;

    public bool CanUseSkillAction(int actor, int slot) => ValidateAction(actor) == null &&
        CardEffects.For(State.Active.Skills.ElementAtOrDefault(slot)) is ISkillAction &&
        !EffectsSuppressed(actor, FieldZone.Skill, slot);

    public CommandResult UseSkillAction(int actor, int slot)
    {
        if (!CanUseSkillAction(actor, slot)) return CommandResult.Fail("Esta habilidad requiere una acción disponible.");
        var id = State.Active.Skills[slot];
        State.ActionsLeft--;
        Log($"{State.Active.Name} activa {CardCatalog.Get(id).Name} (1 acción).");
        ((ISkillAction)CardEffects.For(id)!).Activate(EffectFor(actor, id));
        return CommandResult.Ok();
    }

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
            .Where(i => p.Skills[i] == CardCatalog.Destiny.Id && p.CountersOnSkill(i) < CardCatalog.DestinyCounterLimit && !EffectsSuppressed(actor, FieldZone.Skill, i));
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
            var discarded = DiscardHandCard(p, index);
            if (sniper && discarded != null) sniperIndices.Add(discarded);
        }
        p.SkillCounters[skillSlot] = p.CountersOnSkill(skillSlot) + 1;
        Log($"{p.Name} activa Relick Destiny Shuffle (espacio {p.SkillPosition(skillSlot) + 1}): descarta {indices.Length} cartas y cancela el daño. Contadores: {p.CountersOnSkill(skillSlot)}/{CardCatalog.DestinyCounterLimit}.");
        ResolveAttackDamage(true, deferCompletion: sniperIndices.Count > 0, cardId: p.Skills[skillSlot]);
        BeginSniperChoices(actor, sniperIndices);
        return CommandResult.Ok();
    }

    private void ResolveAttackDamage(bool canceled, string source = "Destiny", bool deferCompletion = false, string? cardId = null)
    {
        var attack = State.PendingAttack!;
        State.Phase = Phase.Combat;
        if (!attack.EffectsSuppressed && CardEffects.For(attack.SkillId) is IAttackDamageEffect effect &&
            effect.TryPrepareDamage(EffectFor(attack.Attacker, attack.SkillId), new EngineContinuation
            { Kind = ContinuationKind.ApplyAttackDamage, Actor = attack.Attacker, Flag = canceled, Text = source, Step = cardId })) return;
        ApplyAttackDamage(canceled, source, deferCompletion, cardId);
    }

    private void ApplyAttackDamage(bool canceled, string source, bool deferCompletion = false, string? cardId = null)
    {
        var attack = State.PendingAttack!;
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

    private bool TryContinueAttackResolution()
    {
        if (State.Phase != Phase.Combat || State.PendingAttack is not { } attack ||
            State.PendingSniper != null || _activeActivation != null) return false;
        if (attack.ApplyDamage is { } applyDamage)
        {
            State.PendingAttack = attack with { ApplyDamage = null };
            Continue(applyDamage);
            return true;
        }
        if (!attack.DamageResolved) return false;
        if (attack.AfterDamage is { } afterDamage)
        {
            State.PendingAttack = attack with { AfterDamage = null };
            Continue(afterDamage);
            return true;
        }
        if (attack.RewardRollPending) return false;
        if (attack.RewardPending && attack.Reward == null && CardEffects.For(attack.SkillId) is IAttackRewardRoll roll)
        {
            State.PendingAttack = attack with { RewardRollPending = true };
            roll.RollReward(EffectFor(attack.Attacker, attack.SkillId));
            return true;
        }
        State.PendingAttack = null;
        if (!attack.RewardPending) return false;
        var p = State.Players[attack.Attacker]; var reward = attack.Reward ?? CardCatalog.Get(attack.SkillId).Reward;
        p.Relikia += reward;
        Log($"{p.Name} obtiene {reward} PR.");
        return false;
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
        var next = new EngineContinuation { Kind = ContinuationKind.TakeMagnetTarget, Actor = actor,
            Target = new(1 - actor, fromBonus ? zone : FieldZone.Hand, fromBonus ? -1 : handIndex, id) };
        if (!TryOfferTargetResponse(actor, 1 - actor, CardCatalog.Magnet.Id, continuation: next)) Continue(next);
        return CommandResult.Ok();
    }

    private void FinishMagnetChoice(EngineContinuation next)
    {
        var actor = next.Actor;
        var rival = State.Players[1 - actor];
        if (next.Target is not { } target || (target.Zone switch
            {
                FieldZone.Area => rival.Area,
                FieldZone.Bonus => rival.Bonus,
                _ => rival.Hand.ElementAtOrDefault(target.Index)
            }) != target.CardId)
        {
            if (rival.Hand.Count == 0 && rival.Bonus == null && rival.Area == null) SkipMagnet(actor);
            return;
        }

        State.Phase = Phase.Combat;
        if (TryReplaceOpponentFieldEffect(actor, target)) return;
        if (target.Zone == FieldZone.Area) TakeArea(rival, State.Active);
        else if (target.Zone == FieldZone.Bonus) TakeBonus(rival, State.Active);
        else MoveCardBetweenHands(rival, State.Active, target.Index);
        Log($"{State.Active.Name} toma {CardCatalog.Get(target.CardId).Name} de {rival.Name} ({(target.Zone == FieldZone.Area ? "Área" : target.Zone == FieldZone.Bonus ? "Bonus" : "mano")}) por Magnet Shuffle.");
        if (target.Zone == FieldZone.Bonus) RefillBonus(rival);
        CompletePendingPlay();
    }
}
