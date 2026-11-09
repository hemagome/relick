namespace Relick.Core;
public sealed class PendingSniper
{
    public int Actor { get; set; }
    public List<int> MemoryIndices { get; set; } = [];
    public Phase ResumePhase { get; set; }
}
public sealed partial class GameEngine
{
    private void BeginSniperChoices(int actor, IEnumerable<int> indices)
    {
        var cards = indices.ToList(); if (cards.Count == 0 || State.IsOver) return;
        State.PendingSniper = new() { Actor = actor, MemoryIndices = cards, ResumePhase = State.Phase };
        State.Phase = Phase.SniperChoice;
    }
    public CommandResult ResolveSniper(int actor, bool equip)
    {
        var pending = State.PendingSniper;
        if (State.Phase != Phase.SniperChoice || pending?.Actor != actor) return CommandResult.Fail("No te corresponde resolver Mira de Sniper.");
        var index = pending.MemoryIndices[0]; var p = State.Players[actor];
        if (index < 0 || index >= p.Memory.Count || p.Memory[index] != CardCatalog.Sniper.Id) return CommandResult.Fail("La carta descartada no está disponible.");
        pending.MemoryIndices.RemoveAt(0);
        State.Phase = pending.MemoryIndices.Count > 0 ? Phase.SniperChoice : pending.ResumePhase;
        if (pending.MemoryIndices.Count == 0) State.PendingSniper = null;
        if (equip)
            BeginActivation(new EngineActivation(actor, CardCatalog.Sniper.Id, 0, 0, State.Phase)
                { Kind = ActivationKind.Equipment, MemoryIndex = index, FinishSniperExchange = true,
                    Parent = State.PendingTurnBuff != null || State.PendingReload != null ? _activeActivation : null });
        else
        {
            p.Memory.RemoveAt(index); ShiftSniperIndices(actor, index);
            PlaceInExchange(p, CardCatalog.Sniper.Id);
        }
        CompleteActivationIfFinished();
        return CommandResult.Ok();
    }
    private void ShiftSniperIndices(int actor, int removed)
    {
        if (State.PendingSniper is not { } pending || pending.Actor != actor) return;
        for (var i = 0; i < pending.MemoryIndices.Count; i++) if (pending.MemoryIndices[i] > removed) pending.MemoryIndices[i]--;
    }
}
