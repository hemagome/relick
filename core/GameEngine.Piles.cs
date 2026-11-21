namespace Relick.Core;

public enum CardZone { Hand, Equipment, Attached, Exchange, Bonus, Memory, Corrupted, Activation, Skill, EquipmentCover, OutOfGame, Zahira, Deck, Area, LightEquipment, GalacticShield, ShieldCharge, TomeCard }
/// <summary>Owner is the player the card leaves; MemoryOwner is the memory that receives it.</summary>
/// <summary>SourceIndex locates the source when Index names the destination slot, such as the shield a charge leaves.</summary>
/// <summary>FieldMove: the card changes zone within its owner's field; SourceIndex is the slot it leaves and Index the one it reaches.</summary>
public sealed record PileMovement(int Owner, string CardId, CardZone Source, int Index, CardZone Destination, int MemoryOwner = -1, int? DestinationPlayer = null,
    int SourceIndex = 0, bool FieldMove = false)
{
    public bool ToRivalMemory => Destination == CardZone.Memory && MemoryOwner >= 0 && MemoryOwner != Owner;
}

public sealed partial class GameEngine
{
    public event Action<PileMovement>? CardMovedToPile;

    private int Seat(PlayerState p) => Array.IndexOf(State.Players, p);

    private void NotifyPileMove(PlayerState p, string id, CardZone source, int index = -1, CardZone destination = CardZone.Memory,
        int memoryOwner = -1, int? destinationPlayer = null, int sourceIndex = 0, bool fieldMove = false)
        => CardMovedToPile?.Invoke(new(Seat(p), id, source, index, destination, memoryOwner, destinationPlayer, sourceIndex, fieldMove));

    private void PutFieldCardInDeck(PlayerState source, string id, CardZone zone, int slot, int owner, bool marked)
    {
        var recipient = State.Players[owner];
        if (marked) recipient.MarkForCorruption(CardZone.Deck, recipient.Deck.Count);
        recipient.Deck.Add(id);
        NotifyPileMove(source, id, zone, slot, CardZone.Deck, destinationPlayer: owner);
    }

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
        if (PlayableTomeCard(p, index) is { } tome) { InvertTomeCard(tome); return null; }
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
        bool deferCorruption = false, int? corruptionIndex = null, bool marked = false)
    {
        var holder = Seat(p);
        var foreign = source switch
        {
            CardZone.Hand => p.IsForeignHandCard(index),
            CardZone.Area => p.AreaIsBorrowed,
            CardZone.LightEquipment => p.LightEquipment![index].Borrowed,
            CardZone.GalacticShield => p.GalacticShields![index].Borrowed,
            CardZone.ShieldCharge or CardZone.Deck or CardZone.TomeCard or CardZone.Activation => false,
            CardZone.Bonus or CardZone.Exchange => ReleaseMovedFieldCard(p, index) ?? p.ReleaseForeignFieldCard(id),
            _ => p.ReleaseForeignFieldCard(id)
        };
        var owner = foreign ? 1 - holder : holder;
        var physical = CardCatalog.PhysicalId(id);
        var trap = physical == null || CardCatalog.Get(physical).CodeType == CodeType.Trap;
        var corrupt = !trap && !deferCorruption && (marked || p.CorruptsOnMemory(source, corruptionIndex ?? index));
        var destination = trap ? CardZone.OutOfGame : corrupt ? CardZone.Corrupted : CardZone.Memory;
        if (notify) NotifyPileMove(p, id, source, index, destination, trap ? -1 : owner);
        SendTomeChildrenToMemory(id);
        if (trap) return null;
        if (corrupt)
        {
            State.Players[owner].CorruptedMemory.Add(physical!);
            Log($"Una carta de {State.Players[owner].Name} se corrompe al ir a la memoria por el efecto de Osamu.");
            return null;
        }
        var memory = State.Players[owner].Memory;
        memory.Add(physical!);
        return new(owner, memory.Count - 1);
    }

    /// <summary>Moves a hand card onto its holder's field, keeping track of a borrowed copy.</summary>
    private string MoveHandCardToField(PlayerState p, int index, CardZone zone, int slot)
    {
        if (PlayableTomeCard(p, index) is { } tome)
        {
            RemoveTomeChild(tome.Slot);
            if (tome.Marked) p.MarkForCorruption(zone, slot);
            if (tome.OriginalOwner != Seat(p))
            {
                if (zone == CardZone.Area) p.AreaIsBorrowed = true;
                else MarkBorrowedFieldCard(p, tome.CardId);
            }
            NotifyPileMove(p, tome.CardId, CardZone.TomeCard, tome.Slot, zone);
            return tome.CardId;
        }
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
        var id = RemoveMemoryCard(source);
        var borrowed = source.Owner != Seat(holder);
        if (lightEquipment) InstallLightEquipment(holder, id, borrowed);
        else if (borrowed) MarkBorrowedFieldCard(holder, id);
    }

    private string RemoveMemoryCard(MemorySelection source)
    {
        var memory = State.Players[source.Owner].Memory;
        var id = memory[source.Index];
        memory.RemoveAt(source.Index);
        ShiftSniperIndices(source.Owner, source.Index);
        return id;
    }

    /// <summary>Returns the seat whose deck received the card, or -1 when a trap left the game.</summary>
    private int ReturnHandCardToDeck(int actor, int index)
    {
        var p = State.Players[actor];
        if (PlayableTomeCard(p, index) is { } tome)
        {
            RemoveTomeChild(tome.Slot);
            ChangeZahiraHosts(actor, FieldZone.TomeCard, tome.Slot);
            var deckOwner = State.Players[tome.OriginalOwner];
            if (tome.Marked) deckOwner.MarkForCorruption(CardZone.Deck, deckOwner.Deck.Count);
            deckOwner.Deck.Add(tome.CardId);
            NotifyPileMove(p, tome.CardId, CardZone.TomeCard, tome.Slot, CardZone.Deck, destinationPlayer: tome.OriginalOwner);
            return tome.OriginalOwner;
        }
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
