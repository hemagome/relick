namespace Relick.Core;
public enum FieldZone { Equipment, Attached, Bonus, Exchange, Hand, Skill, EquipmentCover, Relikario, Zahira }
public sealed record FieldTarget(int Owner, FieldZone Zone, int Index, string CardId);
public sealed partial class GameEngine
{
    public const int BlueEggDrawCost = 200;
    internal bool LegacyBlueEgg { get; private init; }

    internal static int CodePrCost(CardDefinition card, bool legacyBlueEgg) =>
        legacyBlueEgg && card.Id == CardCatalog.BlueEgg.Id ? BlueEggDrawCost : card.PrCost;

    private int CodePrCost(CardDefinition card) => CodePrCost(card, LegacyBlueEgg);

    public bool IsZoneDestruction => State.Phase == Phase.DestroyChoice && _activeActivation is { Kind: ActivationKind.Code } frame &&
        frame.CardId == CardCatalog.TimeBomb.Id;

    private IEnumerable<FieldTarget> TimeBombTargets(int excludedOwner = -1, int excludedHand = -1)
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
        {
            var p = State.Players[owner];
            if (p.Equipment != null) yield return new(owner, FieldZone.Equipment, -1, p.Equipment);
            for (var i = 0; i < (p.EquipmentCovers?.Count ?? 0); i++)
                yield return new(owner, FieldZone.EquipmentCover, i, p.EquipmentCovers![i]);
            for (var slot = 0; slot < PlayerState.ZahiraSlots; slot++)
                if (ZahiraRootZone(owner, slot) is FieldZone.Equipment or FieldZone.EquipmentCover or FieldZone.Bonus)
                    yield return new(owner, FieldZone.Zahira, ZahiraIndex(slot), CardCatalog.ZahiraFlair.Id);
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
        foreach (var target in NonSkillFieldCards()) yield return target;
    }

    private IEnumerable<FieldTarget> NonSkillFieldCards()
    {
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
            for (var slot = 0; slot < PlayerState.ZahiraSlots; slot++)
                if (p.Zahira(slot) != null) yield return new(owner, FieldZone.Zahira, ZahiraIndex(slot), CardCatalog.ZahiraFlair.Id);
        }
    }
    private string? CodeEffectUnavailable(PlayerState p, CardDefinition card, int extraCost = 0, int excludedHand = -1)
    {
        if (card.CodeType == CodeType.Trap) return "Este Código Trampa se crea mediante un canje.";
        if (State.PendingTotemDamage != null) return "Decide primero si cancelas el daño con Tótem de Salvación.";
        if (State.PendingGoldenTotem != null && !card.CancelActivation) return "Completa la colocación de Tótem de Oro.";
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
        var prCost = CodePrCost(card) + extraCost;
        if (p.Relikia < prCost) return $"Necesitas {prCost} PR.";
        if (CardEffects.For(card.Id) is { } effect && effect.Unavailable(EffectFor(Array.IndexOf(State.Players, p), card.Id), excludedHand) is { } effectError)
            return effectError;
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
        if (State.Phase is Phase.TrapCraftChoice or Phase.ZahiraTarget) return "Completa la decisión del Código Trampa antes de jugar.";
        if (State.Phase == Phase.BlueEggDrawChoice) return "Decide primero si robas con Huevo Azul Explosivo.";
        if (State.Phase == Phase.Decision && State.PendingDecision is { AllowsCombos: false } decision)
            return $"Decide primero: {decision.Title()}.";
        if (card.IsCombo && p.AutoPassCombos && (actor != State.CurrentPlayer ||
            State.Phase is not (Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final)))
            return "Activa las respuestas COMBO para responder con esta carta.";
        if (!card.IsCombo) { var error = ValidateTurn(actor, Phase.Actions); if (error != null) return error; }
        if (p.ActionsLeft < card.ActionCost) return "No quedan acciones disponibles.";
        return CodeEffectUnavailable(p, card, excludedHand: handIndex);
    }
    public CommandResult ResolveDestruction(int actor, int owner, bool equipment, int index = -1)
        => ResolveDestruction(actor, owner, equipment ? FieldZone.Equipment : FieldZone.Attached, index);

    public CommandResult ResolveDestruction(int actor, int owner, FieldZone zone, int index = -1)
    {
        // Huevo Azul and Bomba de Tiempo now ask through a decision; this command still answers it.
        if (State.PendingDecision is { Step: FieldDestruction.Target } pending &&
            ResolveDecisionWhere(actor, pending.Source, c => c.Field is { } t && t.Owner == owner && t.Zone == zone &&
                // Bomba chooses zones: a hand card is drawn at random, so naming its position is refused.
                (t.Index == index || pending.Source != CardCatalog.TimeBomb.Id && zone is FieldZone.Equipment or FieldZone.Bonus)) is { } answered) return answered;
        // Legacy: a duel saved while the destruction phase was pending.
        if (State.Phase != Phase.DestroyChoice || State.DestroyActor != actor) return CommandResult.Fail("No te corresponde elegir objetivo.");
        if (IsZoneDestruction && index != -1 && zone != FieldZone.EquipmentCover) return CommandResult.Fail("Elige una zona; la carta de la mano se escoge al azar.");
        var target = DestructionTargets().FirstOrDefault(t => t.Owner == owner && t.Zone == zone && (zone is FieldZone.Equipment or FieldZone.Bonus || t.Index == index));
        if (target == null) return CommandResult.Fail("Elige un objetivo válido para esta destrucción.");
        var p = State.Players[owner];
        var codeName = _activeActivation is { } frame ? CardCatalog.Get(frame.CardId).Name : CardCatalog.BlueEgg.Name;
        var id = target.CardId;
        if (zone == FieldZone.Hand)
        {
            index = _random.Next(p.Hand.Count);
            id = p.Hand[index];
        }
        var chosen = target with { Index = index, CardId = id };
        var completed = new EngineContinuation { Kind = ContinuationKind.FinishCodeDestruction, Actor = actor, Target = chosen, Text = codeName };
        if (!TryReplaceOpponentFieldEffect(actor, chosen, completed)) RequestDestruction(chosen, completed);
        return CommandResult.Ok();
    }

    private void OfferBlueEggDraw(int actor)
    {
        if (State.Players[actor].Relikia >= BlueEggDrawCost) State.Phase = Phase.BlueEggDrawChoice;
    }

    public CommandResult ResolveBlueEggDraw(int actor, bool pay)
    {
        if (ResolveDecisionWhere(actor, CardCatalog.BlueEgg.Id, c => c.Field == null && c.Value == (pay ? 1 : 0)) is { } answered) return answered;
        // Legacy: a duel saved while this draw phase was pending.
        if (State.Phase != Phase.BlueEggDrawChoice || State.DecisionPlayer != actor)
            return CommandResult.Fail("No te corresponde decidir el robo de Huevo Azul Explosivo.");
        var p = State.Players[actor];
        if (pay && p.Relikia < BlueEggDrawCost)
            return CommandResult.Fail($"Necesitas {BlueEggDrawCost} PR.");

        State.Phase = Phase.Actions;
        if (pay)
        {
            p.Relikia -= BlueEggDrawCost;
            Log($"{p.Name} paga {BlueEggDrawCost} PR para robar una carta con {CardCatalog.BlueEgg.Name}.");
            DrawCards(p, 1);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
    private bool OwnDiePending(int actor) => State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll?.Attacker == actor
        : State.Phase == Phase.DieRerollChoice && State.PendingDieRoll?.Player == actor;
    private bool HasRerollCombo(int actor) => ComboResponsesEnabled(actor) && State.Players[actor].Hand.Any(id => { var c = CardCatalog.Get(id); return c.IsCombo && (c.RerollOwnDie || c.SetOwnDie > 0) && State.Players[actor].ActionsLeft >= c.ActionCost; });
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
        var die = attack != null ? RollAttack(out _, "Re-lanza · COMBO", superAttack: attack.SuperAttack) : Roll("Re-lanza · COMBO", roller: actor);
        if (attack != null) attack = attack with { Die = die, Modified = true, WasRerolled = true };
        if (effect != null) effect = effect with { Die = die, Modified = true, WasRerolled = true };
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = attack != null ? Phase.Combat : effect!.ResumePhase;
        var resume = new EngineContinuation { Kind = ContinuationKind.ResumeCombo, Actor = actor, Phase = phase,
            Attack = attack, Die = effect, SavedDieEffect = continuation };
        Log($"{p.Name} activa {card.Name}: dado {die}." + (die <= 3 ? " Roba una carta." : ""));
        var draw = new EngineContinuation { Kind = ContinuationKind.RerollDraw, Actor = actor, Value = die, Die = effect, SavedDieEffect = continuation, Next = resume, Text = card.Id };
        if (attack != null) ProcessAttackDie(die, draw); else Continue(draw);
    }

    private void FinishRerollDraw(EngineContinuation next)
    {
        var die = next.Value; var effect = next.Die; var continuation = next.SavedDieEffect;
        var resume = next.Next!; var actor = next.Actor; var p = State.Players[actor];
        // The new result is seen; before its draw, the rival may cancel Re-lanza with Error de Carga (6.5, 6.6).
        if (die <= 3 && !next.Flag && TryOpenErrorAfterReroll(next)) return;
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
