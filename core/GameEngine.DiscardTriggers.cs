namespace Relick.Core;

public sealed class DiscardTriggerBatch
{
    public int Actor { get; set; }
    public int Draws { get; set; }
    public bool DeclinedRedirect { get; set; }
    public List<ActivationDiscard> Redirectable { get; set; } = [];
}

public sealed partial class GameEngine
{
    private int WildDriveCopies(int actor) => State.Players[actor].Skills.Select((id, slot) => (id, slot))
        .Count(card => card.id == CardCatalog.WildDrive.Id && !EffectsSuppressed(actor, FieldZone.Skill, card.slot));

    private void RecordDiscard(int actor, string id, MemorySelection? landed)
    {
        var draws = CardEffects.For(id)?.DrawOnDiscard ?? 0;
        var redirect = landed != null && State.Players[actor].WildDriveRedirects < WildDriveCopies(actor);
        if (draws == 0 && !redirect) return;

        var queue = State.DiscardTriggers ??= [];
        var batch = queue.LastOrDefault();
        if (batch == null || batch.Actor != actor || batch.DeclinedRedirect)
        {
            batch = new() { Actor = actor };
            queue.Add(batch);
        }
        batch.Draws += draws;
        if (redirect) batch.Redirectable.Add(new(id, landed!.Index, landed.Owner));
    }

    private MemorySelection? DiscardHandCard(PlayerState player, int index)
    {
        var id = PlayableHandCard(player, index)!;
        var landed = SendHandToMemory(player, index);
        RecordDiscard(Seat(player), id, landed);
        return landed;
    }

    private void ShiftDiscardTriggerIndices(int owner, int removed)
    {
        foreach (var batch in State.DiscardTriggers ?? [])
        {
            batch.Redirectable.RemoveAll(card => card.MemoryOwner == owner && card.MemoryIndex == removed);
            for (var index = 0; index < batch.Redirectable.Count; index++)
            {
                var card = batch.Redirectable[index];
                if (card.MemoryOwner == owner && card.MemoryIndex > removed)
                    batch.Redirectable[index] = card with { MemoryIndex = card.MemoryIndex - 1 };
            }
        }
    }

    private bool TryRunDiscardEffects()
    {
        if (State.PendingSniper != null || State.DiscardTriggers is not { Count: > 0 } queue) return false;
        if (!State.Phase.IsTurnStage() && State.Phase is not (Phase.Discard or Phase.WdResolving) &&
            !(State.Phase == Phase.Decision && State.PendingDecision?.TargetResponse?.Played == true) &&
            !(State.Phase == Phase.TurnBuffResolving && State.PendingTurnBuff?.Completed == true) &&
            !(State.Phase == Phase.ReloadResolving && State.PendingReload?.Stage >= 2)) return false;

        while (queue.Count > 0)
        {
            var batch = queue[0];
            var player = State.Players[batch.Actor];
            if (!batch.DeclinedRedirect && batch.Redirectable.Count > 0 && player.Exchange.Count < 2 &&
                player.WildDriveRedirects < WildDriveCopies(batch.Actor))
            {
                AskDecision(new PendingDecision { Source = CardCatalog.WildDrive.Id, Step = WildRedirect, Decider = batch.Actor,
                    Kind = DecisionKind.ChooseCard, Instruction = "Puedes enviar una de las cartas descartadas a tu canje." });
                return true;
            }

            queue.RemoveAt(0);
            if (queue.Count == 0) State.DiscardTriggers = null;
            if (batch.Draws == 0) continue;
            Log($"{player.Name} roba {batch.Draws} carta(s) por Bala.");
            DrawCards(player, batch.Draws, new EngineContinuation { Kind = ContinuationKind.DiscardEffectsComplete, Actor = batch.Actor });
            return true;
        }
        return false;
    }
}
