namespace Relick.Core;

public enum PendantEffect { Reroll, AddOne, AddTwo }

public sealed partial class GameEngine
{
    private int DamageWithEquipmentBonus(int actor, int damage) => damage + (ActiveEquipment(actor)?.DamageBonus ?? 0);
    public int AvailableActions(int actor)
    {
        var actions = State.Players[actor].ActionsLeft - (ActiveEquipment(actor)?.Id == CardCatalog.MetalHood.Id ? 1 : 0);
        return Math.Max(0, ActionsWithAreas(actor, actions));
    }

    public CommandResult Equip(int actor, int handIndex) => Equip(actor, handIndex, onOpponent: false);

    public CommandResult Equip(int actor, int handIndex, bool onOpponent)
    {
        var error = ValidateAction(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        if (PlayableHandCard(p, handIndex) is not { } id || CardCatalog.Get(id).Kind != CardKind.Equipment)
            return CommandResult.Fail("Selecciona un equipo que puedas usar.");
        if (onOpponent && id != CardCatalog.MetalHood.Id && id != CardCatalog.ThiefGhost.Id)
            return CommandResult.Fail("Este equipo no se puede colocar en el campo rival.");
        var frame = new EngineActivation(actor, id, 1, 0, State.Phase)
        {
            Kind = ActivationKind.Equipment, EquipOpponent = onOpponent || id == CardCatalog.ThiefGhost.Id
        };
        if (handIndex >= TomeHandBase) HoldActivationCard(frame, handIndex);
        else
        {
            frame.CorruptOnMemory = p.CorruptsOnMemory(CardZone.Hand, handIndex);
            var waiting = SendToMemory(p, id, CardZone.Hand, handIndex, notify: false, deferCorruption: true)!;
            p.RemoveHandCard(handIndex);
            frame.MemoryIndex = waiting.Index;
            frame.MemoryOwner = waiting.Owner == actor ? null : waiting.Owner;
        }
        State.ActionsLeft--;
        BeginActivation(frame);
        return CommandResult.Ok();
    }

    private void FinishEquipmentInstall()
    {
        var frame = _activeActivation!; var p = State.Players[frame.EquipmentRecipient];
        var card = CardCatalog.Get(frame.CardId);
        if (card.LightEquipment)
        {
            var slot = p.LightEquipment?.Count ?? 0;
            InstallActivationCard(frame, p, CardZone.LightEquipment, slot, light: true);
            Log($"{p.Name} se equipa {card.Name} como equipo ligero (1 acción).");
            CompleteActivationIfFinished();
            return;
        }
        if (CanInstallEquipment(p, card))
        {
            var id = InstallActivationCard(frame, p, CardZone.Equipment, p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1);
            if (p.Equipment != null)
            {
                p.EquipmentStackedCopies++;
                Log($"{p.Name} apila {CardCatalog.Get(frame.CardId).Name}: {p.EquipmentStackedCopies + 1} copias equipadas.");
                CompleteActivationIfFinished(); return;
            }
            p.Equipment = id; p.EquipmentDie = 0; p.EquipmentCounters = 0;
            p.EquipmentStackedCopies = 0;
            p.EquipmentExpiresAtTurnStart = card.DestroyAtNextTurnStart;
            if (frame.EquipOpponent)
                NotifyPileMove(State.Players[frame.Actor], card.Id, CardZone.Activation, destination: CardZone.Equipment,
                    destinationPlayer: frame.EquipmentRecipient);
            Log(frame.EquipOpponent
                ? $"{State.Players[frame.Actor].Name} equipa {card.Name} a {p.Name} (1 acción)."
                : $"{p.Name} pone {card.Name} en su zona de equipo (1 acción).");
            if (card.Heal > 0) Heal(p, card.Heal);
            if (frame.FinishSniperExchange) CompleteExchange(p);
            if (card.StoreDie) RollEquipmentStorage(p);
            if (OfferGhostGift(Seat(p), null)) return;
        }
        else
        {
            Log($"{p.Name} conserva su equipo protegido; {card.Name} queda en memoria porque la zona sigue ocupada.");
            if (frame.FinishSniperExchange) CompleteExchange(p);
        }
        CompleteActivationIfFinished();
    }

    private void DestroyEquipmentNow(PlayerState p, int index)
    {
        if (p.Equipment == null) return;
        var id = p.Equipment;
        var trap = CardCatalog.Get(id).CodeType == CodeType.Trap;
        var slot = index < 0 ? p.EquipmentStackedCopies : index;
        ChangeZahiraHosts(Array.IndexOf(State.Players, p), FieldZone.Equipment, slot);
        var landed = SendToMemory(p, id, CardZone.Equipment, -1, corruptionIndex: slot);
        p.RemoveCorruptionSlots(CardZone.Equipment, slot);
        if (p.EquipmentStackedCopies > 0)
        {
            p.EquipmentStackedCopies--;
            Log($"{p.Name} destruye una copia de {CardCatalog.Get(id).Name}: quedan {p.EquipmentStackedCopies + 1} equipadas.");
            return;
        }
        ClearEquipment(p);
        Log($"{p.Name} destruye {CardCatalog.Get(id).Name}: {(trap ? "queda fuera del juego" : landed == null ? "se corrompe" :
            landed.Owner == Seat(p) ? "va a su memoria" : $"va a la memoria de {State.Players[landed.Owner].Name}, su dueño")}.");
    }

    private void BeginEquipmentInstall(EngineActivation frame)
    {
        frame.CanInterrupt = false;
        var recipient = frame.EquipmentRecipient;
        if (frame.EquipOpponent && State.Players[recipient].Equipment is { } equipment &&
            TryReplaceOpponentFieldEffect(frame.Actor, new(recipient, FieldZone.Equipment, -1, equipment),
                new EngineContinuation { Kind = ContinuationKind.BlockOpponentEquipment, Actor = frame.Actor }))
            return;
        ReplaceEquipment(new EngineContinuation { Kind = ContinuationKind.ReplaceEquipment, Actor = frame.Actor });
    }

    private void ReplaceEquipment(EngineContinuation next)
    {
        var frame = _activeActivation!; var p = State.Players[frame.EquipmentRecipient];
        if (next.Prevented || CanInstallEquipment(p, CardCatalog.Get(frame.CardId)))
            FinishEquipmentInstall();
        else DestroyEquipment(p, next);
    }

    private static void ClearEquipment(PlayerState p)
    {
        p.Equipment = null;
        p.EquipmentDie = 0;
        p.EquipmentCounters = 0;
        p.EquipmentExpiresAtTurnStart = false;
    }

    private static bool CanInstallEquipment(PlayerState p, CardDefinition card) =>
        card.LightEquipment || p.Equipment == null || p.Equipment == card.Id && card.Stackable;

    private IEnumerable<FieldTarget> EquipmentTransferTargets(int owner)
    {
        if (State.Players[owner].Equipment is { } id && (CardCatalog.Get(id).Kind == CardKind.Equipment || id == CardCatalog.SuperMagnet.Id))
            yield return new(owner, FieldZone.Equipment, -1, id);
        foreach (var light in LightEquipmentTargets(owner)) yield return light;
    }

    private void RollEquipmentStorage(PlayerState p, EngineContinuation? completed = null) =>
        RollWithRelikario(p, DiePurpose.PcStore, new DieEffect { Purpose = DiePurpose.PcStore, Actor = Seat(p), Next = completed });

    private void PrepareEquipmentTransfer(FieldTarget target, EngineContinuation completed)
    {
        var p = State.Players[completed.Actor];
        if (CanInstallEquipment(p, CardCatalog.Get(target.CardId))) Continue(completed);
        else DestroyEquipment(p, completed);
    }

    private bool FinishEquipmentTransfer(int actor, FieldTarget target, EngineContinuation completed)
    {
        var p = State.Players[actor];
        var source = State.Players[target.Owner];
        var card = CardCatalog.Get(target.CardId);
        if (!CanInstallEquipment(p, card)) return false;
        if (target.Zone == FieldZone.LightEquipment)
        {
            TransferLightEquipment(source, p, target);
            Log($"{p.Name} se equipa {card.Name} de {source.Name} como equipo ligero.");
            Continue(completed);
            return true;
        }

        var slot = p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1;
        TransferEquipmentCard(source, p, CardZone.Equipment, FieldZone.Equipment, source.EquipmentStackedCopies, slot, card.Id);
        if (slot == 0)
        {
            p.Equipment = card.Id;
            p.EquipmentDie = source.EquipmentDie;
            p.EquipmentCounters = source.EquipmentCounters;
            p.EquipmentExpiresAtTurnStart = card.DestroyAtNextTurnStart;
        }
        else p.EquipmentStackedCopies = slot;

        if (source.EquipmentStackedCopies > 0) source.EquipmentStackedCopies--;
        else
        {
            ClearEquipment(source);
            if (source.EquipmentCovers is { } covers)
            {
                p.EquipmentCovers ??= [];
                var first = p.EquipmentCovers.Count;
                for (var i = covers.Count - 1; i >= 0; i--)
                    TransferEquipmentCard(source, p, CardZone.EquipmentCover, FieldZone.EquipmentCover, i, first + i, covers[i]);
                p.EquipmentCovers.AddRange(covers);
                source.EquipmentCovers = null;
            }
        }

        Log($"{p.Name} se equipa {card.Name} de {source.Name}.");
        var active = ActiveEquipment(actor);
        if (active?.Heal > 0) Heal(p, active.Heal);
        if (active?.StoreDie == true)
        {
            p.EquipmentDie = 0;
            RollEquipmentStorage(p, completed);
        }
        else if (!OfferGhostGift(actor, completed)) Continue(completed);
        return true;
    }

    private void TransferEquipmentCard(PlayerState source, PlayerState recipient, CardZone zone, FieldZone field,
        int slot, int destinationSlot, string id)
    {
        var owner = Seat(recipient);
        if (!source.ReleaseForeignFieldCard(id)) MarkBorrowedFieldCard(recipient, id);
        if (source.CorruptsOnMemory(zone, slot)) recipient.MarkForCorruption(zone, destinationSlot);
        source.RemoveCorruptionSlots(zone, slot);
        ChangeZahiraHosts(Seat(source), field, slot, movedTo: new(owner, field, destinationSlot, id));
        NotifyPileMove(source, id, zone, slot, zone, destinationPlayer: owner);
    }

    public bool CanUseRelikarioOnPendingDie(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length || actor != State.DecisionPlayer) return false;
        if (EffectsSuppressed(actor, FieldZone.Relikario)) return false;
        var p = State.Players[actor];
        if (State.Phase == Phase.AttackRerollChoice && State.PendingAttackRoll is { } attack)
            return p.Relikario.CanRerollAttack && attack.Die != 1 && !attack.RerollStopped && attack.Rerolls < p.Relikario.MaxRerolls &&
                (attack.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        if (State.Phase == Phase.DieRerollChoice && State.PendingDieRoll is { } effect)
            return p.Relikario.CanRerollAnyDie && effect.Die != 1 && !effect.RerollStopped && effect.Rerolls < p.Relikario.MaxRerolls &&
                (effect.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        return false;
    }

    private bool CanRespondToAttackDie() => CanConvertD10(State.CurrentPlayer) || HasTurnBuffInHand(State.CurrentPlayer) || HasComboParry(State.CurrentPlayer) ||
        HasDestinyPickResponse(State.CurrentPlayer) || CanUseRelikarioOnPendingDie(State.CurrentPlayer) ||
        ActiveEquipment(State.CurrentPlayer)?.Id == CardCatalog.Pendant.Id || HasRerollCombo(State.CurrentPlayer) || CanUsePc(State.CurrentPlayer) || CanUseDexterity(State.CurrentPlayer) ||
        IronwingDieCostLimit(State.CurrentPlayer) > 0;
    private bool CanRespondToEffectDie() => CanConvertD10(State.DecisionPlayer) || HasTurnBuffInHand(State.DecisionPlayer) || HasComboParry(State.DecisionPlayer) || CanUseRelikarioOnPendingDie(State.DecisionPlayer) ||
        ActiveEquipment(State.DecisionPlayer)?.Id == CardCatalog.Pendant.Id || HasRerollCombo(State.DecisionPlayer) || CanUsePc(State.DecisionPlayer) || CanUseDexterity(State.DecisionPlayer) || CanRerollLearningDie(State.DecisionPlayer) ||
        IronwingDieCostLimit(State.DecisionPlayer) > 0;

    public bool CanUsePendant(int actor, PendantEffect effect)
    {
        if (!Enum.IsDefined(effect) || actor < 0 || actor >= State.Players.Length || actor != State.DecisionPlayer ||
            ActiveEquipment(actor)?.Id != CardCatalog.Pendant.Id) return false;
        var attack = State.Phase == Phase.AttackRerollChoice;
        if (!attack && State.Phase != Phase.DieRerollChoice) return false;
        var die = attack ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die;
        var rerolled = attack ? State.PendingAttackRoll!.WasRerolled : State.PendingDieRoll!.WasRerolled;
        if (effect == PendantEffect.Reroll) return true;
        var sum = effect == PendantEffect.AddOne ? 1 : 2;
        return rerolled && die + sum <= (attack ? 5 : 6);
    }

    public CommandResult UsePendant(int actor, PendantEffect effect)
    {
        if (!CanUsePendant(actor, effect)) return CommandResult.Fail("El Colgante no puede aplicar ese efecto a este dado.");
        DestroyEquipment(State.Players[actor], new EngineContinuation { Kind = ContinuationKind.UsePendant, Actor = actor, Value = (int)effect });
        return CommandResult.Ok();
    }

    private void FinishPendant(int actor, PendantEffect effect)
    {
        var attack = State.Phase == Phase.AttackRerollChoice;
        var p = State.Players[actor];
        if (effect == PendantEffect.Reroll) OnOwnDieRerolled(actor);
        var die = attack ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die;
        die = effect switch { PendantEffect.AddOne => die + 1, PendantEffect.AddTwo => die + 2,
            _ => attack ? RollAttack(out _, "Colgante · Relanzamiento", superAttack: State.PendingAttackRoll!.SuperAttack) : Roll("Colgante · Relanzamiento", roller: actor) };
        if (effect == PendantEffect.Reroll) die = AdjustBlackHoleDie(actor, State.PendingDieRoll?.Purpose, die);
        Log($"{p.Name} usa Colgante de la Suerte ({effect}): el dado queda en {die}.");
        if (attack)
        {
            var pending = State.PendingAttackRoll!;
            State.PendingAttackRoll = pending with { Die = die, WasRerolled = true, Modified = true };
            State.LastAttackDie = die;
            ProcessAttackDie(die, new EngineContinuation { Kind = ContinuationKind.AttackDieReady, Actor = actor });
        }
        else
        {
            var pending = State.PendingDieRoll!;
            State.PendingDieRoll = pending with { Die = die, WasRerolled = true, Modified = true };
            if (!CanRespondToEffectDie()) ResolveDieReroll(actor, false);
        }
    }
}
