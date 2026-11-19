namespace Relick.Core;

public sealed partial class GameEngine
{
    private const int PurpleRingPoints = 500;

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

    private IEnumerable<FieldTarget> AvailablePurpleRings(int actor) => LightEquipmentTargets(actor)
        .Where(c => c.CardId == CardCatalog.PurpleRing.Id && !EffectsSuppressed(actor, c.Zone, c.Index));

    public bool CanUsePurpleRing(int actor, int index) => actor is 0 or 1 && actor == State.CurrentPlayer &&
        State.Phase is Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final &&
        AvailablePurpleRings(actor).Any(c => c.Index == index);

    public CommandResult UsePurpleRing(int actor, int index)
    {
        if (!CanUsePurpleRing(actor, index)) return CommandResult.Fail("Elige un Anillo Morado activo en tu turno.");
        RequestDestruction(new(actor, FieldZone.LightEquipment, index, CardCatalog.PurpleRing.Id),
            new EngineContinuation { Kind = ContinuationKind.PurpleRingPoints, Actor = actor });
        return CommandResult.Ok();
    }

    private void AwardPurpleRingPoints(int actor)
    {
        var player = State.Players[actor];
        player.Relikia += PurpleRingPoints;
        State.LastCombat = $"{player.Name} gana {PurpleRingPoints} PR con Anillo Morado.";
        Log(State.LastCombat);
    }

    private void OfferPurpleRingDamage(int target, int amount, int attackDie, EngineContinuation? completed, List<int>? used = null)
    {
        if (amount == 0) { Continue(completed); return; }
        if (!AvailablePurpleRings(target).Any(c => used?.Contains(c.Index) != true))
        {
            ApplyDamageAfterRings(target, amount, attackDie, completed);
            return;
        }
        AskDecision(new PendingDecision
        {
            Source = CardCatalog.PurpleRing.Id, Decider = target, Step = "damage", Kind = DecisionKind.ChooseTarget,
            Instruction = $"Vas a recibir {amount} de daño. Elige un Anillo Morado o conserva tus anillos",
            Continuation = new EngineContinuation { Kind = ContinuationKind.PurpleRingDamage, Actor = target,
                Count = amount, Value = attackDie, Next = completed, UsedLightEquipment = used }
        });
    }

    private IReadOnlyList<DecisionChoice> PurpleRingChoices(PendingDecision decision)
    {
        var hit = decision.Continuation!;
        var choices = new List<DecisionChoice>
        {
            new() { Label = $"Recibir {hit.Count} de daño", Public = "decide conservar sus anillos." }
        };
        foreach (var ring in AvailablePurpleRings(decision.Decider).Where(c => hit.UsedLightEquipment?.Contains(c.Index) != true))
        {
            choices.Add(new DecisionChoice { Label = $"Destruir Anillo Morado · recibir {hit.Count / 2} de daño",
                Button = $"Recibir {hit.Count / 2} de daño", Field = ring, Primary = true, Public = "usa Anillo Morado para recibir la mitad del daño." });
            choices.Add(new DecisionChoice { Label = $"Destruir Anillo Morado · ganar {PurpleRingPoints} PR",
                Button = $"Ganar {PurpleRingPoints} PR", Field = ring, Value = 1, Public = $"usa Anillo Morado para ganar {PurpleRingPoints} PR." });
        }
        return choices;
    }

    private void ResolvePurpleRingChoice(PendingDecision decision, DecisionChoice choice)
    {
        var hit = decision.Continuation!;
        if (choice.Field == null)
        {
            ApplyDamageAfterRings(hit.Actor, hit.Count, hit.Value, hit.Next);
            return;
        }
        hit.Target = choice.Field;
        hit.Flag = choice.Value == 1;
        RequestDestruction(choice.Field, hit);
    }

    private void FinishPurpleRingDamage(EngineContinuation hit)
    {
        var amount = hit.Count;
        if (hit.Flag) AwardPurpleRingPoints(hit.Actor);
        else
        {
            amount /= 2;
            NotifyGuard(hit.Actor, hit.Value > 0 ? 1 - hit.Actor : null, CardCatalog.PurpleRing.Id, hit.Count, amount, blocked: false);
            State.LastCombat += $" · Anillo Morado: {amount} de daño";
            Log($"{State.Players[hit.Actor].Name} reduce el daño de {hit.Count} a {amount} con Anillo Morado.");
        }
        // Reverse Shuffle can pay the destruction cost with a different card. A preserved ring
        // has already responded to this hit, even though it can respond to a later one.
        var index = hit.Target!.Index;
        var used = hit.UsedLightEquipment;
        if (hit.Prevented) (used ??= []).Add(index);
        else if (used != null)
            for (var i = 0; i < used.Count; i++)
                if (used[i] > index) used[i]--;
        OfferPurpleRingDamage(hit.Actor, amount, hit.Value, hit.Next, used);
    }
}
