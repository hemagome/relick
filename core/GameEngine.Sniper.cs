namespace Relick.Core;
public sealed class PendingSniper
{
    public int Actor { get; set; }
    public List<int> MemoryIndices { get; set; } = [];
    // Seat whose memory holds each discarded copy; null means every copy is in the actor's memory.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? MemoryOwners { get; set; }
    public int MemoryOwner(int entry) => MemoryOwners?[entry] ?? Actor;
    public Phase ResumePhase { get; set; }
}
public sealed partial class GameEngine
{
    private void BeginSniperChoices(int actor, IEnumerable<MemorySelection> discarded)
    {
        var cards = discarded.ToList(); if (cards.Count == 0 || State.IsOver) return;
        State.PendingSniper = new() { Actor = actor, MemoryIndices = cards.Select(card => card.Index).ToList(), ResumePhase = State.Phase,
            MemoryOwners = cards.Any(card => card.Owner != actor) ? cards.Select(card => card.Owner).ToList() : null };
        State.Phase = Phase.SniperChoice;
    }
    public CommandResult ResolveSniper(int actor, bool equip)
    {
        var pending = State.PendingSniper;
        if (State.Phase != Phase.SniperChoice || pending?.Actor != actor) return CommandResult.Fail("No te corresponde resolver Mira de Sniper.");
        var index = pending.MemoryIndices[0]; var p = State.Players[actor];
        var owner = pending.MemoryOwner(0); var memory = State.Players[owner].Memory;
        if (index < 0 || index >= memory.Count || memory[index] != CardCatalog.Sniper.Id) return CommandResult.Fail("La carta descartada no está disponible.");
        pending.MemoryIndices.RemoveAt(0); pending.MemoryOwners?.RemoveAt(0);
        State.Phase = pending.MemoryIndices.Count > 0 ? Phase.SniperChoice : pending.ResumePhase;
        if (pending.MemoryIndices.Count == 0) State.PendingSniper = null;
        if (equip)
            BeginActivation(new EngineActivation(actor, CardCatalog.Sniper.Id, 0, 0, State.Phase)
                { Kind = ActivationKind.Equipment, MemoryIndex = index, MemoryOwner = owner == actor ? null : owner, FinishSniperExchange = true,
                    Parent = State.PendingTurnBuff != null || State.PendingReload != null || State.PendingDecision?.TargetResponse != null || State.WdPayment != null
                        ? _activeActivation : null });
        else
        {
            MoveMemoryCardToField(new(owner, index), p);
            PlaceInExchange(p, CardCatalog.Sniper.Id);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
    private void ShiftSniperIndices(int memoryOwner, int removed)
    {
        if (State.PendingSniper is not { } pending) return;
        for (var i = 0; i < pending.MemoryIndices.Count; i++)
            if (pending.MemoryOwner(i) == memoryOwner && pending.MemoryIndices[i] > removed) pending.MemoryIndices[i]--;
    }
}
