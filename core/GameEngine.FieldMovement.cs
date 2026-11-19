namespace Relick.Core;

public sealed record MovedFieldCardState(int Counters = 0, int StoredDie = 0, int ExpiresOnTurn = 0,
    int? AreaSide = null, List<ShieldCharge>? ShieldCharges = null, bool Borrowed = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    int FireUsedOnTurn = 0);

public sealed partial class GameEngine
{
    private IEnumerable<FieldTarget> MovableFieldCards(int actor) => FieldCards()
        .Where(card => card.Owner == actor && card.Zone != FieldZone.Relikario && !DeckRules.IsTrap(CardCatalog.Get(card.CardId)) &&
            CompatibleFieldDestinations(card).Any());

    private IEnumerable<FieldTarget> CompatibleFieldDestinations(FieldTarget source)
    {
        var p = State.Players[source.Owner];
        var card = CardCatalog.Get(source.CardId);
        FieldTarget Destination(FieldZone zone, int slot = -1) => source with { Zone = zone, Index = slot };

        if (source.Zone != FieldZone.Bonus && p.Bonus == null)
            yield return Destination(FieldZone.Bonus);
        if (source.Zone != FieldZone.Exchange && p.Exchange.Count < 2)
            yield return Destination(FieldZone.Exchange, p.Exchange.Count);
        if (CanLearnSkill(p, card))
        {
            var occupied = Enumerable.Range(0, p.Skills.Count).Select(p.SkillPosition).ToHashSet();
            for (var position = 0; position < MaxSkills; position++)
                if (!occupied.Contains(position)) yield return Destination(FieldZone.Skill, position);
        }
        if (card.Kind == CardKind.Equipment && card.LightEquipment && p.LightEquipment is not { Count: > 0 })
            yield return Destination(FieldZone.LightEquipment, 0);
        if (card.Kind == CardKind.Equipment && !card.LightEquipment && p.Equipment == null && p.EquipmentCovers is not { Count: > 0 })
            yield return Destination(FieldZone.Equipment, 0);
        if (card.Kind == CardKind.Area && p.Area == null)
            yield return Destination(FieldZone.Area);
    }

    private void AskFieldDestination(FieldTarget source) => AskDecision(new PendingDecision
    {
        Source = CardCatalog.VictoriousManualControl.Id, Step = "destination", Kind = DecisionKind.Confirm, Decider = source.Owner,
        Instruction = $"Elige una zona vacía para {FieldCardName(source)} y paga 1 vida.",
        Continuation = new EngineContinuation { Actor = source.Owner, Target = source }
    });

    private void PayLifeAndMoveFieldCard(FieldTarget source, FieldTarget destination)
    {
        var p = State.Players[source.Owner];
        p.Health--;
        Log($"{p.Name} paga 1 vida con Control Manual Victorioso.");
        if (p.Health == 0)
        {
            if (p.HasReachedZero) { Finish(1 - source.Owner, $"{p.Name} vuelve a 0 vidas al pagar Control Manual Victorioso."); return; }
            p.HasReachedZero = true;
        }
        MoveFieldCard(p, source, destination);
    }

    private void MoveFieldCard(PlayerState p, FieldTarget source, FieldTarget destination)
    {
        State.LastCombat = $"{p.Name} mueve {FieldCardName(source)} a {VictoriousManualControlEffect.DestinationName(destination)}.";
        Log(State.LastCombat);
        if (source.Zone == FieldZone.Skill && destination.Zone == FieldZone.Skill)
        {
            (p.SkillPositions ??= [])[source.Index] = destination.Index;
            TryFinishWithGameOver();
            return;
        }

        var id = source.CardId;
        var (used, placedTurn) = source.Zone switch
        {
            FieldZone.Bonus => (p.BonusEffectUsed, p.BonusPlacedTurn),
            FieldZone.Exchange => (p.ExchangeEffectsUsed?.Contains(source.Index) == true,
                p.ExchangePlacementTurns?.GetValueOrDefault(source.Index) ?? 0),
            _ => (false, 0)
        };
        var (state, borrowed, marked) = DetachFieldCard(p, source, destination);
        var destinationZone = MovementCardZone(destination.Zone);
        var destinationIndex = destination.Zone == FieldZone.Skill ? p.Skills.Count : destination.Index;
        if (marked) p.MarkForCorruption(destinationZone, destinationIndex);
        if (borrowed && destination.Zone is FieldZone.Skill or FieldZone.Equipment)
            p.MarkForeignFieldCard(id);

        EngineContinuation? orient = null;
        switch (destination.Zone)
        {
            case FieldZone.Exchange:
            case FieldZone.Bonus:
                (p.MovedFieldCards ??= [])[destination.Index] = (state ?? new()) with { Borrowed = borrowed };
                break;
            case FieldZone.Skill:
                (p.SkillPositions ??= [])[destinationIndex] = destination.Index;
                if (state?.Counters > 0) p.SkillCounters[destinationIndex] = state.Counters;
                if (state?.FireUsedOnTurn == State.Turn) (p.IronwingFireUsed ??= []).Add(destinationIndex);
                p.Skills.Add(id);
                if (TryFinishWithGameOver()) return;
                break;
            case FieldZone.Equipment:
                p.Equipment = id;
                p.EquipmentCounters = state?.Counters ?? 0;
                p.EquipmentDie = state?.StoredDie ?? 0;
                p.EquipmentExpiresAtTurnStart = state?.ExpiresOnTurn > State.Turn;
                break;
            case FieldZone.LightEquipment:
                (p.LightEquipment ??= []).Add(new(id, borrowed));
                break;
            case FieldZone.Area:
                p.Area = id;
                p.AreaIsBorrowed = borrowed;
                p.AreaSideA = state?.AreaSide;
                if (p.AreaSideA == null)
                    orient = new EngineContinuation { Kind = ContinuationKind.OrientMovedArea, Actor = source.Owner };
                break;
        }

        CompleteFieldMovement(p, source, destination, orient, used, placedTurn);
    }

    private void CompleteFieldMovement(PlayerState p, FieldTarget source, FieldTarget destination, EngineContinuation? orient, bool used, int placedTurn)
    {
        var id = source.CardId;
        if (destination.Zone == FieldZone.Bonus)
        {
            PlaceBonus(p, id, fromDeck: false);
            p.BonusEffectUsed = used;
            if (CardCatalog.Get(id).FieldCombatDie) p.BonusPlacedTurn = placedTurn;
            CompleteExchange(p);
            return;
        }

        EngineContinuation? completed = source.Zone == FieldZone.Bonus
            ? new EngineContinuation { Kind = ContinuationKind.RefillBonus, Actor = source.Owner,
                Next = new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = source.Owner, Next = orient } }
            : orient;
        if (destination.Zone == FieldZone.Exchange)
        {
            if (used) (p.ExchangeEffectsUsed ??= []).Add(destination.Index);
            PlaceInExchange(p, id, completed, fieldPlacedTurn: placedTurn);
        }
        else if (completed != null) Continue(completed);
    }

    private (MovedFieldCardState? State, bool Borrowed, bool Marked) DetachFieldCard(PlayerState p, FieldTarget source, FieldTarget destination)
    {
        var (owner, zone, slot, id) = source;
        var cardZone = MovementCardZone(zone);
        var parked = zone is FieldZone.Exchange or FieldZone.Bonus ? RemoveMovedFieldCard(p, slot) : null;
        var borrowed = zone switch
        {
            FieldZone.Area => p.AreaIsBorrowed,
            FieldZone.LightEquipment => p.LightEquipment![slot].Borrowed,
            FieldZone.GalacticShield => p.GalacticShields![slot].Borrowed,
            FieldZone.Exchange or FieldZone.Bonus when parked != null => parked.Borrowed,
            _ => p.ReleaseForeignFieldCard(id)
        };
        var marked = p.CorruptsOnMemory(cardZone, slot);
        MovedFieldCardState? state = zone switch
        {
            FieldZone.Skill when p.CountersOnSkill(slot) != 0 => new(Counters: p.CountersOnSkill(slot)),
            FieldZone.Skill when p.IronwingFireUsed?.Contains(slot) == true => new(FireUsedOnTurn: State.Turn),
            FieldZone.Equipment when p.EquipmentCounters != 0 || p.EquipmentDie != 0 || p.EquipmentExpiresAtTurnStart =>
                new(p.EquipmentCounters, p.EquipmentDie, p.EquipmentExpiresAtTurnStart ? State.Turn + 2 : 0),
            FieldZone.Area when p.AreaSideA != null => new(AreaSide: p.AreaSideA),
            FieldZone.GalacticShield when p.GalacticShields![slot].Cards.Count != 0 => new(ShieldCharges: p.GalacticShields![slot].Cards),
            FieldZone.Exchange or FieldZone.Bonus => parked,
            _ => null
        };

        var host = destination.Zone == FieldZone.Skill ? destination with { Index = p.Skills.Count } : destination;
        if (zone == FieldZone.Skill) RemoveLearnedSkill(owner, slot, host);
        else if (zone == FieldZone.LightEquipment) RemoveLightEquipment(p, slot, host);
        else
        {
            ChangeZahiraHosts(owner, zone, slot, movedTo: host);
            p.RemoveCorruptionSlots(cardZone, slot);
            switch (zone)
            {
                case FieldZone.Bonus:
                    ForgetFieldCodeSource(p, zone);
                    p.Bonus = null;
                    p.InitialAreaWaitingInBonus = false;
                    break;
                case FieldZone.Exchange:
                    p.Exchange.RemoveAt(slot);
                    ShiftExchangeEffects(p, slot, 1);
                    break;
                case FieldZone.Equipment:
                    if (p.EquipmentStackedCopies > 0) p.EquipmentStackedCopies--;
                    else ClearEquipment(p);
                    break;
                case FieldZone.EquipmentCover:
                    p.EquipmentCovers!.RemoveAt(slot);
                    if (p.EquipmentCovers.Count == 0) p.EquipmentCovers = null;
                    break;
                case FieldZone.Area: ClearArea(p); break;
                case FieldZone.Attached: p.AttachedCards.RemoveAt(slot); break;
                case FieldZone.GalacticShield:
                    p.GalacticShields!.RemoveAt(slot);
                    if (p.GalacticShields.Count == 0) p.GalacticShields = null;
                    break;
            }
        }
        return (state, borrowed, marked);
    }

    private static CardZone MovementCardZone(FieldZone zone) => zone switch
    {
        FieldZone.Bonus => CardZone.Bonus, FieldZone.Exchange => CardZone.Exchange, FieldZone.Skill => CardZone.Skill,
        FieldZone.Equipment => CardZone.Equipment, FieldZone.LightEquipment => CardZone.LightEquipment,
        FieldZone.EquipmentCover => CardZone.EquipmentCover, FieldZone.Area => CardZone.Area, FieldZone.Attached => CardZone.Attached,
        FieldZone.GalacticShield => CardZone.GalacticShield, _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };

    private static MovedFieldCardState? RemoveMovedFieldCard(PlayerState p, int index)
    {
        if (p.MovedFieldCards?.Remove(index, out var state) != true) return null;
        if (p.MovedFieldCards.Count == 0) p.MovedFieldCards = null;
        return state;
    }

    private static void ShiftMovedFieldCards(PlayerState p, int removed, int count)
    {
        if (p.MovedFieldCards is not { } cards) return;
        foreach (var (slot, state) in cards.Where(pair => pair.Key >= removed).OrderBy(pair => pair.Key).ToArray())
        {
            cards.Remove(slot);
            if (slot >= removed + count) cards[slot - count] = state;
        }
        if (cards.Count == 0) p.MovedFieldCards = null;
    }

    private static void MoveNeutralCardState(PlayerState p, int source, int destination)
    {
        if (RemoveMovedFieldCard(p, source) is { } state)
            (p.MovedFieldCards ??= [])[destination] = state;
    }

    private bool? ReleaseMovedFieldCard(PlayerState p, int slot)
    {
        var state = RemoveMovedFieldCard(p, slot);
        if (state?.ShieldCharges is { } charges)
            foreach (var charge in charges)
                SendToMemory(p, charge.CardId, CardZone.ShieldCharge, slot, notify: false, marked: charge.CorruptOnMemory);
        return state?.Borrowed;
    }
}
