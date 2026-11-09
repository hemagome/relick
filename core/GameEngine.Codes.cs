namespace Relick.Core;
public enum FieldZone { Equipment, Attached, Bonus, Exchange, Hand, Skill, EquipmentCover }
public sealed record FieldTarget(int Owner, FieldZone Zone, int Index, string CardId);
public sealed partial class GameEngine
{
    public bool IsZoneDestruction => State.Phase == Phase.DestroyChoice && _activeActivation is { Kind: ActivationKind.Code } frame &&
        CardCatalog.Get(frame.CardId).DestroyChosenZone;

    private IEnumerable<FieldTarget> TimeBombTargets(int excludedOwner = -1, int excludedHand = -1)
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
        {
            var p = State.Players[owner];
            if (p.Equipment != null) yield return new(owner, FieldZone.Equipment, -1, p.Equipment);
            for (var i = 0; i < (p.EquipmentCovers?.Count ?? 0); i++)
                yield return new(owner, FieldZone.EquipmentCover, i, p.EquipmentCovers![i]);
            if (p.Bonus != null) yield return new(owner, FieldZone.Bonus, -1, p.Bonus);
            if (p.Hand.Count > (owner == excludedOwner && excludedHand >= 0 ? 1 : 0))
                yield return new(owner, FieldZone.Hand, -1, "");
        }
    }

    public IEnumerable<FieldTarget> DestructionTargets()
    {
        if (State.Phase == Phase.DestroyChoice && IsZoneDestruction)
        {
            foreach (var target in TimeBombTargets()) yield return target;
            yield break;
        }
        for (var owner = 0; owner < 2; owner++)
        {
            var p = State.Players[owner];
            if (p.Equipment != null && CardCatalog.Get(p.Equipment).Kind != CardKind.Skill) yield return new(owner, FieldZone.Equipment, -1, p.Equipment);
            for (var i = 0; i < (p.EquipmentCovers?.Count ?? 0); i++)
                yield return new(owner, FieldZone.EquipmentCover, i, p.EquipmentCovers![i]);
            for (var i = 0; i < p.AttachedCards.Count; i++)
                if (CardCatalog.Get(p.AttachedCards[i]).Kind != CardKind.Skill) yield return new(owner, FieldZone.Attached, i, p.AttachedCards[i]);
            if (p.Bonus != null && CardCatalog.Get(p.Bonus).Kind != CardKind.Skill)
                yield return new(owner, FieldZone.Bonus, -1, p.Bonus);
            for (var i = 0; i < p.Exchange.Count; i++)
                if (CardCatalog.Get(p.Exchange[i]).Kind != CardKind.Skill)
                    yield return new(owner, FieldZone.Exchange, i, p.Exchange[i]);
        }
    }
    private string? CodeEffectUnavailable(PlayerState p, CardDefinition card, int extraCost = 0, int excludedHand = -1)
    {
        if (State.PendingTotemDamage != null) return "Decide primero si cancelas el daño con Tótem de Salvación.";
        if (State.PendingGoldenTotem != null && !card.CancelActivation) return "Completa la colocación de Tótem de Oro.";
        if (card.DeployTotems && excludedHand < 0) return "Tótem de Oro no puede activarse con Relikia.";
        if (card.DeployTotems && p.Equipment == null && !p.Hand.Contains(CardCatalog.Totem.Id))
            return "Tótem de Oro necesita un equipo debajo o un Tótem de Salvación en tu mano.";
        if (card.FieldCombatDie) return "Activa Jessica Lee desde tu zona de canje o Bonus durante tu etapa de combate.";
        if (State.PendingReload != null && !card.CancelActivation && !card.RerollOwnDie && card.SetOwnDie <= 0)
            return "Completa la resolución de Cartucho de Re-carga.";
        if (State.PendingTurnBuff != null && !card.CancelActivation) return $"Completa la activación de {CardCatalog.Get(State.PendingTurnBuff.CardId).Name}.";
        if (card.RepeatStage && RepeatableStage(Array.IndexOf(State.Players, p)) == null)
            return "Doblador de Tiempo requiere una etapa de tu propio turno sin otra elección pendiente.";
        if (card.DiscardForTurnBuff)
        {
            if (State.Phase is Phase.ErrorResponse or Phase.DestructionResponse or Phase.SniperChoice or Phase.RecycleChoice or Phase.Discard)
                return "Completa la decisión pendiente antes de activar Estrella del Fontanero.";
        }
        if (card.DiscardCost > 0 && p.Hand.Count < card.DiscardCost + (excludedHand < 0 ? 0 : 1))
            return $"Necesitas {card.DiscardCost} carta(s) adicionales para pagar el descarte.";
        if (card.CancelActivation && HandReservedForActivation(Array.IndexOf(State.Players, p)))
            return "Las cartas restantes están reservadas para el descarte de la activación pendiente.";
        if (State.Turn < card.EarliestTurn) return $"{card.Name} solo puede activarse a partir del turno {card.EarliestTurn} de la partida.";
        if (p.Relikia < card.PrCost + extraCost) return $"Necesitas {card.PrCost + extraCost} PR.";
        if (card.ReloadBothHands && (p.Hand.Count <= (excludedHand < 0 ? 0 : 1) || State.Players[1 - Array.IndexOf(State.Players, p)].Hand.Count == 0))
            return "Ambos jugadores necesitan al menos una carta que descartar, además del Cartucho que se activa.";
        if (card.SearchAttacks > 0 && !p.Deck.Any(id => CardCatalog.Get(id).Kind == CardKind.Attack)) return "No hay ataques en tu mazo.";
        if (card.SearchNonSkill && !SearchableDeckCards(Array.IndexOf(State.Players, p)).Any()) return "No hay no-habilidades en tu mazo.";
        if (card.RecoverMemory && State.Players.Sum(player => player.Memory.Count) <= (excludedHand < 0 ? 1 : 0))
            return "No hay cartas disponibles en las memorias.";
        if (card.DestroyNonSkill && !DestructionTargets().Any()) return "No hay no-habilidades en el campo para destruir.";
        if (card.DestroyChosenZone && !TimeBombTargets(Array.IndexOf(State.Players, p), excludedHand).Any())
            return "No hay cartas en equipos, manos o Bonus para destruir.";
        if (card.DodgeAttack && (State.Phase != Phase.DefenseChoice || State.PendingAttack?.Attacker != 1 - Array.IndexOf(State.Players, p))) return "Solo puedes esquivar un ataque de tu oponente pendiente de defensa.";
        if (card.ResetMind && MindResetUnavailable(Array.IndexOf(State.Players, p), excludedHand) is { } resetError) return resetError;
        if (card.CancelActivation && !CanPlayLoadError(Array.IndexOf(State.Players, p))) return "No hay una activación que puedas anular.";
        if ((card.RerollOwnDie || card.SetOwnDie > 0) && !OwnDiePending(Array.IndexOf(State.Players, p))) return "Requiere un dado propio pendiente.";
        return null;
    }
    public string? CodeUnavailableReason(int actor, int handIndex)
    {
        if (actor < 0 || actor >= 2 || State.IsOver || State.Phase == Phase.Preparation) return "No puedes jugar ahora.";
        var p = State.Players[actor];
        if (!InHand(p, handIndex) || CardCatalog.Get(p.Hand[handIndex]).Kind != CardKind.Code) return "Elige un código.";
        var card = CardCatalog.Get(p.Hand[handIndex]);
        if (!card.IsCombo) { var error = ValidateTurn(actor, Phase.Actions); if (error != null) return error; }
        if (p.ActionsLeft < card.ActionCost) return "No quedan acciones disponibles.";
        return CodeEffectUnavailable(p, card, excludedHand: handIndex);
    }
    public CommandResult ResolveDestruction(int actor, int owner, bool equipment, int index = -1)
        => ResolveDestruction(actor, owner, equipment ? FieldZone.Equipment : FieldZone.Attached, index);

    public CommandResult ResolveDestruction(int actor, int owner, FieldZone zone, int index = -1)
    {
        if (State.Phase != Phase.DestroyChoice || State.DestroyActor != actor) return CommandResult.Fail("No te corresponde elegir objetivo.");
        if (IsZoneDestruction && index != -1 && zone != FieldZone.EquipmentCover) return CommandResult.Fail("Elige una zona; la carta de la mano se escoge al azar.");
        var target = DestructionTargets().FirstOrDefault(t => t.Owner == owner && t.Zone == zone && (zone is FieldZone.Equipment or FieldZone.Bonus || t.Index == index));
        if (target == null) return CommandResult.Fail("Elige un objetivo válido para esta destrucción.");
        var p = State.Players[owner];
        var codeName = IsZoneDestruction ? CardCatalog.Get(_activeActivation!.CardId).Name : "Huevo Azul Explosivo";
        var id = target.CardId;
        if (zone == FieldZone.Hand)
        {
            index = _random.Next(p.Hand.Count);
            id = p.Hand[index];
        }
        var chosen = target with { Index = index, CardId = id };
        RequestDestruction(chosen, new EngineContinuation { Kind = ContinuationKind.FinishCodeDestruction, Actor = actor, Target = chosen, Text = codeName });
        return CommandResult.Ok();
    }
    private bool OwnDiePending(int actor) => State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll?.Attacker == actor
        : State.Phase == Phase.DieRerollChoice && State.PendingDieRoll?.Player == actor;
    private bool HasRerollCombo(int actor) => State.Players[actor].Hand.Any(id => { var c = CardCatalog.Get(id); return c.IsCombo && (c.RerollOwnDie || c.SetOwnDie > 0) && State.Players[actor].ActionsLeft >= c.ActionCost; });
    private CommandResult PlayRerollCombo(int actor, int index)
    {
        var p = State.Players[actor]; var card = CardCatalog.Get(p.Hand[index]);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost; p.Relikia -= card.PrCost;
        var frame = new EngineActivation(actor, card.Id, card.ActionCost, card.PrCost, State.Phase)
        { Kind = ActivationKind.Reroll, Parent = _activeActivation };
        BeginActivation(frame);
        return CommandResult.Ok();
    }
    private void ResolveRerollComboEffect(int actor, CardDefinition card)
    {
        var p = State.Players[actor];
        if (card.RerollOwnDie) OnOwnDieRerolled(actor);
        var phase = State.Phase; var attack = State.PendingAttackRoll; var effect = State.PendingDieRoll;
        var continuation = _pendingDieEffect;
        if (card.SetOwnDie > 0)
        {
            if (attack != null) attack = attack with { Die = card.SetOwnDie, Modified = true };
            if (effect != null) effect = effect with { Die = card.SetOwnDie, Modified = true };
            Log($"{p.Name} activa {card.Name}: dado {card.SetOwnDie}.");
            Continue(new EngineContinuation { Kind = ContinuationKind.ResumeCombo, Actor = actor, Phase = phase,
                Attack = attack, Die = effect, SavedDieEffect = continuation });
            return;
        }
        var die = attack != null ? RollAttack(out _, "Re-lanza · COMBO") : Roll("Re-lanza · COMBO");
        if (attack != null) attack = attack with { Die = die, Modified = true, WasRerolled = true };
        if (effect != null) effect = effect with { Die = die, Modified = true, WasRerolled = true };
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = attack != null ? Phase.Combat : effect!.ResumePhase;
        var resume = new EngineContinuation { Kind = ContinuationKind.ResumeCombo, Actor = actor, Phase = phase,
            Attack = attack, Die = effect, SavedDieEffect = continuation };
        Log($"{p.Name} activa {card.Name}: dado {die}." + (die <= 3 ? " Roba una carta." : ""));
        var draw = new EngineContinuation { Kind = ContinuationKind.RerollDraw, Actor = actor, Value = die, Die = effect, SavedDieEffect = continuation, Next = resume };
        if (attack != null) ProcessAttackDie(die, draw); else Continue(draw);
    }

    private void FinishRerollDraw(EngineContinuation next)
    {
        var die = next.Value; var effect = next.Die; var continuation = next.SavedDieEffect;
        var resume = next.Next!; var actor = next.Actor; var p = State.Players[actor];
        if (die <= 3 && effect?.Purpose == DiePurpose.Recycling)
        {
            // The first recycling has not refilled the empty deck yet. Queue the bonus
            // draw after that selection instead of treating this same empty deck as a second depletion.
            continuation!.Next = new EngineContinuation { Kind = ContinuationKind.Draw, Actor = actor, Count = 1, Next = continuation.Next };
            Continue(resume); return;
        }
        if (die <= 3) DrawCards(p, 1, resume); else Continue(resume);
    }
}
