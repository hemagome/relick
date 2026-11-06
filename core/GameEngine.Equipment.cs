namespace Relick.Core;

public enum PendantEffect { Reroll, AddOne, AddTwo }

public sealed partial class GameEngine
{
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
        var frame = new ActivationFrame(actor, id, 1, 0, State.Phase);
        frame.Resume = () =>
        {
            p.Memory.RemoveAt(memoryIndex);
            if (p.Equipment != null) DestroyEquipment(p);
            p.Equipment = id;
            Log($"{p.Name} pone {CardCatalog.Get(id).Name} en su zona de equipo (1 acción).");
        };
        frame.Cancel = () =>
        {
            NotifyPileMove(p, id, CardZone.Activation);
            State.Phase = frame.ResumePhase;
        };
        BeginActivation(frame);
        return CommandResult.Ok();
    }

    private void DestroyEquipment(PlayerState p)
    {
        if (p.Equipment == null) return;
        var id = p.Equipment;
        NotifyPileMove(p, id, CardZone.Equipment);
        p.Equipment = null; p.Memory.Add(id);
        Log($"{p.Name} destruye {CardCatalog.Get(id).Name}: va a su memoria.");
    }

    public bool CanUseYunoOnPendingDie(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length || actor != State.DecisionPlayer) return false;
        var p = State.Players[actor];
        if (State.Phase == Phase.AttackRerollChoice && State.PendingAttackRoll is { } attack)
            return p.Relikario.CanRerollAttack && attack.Die != 1 && !attack.YunoStopped && attack.Rerolls < p.Relikario.MaxRerolls &&
                (attack.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        if (State.Phase == Phase.DieRerollChoice && State.PendingDieRoll is { } effect)
            return p.Relikario.CanRerollAnyDie && effect.Die != 1 && !effect.YunoStopped && effect.Rerolls < p.Relikario.MaxRerolls &&
                (effect.Rerolls > 0 || !p.AttackRerollUsedThisTurn);
        return false;
    }

    private bool CanRespondToAttackDie() => CanUseYunoOnPendingDie(State.CurrentPlayer) ||
        State.Players[State.CurrentPlayer].Equipment == CardCatalog.Pendant.Id || HasRerollCombo(State.CurrentPlayer);
    private bool CanRespondToEffectDie() => CanUseYunoOnPendingDie(State.DecisionPlayer) ||
        State.Players[State.DecisionPlayer].Equipment == CardCatalog.Pendant.Id || HasRerollCombo(State.DecisionPlayer);

    public bool CanUsePendant(int actor, PendantEffect effect)
    {
        if (!Enum.IsDefined(effect) || actor < 0 || actor >= State.Players.Length || actor != State.DecisionPlayer ||
            State.Players[actor].Equipment != CardCatalog.Pendant.Id) return false;
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
        var attack = State.Phase == Phase.AttackRerollChoice;
        var p = State.Players[actor];
        var die = attack ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die;
        DestroyEquipment(p);
        die = effect switch { PendantEffect.AddOne => die + 1, PendantEffect.AddTwo => die + 2, _ => Roll("Colgante · Relanzamiento") };
        Log($"{p.Name} usa Colgante de la Suerte ({effect}): el dado queda en {die}.");
        if (attack)
        {
            var pending = State.PendingAttackRoll!;
            State.PendingAttackRoll = pending with { Die = die, WasRerolled = true, Modified = true };
            State.LastAttackDie = die;
            if (!CanRespondToAttackDie()) FinishAttackRoll(die, true);
        }
        else
        {
            var pending = State.PendingDieRoll!;
            State.PendingDieRoll = pending with { Die = die, WasRerolled = true, Modified = true };
            if (!CanRespondToEffectDie()) return ResolveDieReroll(actor, false);
        }
        return CommandResult.Ok();
    }
}
