namespace Relick.Core;

public sealed partial class GameEngine
{
    private bool HasFutureEraSideB() => State.Players.Select((p, index) => (p, index)).Any(area =>
        area.p.Area == CardCatalog.FutureEra.Id && area.p.AreaSideA.HasValue && !EffectsSuppressed(area.index, FieldZone.Area));

    public bool CanPrepareD10(int actor) => actor is >= 0 and < 2 && !State.IsOver && State.Phase != Phase.Preparation &&
        State.Players[actor].FutureDie != FutureDieState.Used && HasFutureEraSideB();

    public CommandResult PrepareD10(int actor, bool prepare)
    {
        var choice = prepare ? FutureDieState.Prepared : FutureDieState.Ready;
        if (!CanPrepareD10(actor) || State.Players[actor].FutureDie == choice)
            return CommandResult.Fail("No puedes cambiar la próxima tirada con Era Futurista.");
        State.Players[actor].FutureDie = choice;
        return CommandResult.Ok();
    }

    public bool CanConvertD10(int actor) => OwnDiePending(actor) &&
        (State.Phase == Phase.AttackRerollChoice ? State.PendingAttackRoll!.Die : State.PendingDieRoll!.Die) > 6;

    public CommandResult ConvertD10(int actor)
    {
        if (!CanConvertD10(actor)) return CommandResult.Fail("Solo puedes convertir un resultado propio de 7 a 10.");
        var die = State.PendingAttackRoll is { Rerolls: > 0 } && State.Players[actor].Relikario.ConvertRerolledSix ? 5 : 6;
        State.LastDie = die;
        CurrentDieCaption = die == 5 ? "Era Futurista · 6 se convierte en 5 al relanzar" : "Era Futurista · D10 convertido en 6";
        DieAdjusted?.Invoke(die);
        Log($"{State.Players[actor].Name} convierte el resultado de su D10 en 6 con Era Futurista.");
        if (State.PendingAttackRoll is { } attack)
        {
            State.PendingAttackRoll = attack with { Die = die, Modified = true };
            State.LastAttackDie = die;
            if (!CanRespondToAttackDie()) FinishAttackRoll(die, attack.WasRerolled);
        }
        else
        {
            State.PendingDieRoll = State.PendingDieRoll! with { Die = 6, Modified = true };
            if (!CanRespondToEffectDie()) return ResolveDieReroll(actor, false);
        }
        return CommandResult.Ok();
    }

    private bool UsesD10(int actor) => State.Players[actor].FutureDie == FutureDieState.Prepared && HasFutureEraSideB();
}
