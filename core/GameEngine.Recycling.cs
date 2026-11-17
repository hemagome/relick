namespace Relick.Core;

public sealed partial class GameEngine
{
    public int RecyclingCount => State.Phase == Phase.RecycleChoice
        ? Math.Min(State.PendingDieRoll!.Die, State.Players[State.DecisionPlayer].Memory.Count) : 0;

    public CommandResult ResolveRecycling(int actor, IEnumerable<int> memoryIndices)
    {
        if (State.Phase != Phase.RecycleChoice || actor != State.DecisionPlayer)
            return CommandResult.Fail("No te corresponde elegir cartas para reciclar.");
        var p = State.Players[actor];
        var indices = memoryIndices.ToArray();
        if (indices.Length != RecyclingCount || indices.Distinct().Count() != indices.Length ||
            indices.Any(i => i < 0 || i >= p.Memory.Count))
            return CommandResult.Fail($"Selecciona exactamente {RecyclingCount} carta(s) distintas de tu memoria.");

        // Canonical ordering makes an unordered UI selection replay identically.
        Array.Sort(indices);
        p.Deck.AddRange(indices.Select(i => p.Memory[i]));
        foreach (var i in indices.Reverse()) p.Memory.RemoveAt(i);
        Shuffle(p);
        if (indices.Length > 0) CardsReturnedToDeck?.Invoke(actor);
        Log($"{p.Name} devuelve {indices.Length} carta(s) elegidas de su memoria al mazo y lo baraja.");
        var pending = State.PendingDieRoll!;
        var effect = _pendingDieEffect!;
        State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = pending.ResumePhase;
        Continue(effect.Next);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
}
