namespace Relick.Core;

public sealed partial class GameEngine
{
    public IEnumerable<MemorySelection> RecoverableMemoryCards()
    {
        for (var owner = 0; owner < State.Players.Length; owner++)
            for (var index = 0; index < State.Players[owner].Memory.Count; index++)
            {
                // Exclude this resolving copy by position, never every card with its definition.
                if (_activeActivation is { Kind: ActivationKind.Code, ReplayCorrupted: false } frame &&
                    CardCatalog.Get(frame.CardId).RecoverMemory && frame.MemorySeat == owner && frame.MemoryIndex == index) continue;
                yield return new(owner, index);
            }
    }

    private void BeginMemoryRecovery(int actor)
    {
        if (!RecoverableMemoryCards().Any())
        {
            Log("Disquete se resuelve sin cartas disponibles en las memorias después de las respuestas.");
            State.Phase = _activeActivation?.ResumePhase ?? Phase.Actions;
            CompleteActivationIfFinished();
            return;
        }
        State.Phase = Phase.MemoryRecoveryChoice;
        State.LastCombat = $"{State.Players[actor].Name} · Disquete · Elige una carta de cualquier memoria para añadirla a tu mano.";
    }

    public CommandResult ResolveMemoryRecovery(int actor, int owner, int memoryIndex)
    {
        if (State.Phase != Phase.MemoryRecoveryChoice || actor != State.DecisionPlayer ||
            _activeActivation is not { Kind: ActivationKind.Code } frame || frame.Actor != actor || !CardCatalog.Get(frame.CardId).RecoverMemory)
            return CommandResult.Fail("No te corresponde recuperar una carta de memoria.");
        if (!RecoverableMemoryCards().Contains(new(owner, memoryIndex)))
            return CommandResult.Fail("Elige una carta disponible de una memoria normal.");

        var source = State.Players[owner]; var recipient = State.Players[actor]; var id = source.Memory[memoryIndex];
        source.Memory.RemoveAt(memoryIndex); ShiftSniperIndices(owner, memoryIndex);
        recipient.Hand.Add(id);
        if (owner != actor) recipient.MarkForeignHandCard(recipient.Hand.Count - 1);
        MemoryCardClaimed?.Invoke(owner, actor, memoryIndex, recipient.Hand.Count - 1);
        // The source memory is public, but the card is not marked revealed in its new hand.
        State.LastCombat = $"{recipient.Name} añade {CardCatalog.Get(id).Name} de la memoria de {source.Name} a su mano con Disquete.";
        Log(State.LastCombat);
        State.Phase = frame.ResumePhase;
        _activeActivation = null;
        CompleteEndTurnIfReady();
        return CommandResult.Ok();
    }

    public event Action<int, int, int, int>? MemoryCardClaimed;
}
