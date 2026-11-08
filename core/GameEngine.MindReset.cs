namespace Relick.Core;

public sealed record MemorySelection(int Owner, int Index);
public sealed class PendingMindReset
{
    public int Actor { get; set; }
    public int Target { get; set; } = -1;
    public List<MemorySelection> Memories { get; set; } = [];
    public Phase ResumePhase { get; set; }
}

public sealed partial class GameEngine
{
    public IEnumerable<MemorySelection> MindResetMemories()
    {
        for (var owner = 0; owner < 2; owner++)
            for (var index = 0; index < State.Players[owner].Memory.Count; index++)
            {
                // A memory code awaiting its response is reserved by its activation,
                // even though it is not moved to Corruptas until responses finish.
                if (State.PendingCode is { FromMemory: true } code && code.Actor == owner && code.MemoryIndex == index) continue;
                yield return new(owner, index);
            }
    }

    private string? MindResetUnavailable(int actor, int excludedHand)
    {
        if (State.PendingMindReset != null || State.Phase == Phase.ErrorResponse)
            return "Completa la decisión pendiente antes de activar Resetear la Mente.";
        if (MindResetMemories().Count() - (excludedHand < 0 ? 1 : 0) < 2) return "Se requieren dos cartas en las memorias.";
        if (!Enumerable.Range(0, 2).Any(owner => State.Players[owner].Hand.Count > (owner == actor && excludedHand >= 0 ? 1 : 0)))
            return "Se requiere un jugador con cartas en la mano.";
        return null;
    }

    private CommandResult PlayMindReset(int actor, int index)
    {
        var p = State.Players[actor]; var card = CardCatalog.Get(p.Hand[index]);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost;
        BeginActivation(new EngineActivation(actor, card.Id, card.ActionCost, 0, State.Phase)
            { Kind = ActivationKind.MindReset, Parent = _activeActivation });
        return CommandResult.Ok();
    }

    private void BeginMindReset(int actor)
    {
        State.PendingMindReset = new() { Actor = actor, ResumePhase = State.Phase };
        State.Phase = Phase.MindResetChoice;
        if (State.Players.All(p => p.Hand.Count == 0) || MindResetMemories().Count() < 2)
        {
            Log("Resetear la Mente se resuelve sin objetivos suficientes después de las respuestas.");
            FinishMindReset(); return;
        }
        State.LastCombat = $"{State.Players[actor].Name} · Resetear la Mente · Elige un jugador para mirar su mano";
    }

    public CommandResult ChooseMindResetTarget(int actor, int target)
    {
        var pending = State.PendingMindReset;
        if (State.Phase != Phase.MindResetChoice || pending?.Actor != actor || pending.Target != -1 ||
            target < 0 || target > 1 || State.Players[target].Hand.Count == 0)
            return CommandResult.Fail("Elige un jugador con cartas en la mano.");
        pending.Target = target;
        // Only the activating player may see the chosen hand. Do not mark it publicly revealed.
        State.LastCombat = $"{State.Players[actor].Name} mira la mano de {State.Players[target].Name} con Resetear la Mente.";
        return CommandResult.Ok();
    }

    public CommandResult ChooseMindResetMemory(int actor, int owner, int index)
    {
        var pending = State.PendingMindReset;
        if (State.Phase != Phase.MindResetChoice || pending?.Actor != actor || pending.Target < 0 ||
            pending.Memories.Count >= 2 || owner < 0 || owner > 1 || index < 0 || index >= State.Players[owner].Memory.Count ||
            pending.Memories.Contains(new(owner, index)) || !MindResetMemories().Contains(new(owner, index)))
            return CommandResult.Fail("Elige dos cartas distintas de cualquier memoria.");
        pending.Memories.Add(new(owner, index));
        return CommandResult.Ok();
    }

    public CommandResult ResolveMindReset(int actor, int handIndex)
    {
        var pending = State.PendingMindReset;
        if (State.Phase != Phase.MindResetChoice || pending?.Actor != actor)
            return CommandResult.Fail("No te corresponde resolver Resetear la Mente.");
        if (pending.Target < 0)
        {
            if (handIndex != -1 || State.Players.Any(p => p.Hand.Count > 0))
                return CommandResult.Fail("Elige un jugador objetivo.");
            FinishMindReset(); return CommandResult.Ok();
        }
        var target = State.Players[pending.Target];
        var hasTarget = target.Hand.Any(id => CardCatalog.Get(id).Kind != CardKind.Skill);
        if (!hasTarget)
        {
            if (handIndex != -1) return CommandResult.Fail("No hay no-habilidades en esa mano.");
            FinishMindReset(); return CommandResult.Ok();
        }
        if (pending.Memories.Count != 2 || !InHand(target, handIndex) || CardCatalog.Get(target.Hand[handIndex]).Kind == CardKind.Skill)
            return CommandResult.Fail("Elige una no-habilidad de la mano objetivo después de seleccionar dos cartas de memoria.");
        var affected = pending.Memories.Select(m => m.Owner).Append(pending.Target).Distinct().Order().ToArray();
        // All validation precedes mutation. Remove descending indices to retain copy identity.
        foreach (var group in pending.Memories.GroupBy(m => m.Owner))
        {
            var p = State.Players[group.Key];
            foreach (var selection in group.OrderByDescending(m => m.Index))
            {
                p.Deck.Add(p.Memory[selection.Index]); p.Memory.RemoveAt(selection.Index);
                if (State.PendingCode is { FromMemory: true } code && code.Actor == group.Key && code.MemoryIndex > selection.Index)
                    State.PendingCode = code with { MemoryIndex = code.MemoryIndex - 1 };
                // Suspended code activations keep their source indices aligned.
                for (var frame = _activeActivation?.Parent; frame != null; frame = frame.Parent)
                    if (frame.Actor == group.Key && frame.MemoryIndex > selection.Index) frame.MemoryIndex--;
            }
        }
        CardReturnedToDeck?.Invoke(pending.Target, handIndex, target.Hand[handIndex]);
        target.Deck.Add(target.Hand[handIndex]); target.RemoveHandCard(handIndex);
        foreach (var owner in affected) { Shuffle(State.Players[owner].Deck); CardsReturnedToDeck?.Invoke(owner); }
        Log($"{State.Players[actor].Name} devuelve dos cartas de memoria y una de la mano de {target.Name} a sus mazos, los baraja y ese jugador roba una carta.");
        var resume = new EngineContinuation { Kind = ContinuationKind.ResumeMindReset, Phase = pending.ResumePhase,
            Attack = State.PendingAttackRoll, Die = State.PendingDieRoll, SavedDieEffect = _pendingDieEffect };
        FinishMindReset();
        // Drawing the last card can start recycling. Suspend the original die until that completes.
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = pending.ResumePhase is Phase.Combat or Phase.AttackRerollChoice or Phase.DefenseChoice or Phase.MagnetChoice ? Phase.Combat : Phase.Actions;
        DrawCards(target, 1, resume);
        return CommandResult.Ok();
    }

    private void FinishMindReset()
    {
        var pending = State.PendingMindReset!;
        State.PendingMindReset = null; State.Phase = pending.ResumePhase;
        _activeActivation = _activeActivation?.Parent;
    }
}
