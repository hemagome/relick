namespace Relick.Core;

public enum CardZone { Hand, Equipment, Attached, Exchange, Bonus, Memory, Corrupted, Activation, Skill, EquipmentCover, OutOfGame, Zahira, Deck, Area, LightEquipment }
/// <summary>Owner is the player the card leaves; MemoryOwner is the memory that receives it.</summary>
public sealed record PileMovement(int Owner, string CardId, CardZone Source, int Index, CardZone Destination, int MemoryOwner = -1, int? DestinationPlayer = null)
{
    public bool ToRivalMemory => Destination == CardZone.Memory && MemoryOwner >= 0 && MemoryOwner != Owner;
}

public sealed partial class GameEngine
{
    public event Action<PileMovement>? CardMovedToPile;

    private int Seat(PlayerState p) => Array.IndexOf(State.Players, p);

    private void NotifyPileMove(PlayerState p, string id, CardZone source, int index = -1, CardZone destination = CardZone.Memory,
        int memoryOwner = -1, int? destinationPlayer = null)
        => CardMovedToPile?.Invoke(new(Seat(p), id, source, index, destination, memoryOwner, destinationPlayer));

    private void AttachAttackFromDeck(PlayerState p, int index)
    {
        var id = p.Deck[index];
        if (p.CorruptsOnMemory(CardZone.Deck, index)) p.MarkForCorruption(CardZone.Attached, p.AttachedCards.Count);
        p.Deck.RemoveAt(index);
        p.RemoveCorruptionSlots(CardZone.Deck, index);
        p.AttachedCards.Add(id);
        NotifyPileMove(p, id, CardZone.Deck, destination: CardZone.Attached);
        State.LastCombat = $"{p.Name} coloca un Ataque debajo de su Relikario y baraja su mazo.";
        Log(State.LastCombat);
        Shuffle(p);
        if (p.Deck.Count == 0) Exhaust(p);
        else CardsReturnedToDeck?.Invoke(Seat(p));
    }

    /// <summary>Returns where the card landed, or null when a trap leaves the game.</summary>
    private MemorySelection? SendHandToMemory(PlayerState p, int index, bool deferCorruption = false)
    {
        var id = p.Hand[index];
        var landed = SendToMemory(p, id, CardZone.Hand, index, deferCorruption: deferCorruption);
        p.RemoveHandCard(index);
        return landed;
    }

    /// <summary>
    /// Every card that leaves a hand or the field for a memory goes to its owner's memory,
    /// whoever controlled it. Call this before removing a hand card: its slot carries the ownership.
    /// </summary>
    private MemorySelection? SendToMemory(PlayerState p, string id, CardZone source, int index, bool notify = true,
        bool deferCorruption = false, int? corruptionIndex = null)
    {
        var holder = Seat(p);
        var foreign = source switch
        {
            CardZone.Hand => p.IsForeignHandCard(index),
            CardZone.Area => p.AreaIsBorrowed,
            CardZone.LightEquipment => p.LightEquipment![index].Borrowed,
            _ => p.ReleaseForeignFieldCard(id)
        };
        var owner = foreign ? 1 - holder : holder;
        var trap = CardCatalog.Get(id).CodeType == CodeType.Trap;
        var corrupt = !trap && !deferCorruption && p.CorruptsOnMemory(source, corruptionIndex ?? index);
        var destination = trap ? CardZone.OutOfGame : corrupt ? CardZone.Corrupted : CardZone.Memory;
        if (notify) NotifyPileMove(p, id, source, index, destination, trap ? -1 : owner);
        if (trap) return null;
        if (corrupt)
        {
            State.Players[owner].CorruptedMemory.Add(id);
            Log($"Una carta de {State.Players[owner].Name} se corrompe al ir a la memoria por el efecto de Osamu.");
            return null;
        }
        var memory = State.Players[owner].Memory;
        memory.Add(id);
        return new(owner, memory.Count - 1);
    }

    /// <summary>Moves a hand card onto its holder's field, keeping track of a borrowed copy.</summary>
    private string MoveHandCardToField(PlayerState p, int index, CardZone zone, int slot)
    {
        var id = p.Hand[index];
        if (p.CorruptsOnMemory(CardZone.Hand, index)) p.MarkForCorruption(zone, slot);
        var borrowed = p.RemoveHandCard(index);
        if (zone == CardZone.Area) p.AreaIsBorrowed = borrowed;
        else if (borrowed) MarkBorrowedFieldCard(p, id);
        return id;
    }

    /// <summary>Removes a memory card that returns to play under <paramref name="holder"/>'s control.</summary>
    private void MoveMemoryCardToField(MemorySelection source, PlayerState holder, bool lightEquipment = false)
    {
        var id = State.Players[source.Owner].Memory[source.Index];
        State.Players[source.Owner].Memory.RemoveAt(source.Index); ShiftSniperIndices(source.Owner, source.Index);
        var borrowed = source.Owner != Seat(holder);
        if (lightEquipment) InstallLightEquipment(holder, id, borrowed);
        else if (borrowed) MarkBorrowedFieldCard(holder, id);
    }

    /// <summary>Returns the seat whose deck received the card, or -1 when a trap left the game.</summary>
    private int ReturnHandCardToDeck(int actor, int index)
    {
        var p = State.Players[actor];
        var id = p.Hand[index];
        if (CardCatalog.Get(id).CodeType == CodeType.Trap)
        {
            SendHandToMemory(p, index);
            return -1;
        }
        CardReturnedToDeck?.Invoke(actor, index, id);
        var marked = p.CorruptsOnMemory(CardZone.Hand, index);
        var owner = p.RemoveHandCard(index) ? 1 - actor : actor;
        var recipient = State.Players[owner];
        if (marked) recipient.MarkForCorruption(CardZone.Deck, recipient.Deck.Count);
        recipient.Deck.Add(id);
        return owner;
    }

    // Equipment and Tótem de Oro wait for responses in memory and only stay there when placement fails.
    private bool CorruptWaitingFieldCard(EngineActivation frame)
    {
        if (!frame.CorruptOnMemory) return false;
        frame.CorruptOnMemory = false;
        var p = State.Players[frame.MemorySeat];
        var index = frame.MemoryIndex;
        var id = p.Memory[index];
        NotifyPileMove(State.Players[frame.Actor], id, CardZone.Activation, destination: CardZone.Corrupted, memoryOwner: frame.MemorySeat);
        p.Memory.RemoveAt(index);
        ShiftSniperIndices(frame.MemorySeat, index);
        p.CorruptedMemory.Add(id);
        Log($"Una carta de {p.Name} se corrompe al ir a la memoria por el efecto de Osamu.");
        return true;
    }
}
