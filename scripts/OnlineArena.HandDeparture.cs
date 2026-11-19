using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    /// <summary>
    /// Several cards leaving our hand in one move (Destiny's discard, a COMBO cost…). The hand keeps showing them
    /// until each one takes off, so they disappear one by one instead of all at once; the hand closes up afterwards.
    /// <see cref="Slots"/> maps each departure cue to the card's slot in <see cref="Hand"/>.
    /// </summary>
    private sealed record HandDeparture(string?[] Hand, int?[]? Owners, Dictionary<DuelVisualEvent, int> Slots);
    private HandDeparture? _handDeparture;

    private static bool LeavesHand(DuelVisualEvent cue, int seat) => cue.Player == seat &&
        (cue.Kind == DuelVisualKind.Pile && cue.Source == CardZone.Hand || cue.Kind == DuelVisualKind.Return);

    /// <summary>Only when our hand merely loses two or more cards, so the previous hand is a faithful picture.</summary>
    private static HandDeparture? CaptureHandDeparture(DuelSnapshot previous, DuelSnapshot current)
    {
        var seat = current.MySeat;
        var departures = current.Visuals.Where(cue => LeavesHand(cue, seat)).ToArray();
        if (departures.Length < 2) return null;
        if (current.Visuals.Any(cue => cue.Kind is DuelVisualKind.Draw or DuelVisualKind.BonusClaim or DuelVisualKind.MemoryClaim or
                DuelVisualKind.HandSteal or DuelVisualKind.HandTransfer or DuelVisualKind.HandDiscard && (cue.Player == seat || cue.Target == seat)))
            return null;
        var before = previous.Players[seat].Hand;
        // Each cue names the slot at the moment it left: replay the removals to find the original slot.
        var remaining = Enumerable.Range(0, before.Length).ToList();
        var slots = new Dictionary<DuelVisualEvent, int>();
        foreach (var cue in departures)
        {
            if (cue.Value < 0 || cue.Value >= remaining.Count) return null;
            slots[cue] = remaining[cue.Value]; remaining.RemoveAt(cue.Value);
        }
        var after = current.Players[seat].Hand;
        if (!remaining.Select(i => before[i]).SequenceEqual(after)) return null;
        return new(before, previous.Players[seat].HandOwners, slots);
    }

    /// <summary>The departing card's place in the hand empties as it takes off.</summary>
    private void LaunchHandDeparture(Flight flight)
    {
        if (_handDeparture?.Slots.TryGetValue(flight.Event, out var slot) == true && ZoneNode(0, CardZone.Hand, slot) is { } card)
            card.Modulate = new(1, 1, 1, 0);
    }

    /// <summary>Once the last card has left, the hand is rebuilt from the live snapshot and closes up.</summary>
    private bool FinishHandDeparture()
    {
        if (_handDeparture is not { } departure || _flight != null || _flights.Any(f => departure.Slots.ContainsKey(f.Event))) return false;
        _handDeparture = null;
        Render();
        SetQueuedDestinationsPending(true);
        _flightLayoutFrames = 2;
        return true;
    }
}
