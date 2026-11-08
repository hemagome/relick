namespace Relick.Core;

public sealed partial class GameEngine
{
    // Current dice are D6: neither 1 nor a result that would reach the maximum is eligible.
    private static bool CanBoostWithDexterity(int die) => die is >= 2 and <= 4;

    public bool CanUseDexterity(int actor) => actor is >= 0 and < 2 && !State.IsOver &&
        State.Players[actor].Dexterity > 0 && OwnDiePending(actor) &&
        CanBoostWithDexterity(State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die);

    public CommandResult UseDexterity(int actor)
    {
        if (!CanUseDexterity(actor))
            return CommandResult.Fail("La destreza requiere una ficha y un dado propio pendiente con resultado 2, 3 o 4.");
        var p = State.Players[actor];
        var attack = State.PendingAttackRoll; var effect = State.PendingDieRoll;
        var previous = attack?.Die ?? effect!.Die; var die = previous + 1;
        p.Dexterity--;
        State.LastDie = die; CurrentDieCaption = "Destreza · +1";
        if (attack != null)
        {
            State.PendingAttackRoll = attack with { Die = die, Modified = true };
            State.LastAttackDie = die;
        }
        else State.PendingDieRoll = effect! with { Die = die, Modified = true };
        State.LastCombat = $"{p.Name} · Destreza · Dado {previous} → {die}";
        DieAdjusted?.Invoke(die);
        Log($"{p.Name} gasta una ficha de destreza: dado {previous} → {die}. Le quedan {p.Dexterity}.");
        if (attack != null && !CanRespondToAttackDie()) FinishAttackRoll(die, attack.WasRerolled);
        else if (effect != null && !CanRespondToEffectDie()) return ResolveDieReroll(actor, false);
        return CommandResult.Ok();
    }
}
