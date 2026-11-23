namespace Relick.Core;

public sealed partial class GameEngine
{
    public bool CanUseRelikiaEffect(int actor, RelikiaEffect effect) => RelikiaEffectUnavailableReason(actor, effect) == null;

    private string? RelikiaCommonUnavailableReason(int actor, RelikiaEffect effect)
    {
        var error = ValidateAction(actor);
        if (error != null) return error;
        if (!Enum.IsDefined(effect)) return "Efecto desconocido.";
        var definition = RelikiaEffects.Get(effect);
        if (AvailableActions(actor) < definition.Actions) return $"Necesitas {definition.Actions} acciones disponibles.";
        if (State.Active.Relikia < definition.Cost) return $"Necesitas {definition.Cost} PR.";
        return null;
    }

    public string? RelikiaEffectUnavailableReason(int actor, RelikiaEffect effect)
    {
        var error = RelikiaCommonUnavailableReason(actor, effect);
        if (error != null) return error;
        var definition = RelikiaEffects.Get(effect);
        var p = State.Active;
        return effect switch
        {
            RelikiaEffect.ChangeBonus when p.Exchange.Count != 0 => "Tu zona de canje debe estar vacía.",
            RelikiaEffect.ChangeBonus or RelikiaEffect.ClaimBonus when p.Bonus == null => "No tienes Bonus disponible.",
            RelikiaEffect.ReplayCode when !p.Memory.Where(IsCode).Any(id => CodeEffectUnavailable(p, CardCatalog.Get(id), definition.Cost) == null) => "No tienes códigos sin corromper en tu memoria.",
            _ => null
        };
    }

    public bool CanReplayMemoryCode(int actor, int index) =>
        RelikiaCommonUnavailableReason(actor, RelikiaEffect.ReplayCode) == null &&
        IsMemoryCode(actor, index) && CodeEffectUnavailable(State.Players[actor], CardCatalog.Get(State.Players[actor].Memory[index]),
            RelikiaEffects.Get(RelikiaEffect.ReplayCode).Cost) == null;

    public IReadOnlyList<int> ReplayableMemoryCodes(int actor)
    {
        if (RelikiaCommonUnavailableReason(actor, RelikiaEffect.ReplayCode) != null) return Array.Empty<int>();
        var result = new List<int>();
        var player = State.Players[actor]; var extraCost = RelikiaEffects.Get(RelikiaEffect.ReplayCode).Cost;
        for (var index = 0; index < player.Memory.Count; index++)
            if (IsMemoryCode(actor, index) && CodeEffectUnavailable(player, CardCatalog.Get(player.Memory[index]), extraCost) == null) result.Add(index);
        return result;
    }

    public CommandResult UseRelikiaEffect(int actor, RelikiaEffect effect, int memoryIndex = -1)
    {
        var error = RelikiaEffectUnavailableReason(actor, effect);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (effect == RelikiaEffect.ReplayCode && !IsMemoryCode(actor, memoryIndex))
            return CommandResult.Fail("Elige un código sin corromper de tu memoria.");
        var definition = RelikiaEffects.Get(effect);
        if (effect == RelikiaEffect.ReplayCode)
        {
            var extraError = CodeEffectUnavailable(p, CardCatalog.Get(p.Memory[memoryIndex]), definition.Cost);
            if (extraError != null) return CommandResult.Fail(extraError);
        }
        p.Relikia -= definition.Cost;
        if (effect == RelikiaEffect.ReplayCode) p.Relikia -= CodePrCost(CardCatalog.Get(p.Memory[memoryIndex]));
        State.ActionsLeft -= definition.Actions;
        Log($"{p.Name} usa Relikia: {definition.Name} ({definition.Cost} PR, {definition.ActionLabel}).");
        switch (effect)
        {
            case RelikiaEffect.ChangeBonus:
                var bonus = p.Bonus!;
                var bonusPlacedTurn = p.BonusPlacedTurn;
                MoveNeutralCardState(p, -1, p.Exchange.Count);
                if (p.CorruptsOnMemory(CardZone.Bonus, -1)) p.MarkForCorruption(CardZone.Exchange, p.Exchange.Count);
                p.ClearCorruptionMarks(CardZone.Bonus);
                ChangeZahiraHosts(actor, FieldZone.Bonus, -1, movedTo: new(actor, FieldZone.Exchange, p.Exchange.Count, bonus));
                ForgetFieldCodeSource(p, FieldZone.Bonus);
                p.Bonus = null;
                p.InitialAreaWaitingInBonus = false;
                PlaceInExchange(p, bonus, new EngineContinuation { Kind = ContinuationKind.RefillBonus, Actor = actor }, fieldPlacedTurn: bonusPlacedTurn);
                break;
            case RelikiaEffect.DrawAndPlace:
                DrawOne(p, new EngineContinuation { Kind = ContinuationKind.RelikiaPlacement, Actor = actor });
                break;
            case RelikiaEffect.ReplayCode:
                var code = CardCatalog.Get(p.Memory[memoryIndex]);
                ActivateCode(actor, code, true, memoryIndex);
                break;
            case RelikiaEffect.ClaimBonus:
                TakeBonus(p, p);
                RefillBonus(p, new EngineContinuation { Kind = ContinuationKind.RollRelikia, Actor = actor });
                break;
        }
        return CommandResult.Ok();
    }

    public CommandResult ResolveRelikiaPlacement(int actor, int handIndex, CardDestination destination)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.PlaceHandCard);
        if (error != null) return CommandResult.Fail(error);
        var p = State.Active;
        if (PlayableHandCard(p, handIndex) is not { } id || !Enum.IsDefined(destination)) return CommandResult.Fail("Elige una carta y su destino.");
        if (destination == CardDestination.Exchange && p.Bonus == null) return CommandResult.Fail("No hay Bonus disponible para canjear.");
        EndRelikiaChoice();
        if (destination == CardDestination.Exchange)
        {
            MoveHandCardToField(p, handIndex, CardZone.Exchange, p.Exchange.Count);
            PlaceInExchange(p, id);
        }
        else
        {
            var card = CardCatalog.Get(id);
            var deckOwner = ReturnHandCardToDeck(actor, handIndex);
            if (deckOwner >= 0)
            {
                CardsReturnedToDeck?.Invoke(deckOwner);
                Log($"{p.Name} envía {card.Name} al fondo del mazo por Relikia.");
            }
            else Log($"{card.Name} de {p.Name} queda fuera del juego por Relikia.");
        }
        return CommandResult.Ok();
    }

    public CommandResult ChooseRelikiaReward(int actor, RelikiaReward reward)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.BonusReward);
        if (error != null) return CommandResult.Fail(error);
        if (!Enum.IsDefined(reward)) return CommandResult.Fail("Elige un efecto adicional.");
        EndRelikiaChoice();
        if (reward == RelikiaReward.OpponentDiscards)
        {
            var next = new EngineContinuation { Kind = ContinuationKind.RelikiaOpponentDiscards, Actor = actor };
            if (!TryOfferTargetResponse(actor, 1 - actor, RelikiaEffects.Get(RelikiaEffect.ClaimBonus).Name, continuation: next)) Continue(next);
        }
        else
        {
            State.CorruptionsRemaining = Math.Min(2, State.Players.Sum(p => p.Memory.Count(IsCode)));
            Log($"{State.Active.Name} elige {State.CorruptionsRemaining} código(s) para corromper.");
            if (State.CorruptionsRemaining > 0) BeginRelikiaChoice(RelikiaChoice.CorruptCode);
        }
        return CommandResult.Ok();
    }

    private void ResolveRelikiaOpponentDiscards(int actor)
    {
        var rival = 1 - actor;
        State.DiscardsRemaining = Math.Min(2, State.Players[rival].Hand.Count);
        Log($"{State.Players[actor].Name} obliga a {State.Players[rival].Name} a descartar {State.DiscardsRemaining} carta(s) por Relikia.");
        if (State.DiscardsRemaining == 0) return;
        State.DiscardPlayer = rival;
        State.DiscardResumePhase = Phase.Actions;
        State.Phase = Phase.Discard;
    }

    public CommandResult ResolveRelikiaCorruption(int actor, int owner, int memoryIndex)
    {
        var error = ValidateRelikiaChoice(actor, RelikiaChoice.CorruptCode);
        if (error != null) return CommandResult.Fail(error);
        if (!IsMemoryCode(owner, memoryIndex)) return CommandResult.Fail("Elige un código sin corromper de una memoria.");
        Corrupt(actor, owner, memoryIndex);
        if (--State.CorruptionsRemaining == 0) EndRelikiaChoice();
        return CommandResult.Ok();
    }

    private void TakeBonus(PlayerState source, PlayerState recipient)
    {
        var id = source.Bonus!;
        var moved = RemoveMovedFieldCard(source, -1);
        var borrowed = moved?.Borrowed ?? source.ReleaseForeignFieldCard(id);
        ChangeZahiraHosts(Array.IndexOf(State.Players, source), FieldZone.Bonus, -1);
        ForgetFieldCodeSource(source, FieldZone.Bonus);
        source.Bonus = null;
        source.InitialAreaWaitingInBonus = false;
        var owner = borrowed ? 1 - Seat(source) : Seat(source);
        var marked = source.CorruptsOnMemory(CardZone.Bonus, -1);
        source.ClearCorruptionMarks(CardZone.Bonus);
        var physical = CardCatalog.PhysicalId(id);
        if (physical != null)
        {
            if (marked) recipient.MarkForCorruption(CardZone.Hand, recipient.Hand.Count);
            recipient.Hand.Add(physical);
            if (owner != Seat(recipient)) MarkBorrowedHandCard(recipient, recipient.Hand.Count - 1);
            BonusClaimed?.Invoke(Seat(source), Seat(recipient), recipient.Hand.Count - 1, physical);
        }
        else NotifyPileMove(source, id, CardZone.Bonus, destination: CardZone.OutOfGame);
        if (moved?.ShieldCharges is { } charges)
            foreach (var charge in charges)
                ReturnFieldInstance(source, charge.CardId, CardZone.ShieldCharge, -1, false, charge.CorruptOnMemory);
        ReturnTomeAttachments(id);
    }

    private void TakeArea(PlayerState source, PlayerState recipient)
    {
        var id = source.Area!;
        var owner = source.AreaIsBorrowed ? 1 - Seat(source) : Seat(source);
        if (source.CorruptsOnMemory(CardZone.Area, -1)) recipient.MarkForCorruption(CardZone.Hand, recipient.Hand.Count);
        recipient.Hand.Add(id);
        if (owner != Seat(recipient)) MarkBorrowedHandCard(recipient, recipient.Hand.Count - 1);
        NotifyPileMove(source, id, CardZone.Area, recipient.Hand.Count - 1, CardZone.Hand, destinationPlayer: Seat(recipient));
        ClearArea(source);
    }

    private void DealBonus(PlayerState p, EngineContinuation? completed = null) => MoveTopCard(p, true, completed);
    private void RefillBonus(PlayerState p, EngineContinuation? completed = null)
        => DealBonus(p, new EngineContinuation { Kind = ContinuationKind.RefillDone, Actor = Array.IndexOf(State.Players, p), Next = completed });

    private static bool IsCode(string id) => CardCatalog.Get(id).Kind == CardKind.Code;
    private bool IsMemoryCode(int owner, int index) => owner >= 0 && owner < State.Players.Length &&
        index >= 0 && index < State.Players[owner].Memory.Count && IsCode(State.Players[owner].Memory[index]);
    private void Corrupt(int actor, int owner, int index)
    {
        var p = State.Players[owner];
        var id = p.Memory[index];
        NotifyPileMove(p, id, CardZone.Memory, index, CardZone.Corrupted);
        RemoveMemoryCard(new(owner, index));
        p.CorruptedMemory.Add(id);
        Log($"{State.Players[actor].Name} corrompe {CardCatalog.Get(id).Name} de la memoria de {p.Name} (boca abajo).");
    }
    private void BeginRelikiaChoice(RelikiaChoice choice)
    {
        if (choice == RelikiaChoice.PlaceHandCard && !PlayableCards(State.CurrentPlayer).Any())
        {
            Log($"{State.Active.Name} no tiene cartas en la mano para colocar por Relikia.");
            State.Phase = Phase.Actions;
            return;
        }
        State.PendingRelikia = choice;
        State.Phase = Phase.RelikiaChoice;
    }
    private void EndRelikiaChoice()
    {
        State.PendingRelikia = null;
        State.Phase = Phase.Actions;
    }
    private string? ValidateRelikiaChoice(int actor, RelikiaChoice choice) => ValidateTurn(actor, Phase.RelikiaChoice) ??
        (State.PendingRelikia != choice ? "No está pendiente esa elección de Relikia." : null);
}
