namespace Relick.Core;

public sealed partial class GameEngine
{
    public string? EvolutionUnavailableReason(int actor)
    {
        if (actor < 0 || actor >= State.Players.Length) return "Jugador inválido.";
        var error = ValidateTurn(actor);
        if (error != null) return error;
        if (State.Phase is not (Phase.Combat or Phase.Actions)) return "Completa el efecto pendiente antes de evolucionar.";
        var p = State.Players[actor];
        var next = RelikarioCatalog.EvolutionFor(p.Relikario);
        if (next == null) return "El Relikario ya está en su nivel máximo.";
        if (!p.HasNaturalSix) return "Requiere haber obtenido un 6 natural en un D6 durante la partida, sin relanzarlo ni modificarlo.";
        if (p.Relikia < next.EvolutionCost) return $"Necesitas {next.EvolutionCost} PR para evolucionar.";
        return null;
    }

    public bool CanEvolve(int actor) => EvolutionUnavailableReason(actor) == null;

    public CommandResult Evolve(int actor)
    {
        var error = EvolutionUnavailableReason(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        var next = RelikarioCatalog.EvolutionFor(p.Relikario)!;
        p.Relikia -= next.EvolutionCost;
        p.Relikario = next;
        p.Health = Math.Min(p.MaxHealth, p.Health + 2);
        // Evolution does not refresh an ability already used during this turn.
        Log($"{p.Name} evoluciona a {p.Relikario.Name} Lv {p.Level}: paga {next.EvolutionCost} PR, gana 2 vidas y su máximo sube a {p.MaxHealth}.");
        return CommandResult.Ok();
    }

    private void RegisterNaturalSix(PlayerState p, int die)
    {
        if (die != 6 || State.Phase == Phase.Preparation || p.HasNaturalSix) return;
        p.HasNaturalSix = true;
        Log($"{p.Name} obtiene un 6 natural: condición de evolución cumplida para esta partida.");
    }

    // A continuation pauses the effect before it consumes the chosen result.
    private Action<int>? _pendingDieContinuation;

    private void RollWithRelikario(PlayerState p, DiePurpose purpose, Action<int> receive)
    {
        var die = Roll(purpose == DiePurpose.ExtraLife ? "Vida Extra · Curación" : purpose == DiePurpose.PointsChange ? "Aumento de Relick Points" : "");
        if ((p.Relikario.CanRerollAnyDie && !p.AttackRerollUsedThisTurn && die != 1) || p.Equipment == CardCatalog.Pendant.Id || HasRerollCombo(Array.IndexOf(State.Players, p)))
        {
            _pendingDieContinuation = receive;
            State.PendingDieRoll = new(Array.IndexOf(State.Players, p), purpose, die, State.Phase);
            State.Phase = Phase.DieRerollChoice;
        }
        else
        {
            RegisterNaturalSix(p, die);
            receive(die);
        }
    }

    public CommandResult ResolveDieReroll(int actor, bool reroll)
    {
        if (State.Phase != Phase.DieRerollChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde decidir esta tirada.");
        var pending = State.PendingDieRoll!;
        var p = State.Players[actor];
        if (reroll && !CanUseYunoOnPendingDie(actor))
            return CommandResult.Fail("No puedes volver a relanzar este dado.");
        var die = pending.Die;
        var count = pending.Rerolls;
        if (reroll)
        {
            p.AttackRerollUsedThisTurn = true;
            die = Roll("Yuno Lv 2 · Relanzamiento"); count++;
            Log($"{p.Name} usa Yuno Lv 2: relanzamiento {count}/2 de {pending.Purpose}, resultado {die}.");
            State.PendingDieRoll = pending with { Die = die, Rerolls = count, WasRerolled = true, Modified = true, YunoStopped = die == 1 };
            if (CanRespondToEffectDie()) return CommandResult.Ok();
        }
        if (!State.PendingDieRoll!.Modified) RegisterNaturalSix(p, die);
        var continuation = _pendingDieContinuation!;
        _pendingDieContinuation = null;
        State.PendingDieRoll = null;
        State.Phase = pending.ResumePhase;
        continuation(die);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}
