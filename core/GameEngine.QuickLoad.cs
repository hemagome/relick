namespace Relick.Core;

public sealed partial class GameEngine
{
    private void ResolveQuickLoadDie(int actor, int die)
    {
        var p = State.Players[actor];
        if (_activeActivation is { } frame) frame.CanInterrupt = false;
        if (die >= 5)
        {
            var count = die == 5 ? 1 : 2;
            State.LastCombat = $"{p.Name} · Carga Rápida · D6 = {die} · Roba {count} carta(s) adicional(es).";
            Log(State.LastCombat); DrawCards(p, count);
            return;
        }
        State.DiscardsRemaining = Math.Min(die <= 2 ? 2 : 1, p.Hand.Count);
        State.LastCombat = $"{p.Name} · Carga Rápida · D6 = {die} · Elige {State.DiscardsRemaining} carta(s) para descartar.";
        Log(State.LastCombat);
        // The chosen result is committed. The shared discard flow owns its choices and triggers.
        _activeActivation = null;
        if (State.DiscardsRemaining > 0)
        {
            State.DiscardPlayer = actor; State.DiscardResumePhase = Phase.Actions;
            State.Phase = Phase.Discard;
        }
    }
}
