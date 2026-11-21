namespace Relick.Core;

internal interface IAreaActionModifier
{
    (int Extra, bool Blocked) ActionModifier(bool sideA, bool ownTurn);
}

public sealed partial class GameEngine
{
    /// <summary>Who gets side A of a two-sided Area.</summary>
    internal static DecisionChoice[] AreaSideChoices(int actor) =>
    [
        new() { Label = "Lado A para mí · Lado B para el rival", Value = actor, Public = "elige el lado A para sí." },
        new() { Label = "Lado B para mí · Lado A para el rival", Value = 1 - actor, Public = "elige el lado B para sí." }
    ];

    private void AskAreaOrientation(int holder, bool moved = false)
    {
        var card = CardCatalog.Get(State.Players[holder].Area!);
        if (card == CardCatalog.BlackHole)
            AskBlackHole(holder, holder, moved ? BlackHoleOrient : BlackHoleSides, DecisionKind.Confirm, "Elige qué lado del Área se aplica a ti");
        else if (card == CardCatalog.EquipmentRaid)
            AskRaid(holder, holder, moved ? RaidOrientMovedArea : RaidSides, DecisionKind.Confirm, "Elige qué lado del Área se aplica a ti");
        else
            AskDecision(new PendingDecision
            {
                Source = card.Id, Step = moved ? "moved" : "orient", Decider = holder,
                Kind = DecisionKind.Confirm, Instruction = $"Elige qué lado de {card.Name} se aplica a ti."
            });
    }

    private int ActionsWithAreas(int actor, int actions)
    {
        for (var holder = 0; holder < State.Players.Length; holder++)
        {
            var player = State.Players[holder];
            if (player.AreaSideA is not int sideA || CardEffects.For(player.Area) is not IAreaActionModifier modifier ||
                EffectsSuppressed(holder, FieldZone.Area)) continue;
            var (extra, blocked) = modifier.ActionModifier(sideA == actor, State.CurrentPlayer == actor);
            if (blocked) return 0;
            actions += extra;
        }
        return actions;
    }

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

    private void ShuffleBonusZoneIntoDecks(PlayerState p)
    {
        var actor = Seat(p);
        var decks = new HashSet<int>();
        // Empty the Bonus first so removing the Area cannot promote a waiting Area out of it.
        if (p.Bonus is { } bonus) ReturnToDeck(new(actor, FieldZone.Bonus, -1, bonus));
        if (p.Area is { } area) ReturnToDeck(new(actor, FieldZone.Area, -1, area));
        foreach (var owner in decks.Order())
        {
            Shuffle(State.Players[owner]);
            CardsReturnedToDeck?.Invoke(owner);
        }

        void ReturnToDeck(FieldTarget target)
        {
            var (state, borrowed, marked) = DetachFieldCard(p, target, null);
            var owner = borrowed ? 1 - actor : actor;
            MoveToDeck(target.CardId, MovementCardZone(target.Zone), target.Index, owner, marked);
            if (state?.ShieldCharges is { } charges)
                foreach (var charge in charges)
                    MoveToDeck(charge.CardId, CardZone.ShieldCharge, target.Index, actor, charge.CorruptOnMemory);
            if (TakeTomeAttachments(target.CardId) is { } children)
                foreach (var child in children)
                    MoveToDeck(child.CardId, CardZone.TomeCard, child.Slot, child.OriginalOwner, child.Marked);
        }

        void MoveToDeck(string id, CardZone source, int slot, int owner, bool marked)
        {
            if (CardCatalog.PhysicalId(id) is not { } physical || DeckRules.IsTrap(CardCatalog.Get(physical)))
            {
                NotifyPileMove(p, id, source, slot, CardZone.OutOfGame);
                return;
            }
            PutFieldCardInDeck(p, physical, source, slot, owner, marked);
            decks.Add(owner);
        }
    }

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
