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

    /// <summary>A Resetear la Mente is choosing, as a decision (also one a COMBO interrupted) or in a duel saved in its former phase.</summary>
    private bool MindResetPending()
    {
        for (var decision = State.PendingDecision; decision != null; decision = decision.Suspended)
            if (decision.Source == CardCatalog.MindReset.Id) return true;
        return State.PendingMindReset != null;
    }

    private string? MindResetUnavailable(int actor, int excludedHand)
    {
        if (State.PendingDestruction != null || State.PendingSniper != null || MindResetPending() || State.Phase == Phase.ErrorResponse)
            return "Completa la decisión pendiente antes de activar Resetear la Mente.";
        if (MindResetMemories().Count() - (excludedHand < 0 ? 1 : 0) < 2) return "Se requieren dos cartas en las memorias.";
        if (!Enumerable.Range(0, 2).Any(owner => State.Players[owner].Hand.Count > (owner == actor && excludedHand >= 0 ? 1 : 0)))
            return "Se requiere un jugador con cartas en la mano.";
        return null;
    }

    private CommandResult PlayMindReset(int actor, int index)
    {
        var p = State.Players[actor]; var card = CardCatalog.Get(PlayableHandCard(p, index)!);
        SendHandToMemory(p, index); p.ActionsLeft -= card.ActionCost;
        BeginActivation(new EngineActivation(actor, card.Id, card.ActionCost, 0, State.Phase)
            { Kind = ActivationKind.MindReset, Parent = _activeActivation });
        return CommandResult.Ok();
    }

    // Resetear la Mente now asks through a decision (MindResetEffect). The commands below still answer it, and finish
    // duels saved while its former phase, MindResetChoice, was pending.
    private CommandResult? AnswerMindReset(int actor, string step, Func<DecisionChoice, bool> matches, string error)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision?.Source != CardCatalog.MindReset.Id) return null;
        if (State.PendingDecision.Decider != actor) return CommandResult.Fail("No te corresponde resolver Resetear la Mente.");
        return State.PendingDecision.Step == step ? ResolveDecisionWhere(actor, CardCatalog.MindReset.Id, matches) : CommandResult.Fail(error);
    }

    public CommandResult ChooseMindResetTarget(int actor, int target)
    {
        if (AnswerMindReset(actor, MindResetEffect.Target, c => target >= 0 && c.Value == target, "Elige un jugador con cartas en la mano.") is { } answered) return answered;
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
        if (AnswerMindReset(actor, MindResetEffect.Memory, c => c.Owner == owner && c.Value == index, "Elige dos cartas distintas de cualquier memoria.") is { } answered) return answered;
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
        // The hand card, or finishing without one when no target hand or no non-skill is left.
        if (State.Phase == Phase.Decision && State.PendingDecision is { Source: "mind-reset" } reset &&
            AnswerMindReset(actor, reset.Step, c => c.Owner == null && c.Value == handIndex && (reset.Step == MindResetEffect.Hand || handIndex == -1),
                "Elige una no-habilidad de la mano objetivo después de seleccionar dos cartas de memoria.") is { } answered) return answered;
        var pending = State.PendingMindReset;
        if (State.Phase != Phase.MindResetChoice || pending?.Actor != actor)
            return CommandResult.Fail("No te corresponde resolver Resetear la Mente.");
        if (pending.Target < 0)
        {
            if (handIndex != -1 || State.Players.Any(p => p.Hand.Count > 0))
                return CommandResult.Fail("Elige un jugador objetivo.");
            FinishMindReset(); CompleteEndTurnIfReady(); return CommandResult.Ok();
        }
        var target = State.Players[pending.Target];
        var hasTarget = target.Hand.Any(id => CardCatalog.Get(id).Kind != CardKind.Skill);
        if (!hasTarget)
        {
            if (handIndex != -1) return CommandResult.Fail("No hay no-habilidades en esa mano.");
            FinishMindReset(); CompleteEndTurnIfReady(); return CommandResult.Ok();
        }
        if (pending.Memories.Count != 2 || !InHand(target, handIndex) || CardCatalog.Get(target.Hand[handIndex]).Kind == CardKind.Skill)
            return CommandResult.Fail("Elige una no-habilidad de la mano objetivo después de seleccionar dos cartas de memoria.");
        ReturnMindResetCards(actor, pending.Target, pending.Memories, handIndex, pending.ResumePhase, FinishMindReset);
        return CommandResult.Ok();
    }

    /// <summary>
    /// Sends the two memory cards and the hand card to their owners' decks, shuffles them and lets the target draw. The
    /// draw may start recycling, so a die the reset interrupted waits until it finishes (<see cref="ContinuationKind.ResumeMindReset"/>).
    /// <paramref name="finish"/> ends the reset's own pending state before the draw.
    /// </summary>
    private void ReturnMindResetCards(int actor, int targetSeat, IReadOnlyList<MemorySelection> memories, int handIndex, Phase resumePhase, Action finish)
    {
        var target = State.Players[targetSeat];
        var returnsHandCard = CardCatalog.Get(target.Hand[handIndex]).CodeType != CodeType.Trap;
        var affected = memories.Select(m => m.Owner);
        // "Al mazo de su respectivo dueño": a borrowed hand card returns to the rival's deck.
        if (returnsHandCard) affected = affected.Append(target.IsForeignHandCard(handIndex) ? 1 - targetSeat : targetSeat);
        // All validation precedes mutation. Remove descending indices to retain copy identity.
        foreach (var group in memories.GroupBy(m => m.Owner))
        {
            var p = State.Players[group.Key];
            foreach (var selection in group.OrderByDescending(m => m.Index))
            {
                p.Deck.Add(p.Memory[selection.Index]); p.Memory.RemoveAt(selection.Index);
                if (State.PendingCode is { FromMemory: true } code && code.Actor == group.Key && code.MemoryIndex > selection.Index)
                    State.PendingCode = code with { MemoryIndex = code.MemoryIndex - 1 };
                // Suspended code activations keep their source indices aligned.
                for (var frame = _activeActivation?.Parent; frame != null; frame = frame.Parent)
                    if (frame.MemorySeat == group.Key && frame.MemoryIndex > selection.Index) frame.MemoryIndex--;
                    else if (frame.MemorySeat == group.Key && frame.MemoryIndex == selection.Index &&
                        !frame.ReplayCorrupted && frame.CardId == CardCatalog.FloppyDisk.Id) frame.MemoryIndex = -1;
            }
        }
        ReturnHandCardToDeck(targetSeat, handIndex);
        foreach (var owner in affected.Distinct().Order()) { Shuffle(State.Players[owner]); CardsReturnedToDeck?.Invoke(owner); }
        Log(returnsHandCard
            ? $"{State.Players[actor].Name} devuelve dos cartas de memoria y una de la mano de {target.Name} a sus mazos, los baraja y ese jugador roba una carta."
            : $"{State.Players[actor].Name} devuelve dos cartas de memoria, baraja sus mazos y retira la trampa de la mano de {target.Name}, que roba una carta.");
        var resume = new EngineContinuation { Kind = ContinuationKind.ResumeMindReset, Phase = resumePhase,
            Attack = State.PendingAttackRoll, Die = State.PendingDieRoll, SavedDieEffect = _pendingDieEffect };
        finish();
        // Drawing the last card can start recycling. Suspend the original die until that completes.
        State.PendingAttackRoll = null; State.PendingDieRoll = null; _pendingDieEffect = null;
        State.Phase = resumePhase is Phase.Combat or Phase.AttackRerollChoice or Phase.DefenseChoice or Phase.MagnetChoice ? Phase.Combat : Phase.Actions;
        DrawCards(target, 1, resume);
    }

    /// <summary>Resetear la Mente has finished choosing: its activation is over.</summary>
    private void EndMindResetActivation() => _activeActivation = _activeActivation?.Parent;

    private void FinishMindReset()
    {
        var pending = State.PendingMindReset!;
        State.PendingMindReset = null; State.Phase = pending.ResumePhase;
        _activeActivation = _activeActivation?.Parent;
    }
}
