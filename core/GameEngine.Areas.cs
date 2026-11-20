namespace Relick.Core;

public sealed partial class GameEngine
{
    /// <summary>Who gets side A of a two-sided Area; Era Futurista, Robo de Equipos and Agujero Negro ask it.</summary>
    internal static DecisionChoice[] AreaSideChoices(int actor) =>
    [
        new() { Label = "Lado A para mí · Lado B para el rival", Value = actor, Public = "elige el lado A para sí." },
        new() { Label = "Lado B para mí · Lado A para el rival", Value = 1 - actor, Public = "elige el lado B para sí." }
    ];

    public bool CanActivateArea(int actor, int index) => ValidateAction(actor) == null &&
        State.Players[actor] is { Area: null } p && IsArea(PlayableHandCard(p, index));

    public CommandResult ActivateArea(int actor, int index)
    {
        var error = ValidateAction(actor);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Players[actor];
        if (p.Area != null) return CommandResult.Fail("Retira primero tu Área activa.");
        if (!IsArea(PlayableHandCard(p, index))) return CommandResult.Fail("Elige un Área de tu mano.");
        p.Area = MoveHandCardToField(p, index, CardZone.Area, -1);
        State.ActionsLeft--;
        NotifyPileMove(p, p.Area, CardZone.Hand, index, CardZone.Area);
        Log($"{p.Name} activa {CardCatalog.Get(p.Area).Name}.");
        CompletePendingPlay();
        return CommandResult.Ok();
    }

    internal static bool IsArea(string? id) => id != null && CardCatalog.Get(id).Kind == CardKind.Area;

    private void PromoteInitialAreas()
    {
        foreach (var p in State.Players)
        {
            if (!IsArea(p.Bonus) || p.Area != null) continue;
            MoveBonusToArea(p);
            if (p.Deck.Count == 0) continue;
            var id = p.Deck[0];
            if (p.CorruptsOnMemory(CardZone.Deck, 0)) p.MarkForCorruption(CardZone.Bonus, -1);
            p.Deck.RemoveAt(0); p.RemoveCorruptionSlots(CardZone.Deck, 0);
            PlaceBonus(p, id);
            p.InitialAreaWaitingInBonus = IsArea(id);
        }
    }

    private void MoveBonusToArea(PlayerState p)
    {
        var id = p.Bonus!;
        p.Area = id;
        p.AreaIsBorrowed = false;
        p.Bonus = null;
        p.InitialAreaWaitingInBonus = false;
        if (p.CorruptsOnMemory(CardZone.Bonus, -1)) p.MarkForCorruption(CardZone.Area, -1);
        p.ClearCorruptionMarks(CardZone.Bonus);
        ForgetFieldCodeSource(p, FieldZone.Bonus);
        ChangeZahiraHosts(Seat(p), FieldZone.Bonus, -1, movedTo: new(Seat(p), FieldZone.Area, -1, id));
        NotifyPileMove(p, id, CardZone.Bonus, destination: CardZone.Area);
        Log($"{p.Name} coloca {CardCatalog.Get(id).Name} en su Área.");
    }

    private void ClearArea(PlayerState p)
    {
        var seat = Seat(p);
        ChangeZahiraHosts(seat, FieldZone.Area, -1);
        p.Area = null;
        p.AreaIsBorrowed = false;
        p.AreaSideA = null;
        p.AreaNeedsBonus = false;
        State.AreaRewards?.Remove(seat);
        if (State.AreaRewards?.Count == 0) State.AreaRewards = null;
        p.ClearCorruptionMarks(CardZone.Area);
        if (!p.InitialAreaWaitingInBonus || !IsArea(p.Bonus)) return;
        MoveBonusToArea(p);
        p.AreaNeedsBonus = true;
    }

    private void MarkBorrowedHandCard(PlayerState player, int index)
    {
        player.MarkForeignHandCard(index);
        ObserveBorrowedEquipment(Seat(player), player.Hand[index]);
    }

    private void MarkBorrowedFieldCard(PlayerState player, string id)
    {
        player.MarkForeignFieldCard(id);
        ObserveBorrowedEquipment(Seat(player), id);
    }

    private void ObserveBorrowedEquipment(int recipient, string id)
    {
        if (CardCatalog.Get(id).Kind != CardKind.Equipment && id != CardCatalog.SuperMagnet.Id) return;
        for (var holder = 0; holder < State.Players.Length; holder++)
        {
            var area = State.Players[holder];
            if (area.Area != CardCatalog.EquipmentRaid.Id || area.AreaSideA != recipient ||
                EffectsSuppressed(holder, FieldZone.Area) || State.AreaRewards?.Contains(holder) == true) continue;
            (State.AreaRewards ??= []).Add(holder);
        }
    }

    internal bool PresentPendingArea()
    {
        // Resolve the action and its dice/replacements before opening another player's decision.
        if (!State.Phase.IsTurnStage() ||
            _activeActivation != null || State.PendingAttack != null || State.PendingSniper != null) return false;
        if (State.AreaRewards is { Count: > 0 } rewards)
        {
            var holder = rewards[0];
            rewards.RemoveAt(0);
            if (rewards.Count == 0) State.AreaRewards = null;
            PresentEquipmentRaidReward(holder);
            return true;
        }
        foreach (var holder in new[] { State.CurrentPlayer, 1 - State.CurrentPlayer })
        {
            var p = State.Players[holder];
            if (p.Area == null || p.AreaSideA != null) continue;
            if (p.AreaNeedsBonus)
            {
                p.AreaNeedsBonus = false;
                RefillBonus(p);
            }
            else AskAreaOrientation(holder);
            return true;
        }
        return false;
    }
}
