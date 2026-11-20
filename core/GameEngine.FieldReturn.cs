namespace Relick.Core;

public sealed partial class GameEngine
{
    private IEnumerable<FieldTarget> ReturnableFieldCards() => FieldCards()
        .Where(target => target.Zone != FieldZone.Relikario);

    private void ReturnFieldCard(int actor, FieldTarget target)
    {
        var rest = new EngineContinuation { Kind = ContinuationKind.FieldReturned, Actor = actor, Target = target };
        if (TryReplaceOpponentFieldEffect(actor, target, rest)) return;
        if (!TryReplaceFieldLeave(target, FieldLeave.Return, rest))
        {
            ReturnFieldCardNow(target);
            Continue(rest);
        }
    }

    private void FinishFieldReturn(EngineContinuation next)
    {
        if (!next.Prevented && next.Target is { Zone: FieldZone.Bonus } target)
            RefillBonus(State.Players[target.Owner]);
        else CompletePendingPlay();
    }

    private void ReturnFieldCardNow(FieldTarget target)
    {
        var p = State.Players[target.Owner];
        if (target.Zone == FieldZone.Zahira) { RemoveZahira(target.Owner, ZahiraSlot(target.Index)); return; }
        if (target.Zone == FieldZone.TomeCard)
        {
            var child = RemoveTomeChild(target.Index);
            if (child != null)
            {
                ChangeZahiraHosts(target.Owner, target.Zone, target.Index);
                ReturnFieldInstance(p, child.CardId, CardZone.TomeCard, target.Index, child.OriginalOwner != target.Owner, child.Marked);
            }
            return;
        }
        if (target.Zone == FieldZone.Bonus)
        {
            var borrowedBonus = p.MovedFieldCards?.GetValueOrDefault(-1)?.Borrowed ?? p.ForeignFieldCards?.Contains(target.CardId) == true;
            TakeBonus(p, State.Players[borrowedBonus ? 1 - target.Owner : target.Owner]);
            return;
        }
        if (target.Zone == FieldZone.Area) { TakeArea(p, State.Players[p.AreaIsBorrowed ? 1 - target.Owner : target.Owner]); return; }
        if (target.Zone == FieldZone.Equipment && p.EquipmentStackedCopies == 0 && p.EquipmentCovers != null)
            for (var slot = p.EquipmentCovers.Count - 1; slot >= 0; slot--)
                ReturnFieldCardNow(new(target.Owner, FieldZone.EquipmentCover, slot, p.EquipmentCovers[slot]));
        var (state, borrowed, marked) = DetachFieldCard(p, target, null);
        ReturnFieldInstance(p, target.CardId, MovementCardZone(target.Zone), target.Index, borrowed, marked);
        if (state?.ShieldCharges is { } charges)
            foreach (var charge in charges)
                ReturnFieldInstance(p, charge.CardId, CardZone.ShieldCharge, target.Index, false, charge.CorruptOnMemory);
        TryFinishWithGameOver();
    }

    private void ReturnFieldInstance(PlayerState source, string id, CardZone zone, int index, bool borrowed, bool marked)
    {
        var owner = borrowed ? 1 - Seat(source) : Seat(source);
        var player = State.Players[owner];
        var physical = CardCatalog.PhysicalId(id);
        var destination = physical == null || DeckRules.IsTrap(CardCatalog.Get(physical)) ? CardZone.OutOfGame : CardZone.Hand;
        if (destination == CardZone.Hand)
        {
            if (marked) player.MarkForCorruption(CardZone.Hand, player.Hand.Count);
            player.Hand.Add(physical!);
        }
        NotifyPileMove(source, CardCatalog.TextId(id), zone, destination == CardZone.Hand ? player.Hand.Count - 1 : index,
            destination, destinationPlayer: owner, sourceIndex: index);
        ReturnTomeAttachments(id);
    }

    private void ReturnTomeAttachments(string id)
    {
        if (TakeTomeAttachments(id) is { } children)
            foreach (var child in children)
                ReturnFieldInstance(State.Players[child.OriginalOwner], child.CardId, CardZone.TomeCard, child.Slot, false, child.Marked);
    }
}
