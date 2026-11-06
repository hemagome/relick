using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private MemoryZoneButton? PileTarget(int owner, CardZone zone) =>
        _root.FindChild($"{(zone == CardZone.Corrupted ? "CorruptedMemory" : "Memory")}Button{owner}", true, false) as MemoryZoneButton;

    private static Rect2 PileCardRect(MemoryZoneButton pile) => new(pile.GlobalPosition + pile.CardRect.Position, pile.CardRect.Size);

    private void QueuePileMovement(PileMovement movement)
    {
        if (movement.Source == CardZone.Hand) PreserveHandForDeparture(movement.Owner);
        Control? source = movement.Source switch
        {
            CardZone.Hand => DrawTarget(new(movement.Owner, movement.Index)),
            CardZone.Memory or CardZone.Corrupted => PileTarget(movement.Owner, movement.Source),
            CardZone.Bonus => BonusTarget(movement.Owner),
            CardZone.Equipment => _root.FindChild($"EquipmentZone{movement.Owner}", true, false) as Control,
            CardZone.Attached => _root.FindChild($"AttachmentStack{movement.Owner}", true, false) as Control,
            CardZone.Exchange => (_root.FindChild($"BonusTrack{movement.Owner}", true, false) as ExchangeTrack)?.ExchangeButtons.ElementAtOrDefault(movement.Index),
            _ => null
        };
        var origin = source switch
        {
            MemoryZoneButton pile => PileCardRect(pile),
            ZoneCardButton zone => BonusCardRect(zone),
            not null => source.GetGlobalRect(),
            _ => new Rect2(_arena.GetGlobalRect().GetCenter() - new Vector2(27, 38.5f), new(54, 77))
        };
        _drawQueue.Enqueue(new(movement.Owner, movement.Index, CardId: movement.CardId, Origin: origin,
            Duration: 0.42, PileSource: movement.Source, PileDestination: movement.Destination));
    }

    private static bool LeavesHand(DrawRequest request) => request.ReturnToDeck || request.PileSource == CardZone.Hand;
}
