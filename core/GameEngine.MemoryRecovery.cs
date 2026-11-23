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
                    frame.CardId == CardCatalog.FloppyDisk.Id && frame.MemorySeat == owner && frame.MemoryIndex == index) continue;
                yield return new(owner, index);
            }
    }

    private string TakeFromMemory(int actor, int owner, int memoryIndex, string effectName = "Disquete", bool mark = false)
    {
        var source = State.Players[owner]; var recipient = State.Players[actor]; var id = source.Memory[memoryIndex];
        RemoveMemoryCard(new(owner, memoryIndex));
        recipient.Hand.Add(id);
        if (mark) recipient.MarkForCorruption(CardZone.Hand, recipient.Hand.Count - 1);
        if (owner != actor) MarkBorrowedHandCard(recipient, recipient.Hand.Count - 1);
        MemoryCardClaimed?.Invoke(owner, actor, memoryIndex, recipient.Hand.Count - 1);
        // The source memory is public, but the card is not marked revealed in its new hand.
        State.LastCombat = $"{recipient.Name} añade {CardCatalog.Get(id).Name} de la memoria de {source.Name} a su mano con {effectName}.";
        Log(State.LastCombat);
        return id;
    }

    public event Action<int, int, int, int>? MemoryCardClaimed;
}
