namespace Relick.Core;

public sealed record MemorySelection(int Owner, int Index);

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

    /// <summary>A Resetear la Mente is choosing, as a decision, also one a COMBO interrupted.</summary>
    private bool MindResetPending()
    {
        for (var decision = State.PendingDecision; decision != null; decision = decision.Suspended)
            if (decision.Source == CardCatalog.MindReset.Id) return true;
        return false;
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

    private CommandResult PlayMindReset(int actor, int index, int actionCost)
    {
        var p = State.Players[actor]; var card = CardCatalog.Get(PlayableHandCard(p, index)!);
        SendHandToMemory(p, index); p.ActionsLeft -= actionCost;
        BeginActivation(new EngineActivation(actor, card.Id, actionCost, 0, State.Phase)
            { Kind = ActivationKind.MindReset, Parent = _activeActivation });
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
                p.Deck.Add(RemoveMemoryCard(selection));
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
}
