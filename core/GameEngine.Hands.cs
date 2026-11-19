namespace Relick.Core;

public sealed partial class GameEngine
{
    // Presentation signal: owner, recipient, number of cards. Never carries identities.
    public event Action<int, int, int>? HandTransferred;

    private void MoveCardBetweenHands(PlayerState source, PlayerState receiver, int index)
    {
        var id = source.Hand[index];
        var slot = receiver.Hand.Count;
        if (source.CorruptsOnMemory(CardZone.Hand, index)) receiver.MarkForCorruption(CardZone.Hand, slot);
        var locked = HandCardLocked(source, index);
        var returning = source.RemoveHandCard(index);
        receiver.Hand.Add(id);
        if (locked) (receiver.LockedGhostCards ??= []).Add(slot);
        if (!returning) MarkBorrowedHandCard(receiver, slot);
        HandCardTaken?.Invoke(Seat(source), Seat(receiver), index, slot);
    }

    private void SwapHands(int actor)
    {
        var p = State.Players[actor]; var rival = State.Players[1 - actor];
        var ownCount = p.Hand.Count; var rivalCount = rival.Hand.Count;
        // The code and any countereffects have already left the hands. Revelations
        // travel with those exact copies, not with the slots on their old owner.
        (p.Hand, rival.Hand) = (rival.Hand, p.Hand);
        (p.LockedGhostCards, rival.LockedGhostCards) = (rival.LockedGhostCards, p.LockedGhostCards);
        p.GhostDogReveals = null; rival.GhostDogReveals = null;
        // Ownership stays with each copy: what was the rival's own is now borrowed, and vice versa.
        var borrowed = p.ForeignHandSlots ?? []; var rivalBorrowed = rival.ForeignHandSlots ?? [];
        p.ForeignHandSlots = Enumerable.Range(0, p.Hand.Count).Except(rivalBorrowed).ToArray();
        rival.ForeignHandSlots = Enumerable.Range(0, rival.Hand.Count).Except(borrowed).ToArray();
        foreach (var index in p.ForeignHandSlots ?? []) ObserveBorrowedEquipment(actor, p.Hand[index]);
        foreach (var index in rival.ForeignHandSlots ?? []) ObserveBorrowedEquipment(1 - actor, rival.Hand[index]);
        var revealed = p.RevealedHandSlots;
        p.RevealedHandSlots = rival.RevealedHandSlots;
        rival.RevealedHandSlots = revealed;
        var marked = p.CorruptionMarks?.GetValueOrDefault(CardZone.Hand);
        var rivalMarked = rival.CorruptionMarks?.GetValueOrDefault(CardZone.Hand);
        p.ClearCorruptionMarks(CardZone.Hand);
        rival.ClearCorruptionMarks(CardZone.Hand);
        foreach (var slot in rivalMarked ?? []) p.MarkForCorruption(CardZone.Hand, slot);
        foreach (var slot in marked ?? []) rival.MarkForCorruption(CardZone.Hand, slot);
        if (ownCount > 0) HandTransferred?.Invoke(actor, 1 - actor, ownCount);
        if (rivalCount > 0) HandTransferred?.Invoke(1 - actor, actor, rivalCount);
        State.LastCombat = $"{p.Name} intercambia su mano de {ownCount} carta(s) por la de {rival.Name}, de {rivalCount} carta(s).";
        Log(State.LastCombat);
    }
}
