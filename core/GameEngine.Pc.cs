namespace Relick.Core;
public sealed partial class GameEngine
{
    public bool CanUsePc(int actor) => actor >= 0 && actor < State.Players.Length && actor == State.DecisionPlayer &&
        State.Players[actor].Equipment == CardCatalog.Pc.Id && State.Players[actor].EquipmentDie is >= 1 and <= 6 && OwnDiePending(actor);

    public CommandResult UsePc(int actor)
    {
        if (!CanUsePc(actor)) return CommandResult.Fail("PC 8BITS requiere un dado propio pendiente y un valor guardado.");
        var p = State.Players[actor]; var stored = p.EquipmentDie;
        var attack = State.PendingAttackRoll; var effect = State.PendingDieRoll;
        var current = attack?.Die ?? effect!.Die;
        var die = stored <= 3 ? Math.Min(5, current + stored) : stored;
        DestroyEquipment(p);
        if (attack != null) { State.PendingAttackRoll = attack with { Die = die, Modified = true }; State.LastAttackDie = die; }
        else State.PendingDieRoll = effect! with { Die = die, Modified = true };
        Log($"{p.Name} destruye PC 8BITS: dado {current} → {die} (valor guardado {stored}).");
        if (attack != null && !CanRespondToAttackDie()) FinishAttackRoll(die, attack.WasRerolled);
        else if (effect != null && !CanRespondToEffectDie()) ResolveDieReroll(actor, false);
        return CommandResult.Ok();
    }
}
