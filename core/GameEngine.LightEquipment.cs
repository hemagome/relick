namespace Relick.Core;

public sealed partial class GameEngine
{

    private IEnumerable<FieldTarget> LightEquipmentTargets(int owner)
    {
        if (State.Players[owner].LightEquipment is not { } cards) yield break;
        for (var i = 0; i < cards.Count; i++)
            yield return new(owner, FieldZone.LightEquipment, i, cards[i].CardId);
    }

    private void InstallLightEquipment(PlayerState player, string id, bool borrowed = false)
    {
        (player.LightEquipment ??= []).Add(new(id, borrowed));
        if (borrowed) ObserveBorrowedEquipment(Seat(player), id);
    }

    private void RemoveLightEquipment(PlayerState player, int index, FieldTarget? movedTo = null)
    {
        ChangeZahiraHosts(Seat(player), FieldZone.LightEquipment, index, movedTo: movedTo);
        player.RemoveCorruptionSlots(CardZone.LightEquipment, index);
        player.LightEquipment!.RemoveAt(index);
        if (player.LightEquipment.Count == 0) player.LightEquipment = null;
    }

    private void TransferLightEquipment(PlayerState source, PlayerState recipient, FieldTarget target)
    {
        var copy = source.LightEquipment![target.Index];
        var slot = recipient.LightEquipment?.Count ?? 0;
        var seat = Seat(recipient);
        if (source.CorruptsOnMemory(CardZone.LightEquipment, target.Index))
            recipient.MarkForCorruption(CardZone.LightEquipment, slot);
        InstallLightEquipment(recipient, copy.CardId, borrowed: !copy.Borrowed);
        RemoveLightEquipment(source, target.Index, new(seat, FieldZone.LightEquipment, slot, copy.CardId));
        NotifyPileMove(source, copy.CardId, CardZone.LightEquipment, target.Index, CardZone.LightEquipment, destinationPlayer: seat);
    }

    private IEnumerable<FieldTarget> AvailablePurpleRings(int actor) => PurpleRingEffect.Rings(State, actor, EffectsSuppressed);

    public bool CanUsePurpleRing(int actor, int index) => actor is 0 or 1 && actor == State.CurrentPlayer &&
        State.Phase.IsTurnStage() &&
        AvailablePurpleRings(actor).Any(c => c.Index == index);

    public CommandResult UsePurpleRing(int actor, int index)
    {
        if (!CanUsePurpleRing(actor, index)) return CommandResult.Fail("Elige un Anillo Morado activo en tu turno.");
        RequestDestruction(new(actor, FieldZone.LightEquipment, index, CardCatalog.PurpleRing.Id),
            new EngineContinuation { Kind = ContinuationKind.PurpleRingPoints, Actor = actor });
        return CommandResult.Ok();
    }

    private void AwardPurpleRingPoints(int actor) => PurpleRingEffect.AwardPoints(State, actor, Log);

}
