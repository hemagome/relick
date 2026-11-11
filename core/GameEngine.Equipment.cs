namespace Relick.Core;

public enum PendantEffect { Reroll, AddOne, AddTwo }

public sealed partial class GameEngine
{
    private int DamageWithEquipmentBonus(int actor, int damage) => damage + (ActiveEquipment(actor)?.DamageBonus ?? 0);
    public CommandResult Equip(int actor, int handIndex)
    {
        var error = ValidateAction(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        if (!InHand(p, handIndex) || CardCatalog.Get(p.Hand[handIndex]).Kind != CardKind.Equipment)
            return CommandResult.Fail("Selecciona un equipo de tu mano.");
        var id = p.Hand[handIndex];
        p.RemoveHandCard(handIndex); State.ActionsLeft--;
        var memoryIndex = p.Memory.Count; p.Memory.Add(id);
        var frame = new EngineActivation(actor, id, 1, 0, State.Phase) { Kind = ActivationKind.Equipment, MemoryIndex = memoryIndex };
        BeginActivation(frame);
        return CommandResult.Ok();
    }

    private void FinishEquipmentInstall()
    {
        var frame = _activeActivation!; var p = State.Players[frame.Actor];
        if (p.Equipment == null || p.Equipment == frame.CardId && CardCatalog.Get(frame.CardId).Stackable)
        {
            p.Memory.RemoveAt(frame.MemoryIndex); ShiftSniperIndices(frame.Actor, frame.MemoryIndex);
            if (p.Equipment != null)
            {
                p.EquipmentStackedCopies++;
                Log($"{p.Name} apila {CardCatalog.Get(frame.CardId).Name}: {p.EquipmentStackedCopies + 1} copias equipadas.");
                CompleteActivationIfFinished(); return;
            }
            p.Equipment = frame.CardId; p.EquipmentDie = 0; p.EquipmentCounters = 0;
            p.EquipmentStackedCopies = 0;
            var card = CardCatalog.Get(frame.CardId);
            p.EquipmentExpiresAtTurnStart = card.DestroyAtNextTurnStart;
            Log($"{p.Name} pone {card.Name} en su zona de equipo (1 acción).");
            if (card.Heal > 0) Heal(p, card.Heal);
            if (frame.FinishSniperExchange) CompleteExchange(p);
            if (card.StoreDie)
                RollWithRelikario(p, DiePurpose.PcStore, new DieEffect { Purpose = DiePurpose.PcStore, Actor = frame.Actor });
        }
        else
        {
            Log($"{p.Name} conserva su equipo protegido; {CardCatalog.Get(frame.CardId).Name} queda en memoria porque la zona sigue ocupada.");
            if (frame.FinishSniperExchange) CompleteExchange(p);
        }
        CompleteActivationIfFinished();
    }

    private void DestroyEquipmentNow(PlayerState p, int index)
    {
        if (p.Equipment == null) return;
        var id = p.Equipment;
        var trap = CardCatalog.Get(id).CodeType == CodeType.Trap;
        ChangeZahiraHosts(Array.IndexOf(State.Players, p), FieldZone.Equipment, index < 0 ? p.EquipmentStackedCopies : index);
        NotifyPileMove(p, id, CardZone.Equipment, destination: trap ? CardZone.OutOfGame : CardZone.Memory);
        if (p.EquipmentStackedCopies > 0)
        {
            p.EquipmentStackedCopies--; p.Memory.Add(id);
            Log($"{p.Name} destruye una copia de {CardCatalog.Get(id).Name}: quedan {p.EquipmentStackedCopies + 1} equipadas.");
            return;
        }
        p.Equipment = null; p.EquipmentDie = 0; p.EquipmentCounters = 0;
        p.EquipmentExpiresAtTurnStart = false;
        if (!trap) p.Memory.Add(id);
        Log($"{p.Name} destruye {CardCatalog.Get(id).Name}: {(trap ? "queda fuera del juego" : "va a su memoria")}.");
    }

    private void ReplaceEquipment(EngineContinuation next)
    {
        var frame = _activeActivation!; var p = State.Players[frame.Actor];
        if (next.Prevented || p.Equipment == null || p.Equipment == frame.CardId && CardCatalog.Get(frame.CardId).Stackable)
            FinishEquipmentInstall();
        else DestroyEquipment(p, next);
    }

    public bool CanUseYunoOnPendingDie(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length || actor != State.DecisionPlayer) return false;
        if (EffectsSuppressed(actor, FieldZone.Relikario)) return false;
        var p = State.Players[actor];
        if (State.Phase == Phase.AttackRerollChoice && State.PendingAttackRoll is { } attack)
            return p.Relikario.CanRerollAttack && attack.Die != 1 && !attack.YunoStopped && attack.Rerolls < p.Relikario.MaxRerolls &&
                (attack.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        if (State.Phase == Phase.DieRerollChoice && State.PendingDieRoll is { } effect)
            return p.Relikario.CanRerollAnyDie && effect.Die != 1 && !effect.YunoStopped && effect.Rerolls < p.Relikario.MaxRerolls &&
                (effect.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        return false;
    }

    private bool CanRespondToAttackDie() => HasTurnBuffInHand(State.CurrentPlayer) || CanUseYunoOnPendingDie(State.CurrentPlayer) ||
        ActiveEquipment(State.CurrentPlayer)?.Id == CardCatalog.Pendant.Id || HasRerollCombo(State.CurrentPlayer) || CanUsePc(State.CurrentPlayer) || CanUseDexterity(State.CurrentPlayer);
    private bool CanRespondToEffectDie() => HasTurnBuffInHand(State.DecisionPlayer) || CanUseYunoOnPendingDie(State.DecisionPlayer) ||
        ActiveEquipment(State.DecisionPlayer)?.Id == CardCatalog.Pendant.Id || HasRerollCombo(State.DecisionPlayer) || CanUsePc(State.DecisionPlayer) || CanUseDexterity(State.DecisionPlayer) || CanRerollLearningDie(State.DecisionPlayer);

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
            _ => attack ? RollAttack(out _, "Colgante · Relanzamiento", superAttack: State.PendingAttackRoll!.SuperAttack) : Roll("Colgante · Relanzamiento") };
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
