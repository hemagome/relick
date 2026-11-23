namespace Relick.Core;

public sealed class PendingReload
{
    public int Actor { get; set; }
    public int Stage { get; set; }
    public List<int>[] SniperIndices { get; set; } = [[], []];
    // Memory seat of each entry above; null means the discarding player's own memory.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<int>[]? SniperOwners { get; set; }
}

public sealed partial class GameEngine
{
    // A whole hand is presented as one face-down packet, without exposing hidden identities.
    public event Action<int, int>? HandDiscarded;
    private void BeginReloadHands(int actor)
    {
        _activeActivation!.CanInterrupt = false;
        var pending = new PendingReload { Actor = actor }; State.PendingReload = pending;
        // Freeze both hands before any discard trigger can draw, equip or complete a canje.
        var sniperOwners = new List<int>[] { [], [] };
        foreach (var owner in new[] { actor, 1 - actor })
        {
            var p = State.Players[owner];
            Log($"{p.Name} descarta su mano de {p.Hand.Count} carta(s) por Cartucho de Re-carga.");
            var discarded = DiscardHandCards(owner);
            pending.SniperIndices[owner].AddRange(discarded.Select(card => card.Index));
            sniperOwners[owner].AddRange(discarded.Select(card => card.Owner));
            if (discarded.Any(card => card.Owner != owner)) pending.SniperOwners = sniperOwners;
        }
        State.Phase = Phase.ReloadResolving;
        ContinueReloadHands();
    }

    private List<MemorySelection> DiscardHandCards(int owner, int keep = -1)
    {
        var player = State.Players[owner];
        var indices = Enumerable.Range(0, player.Hand.Count).Where(index => index != keep).ToArray();
        var snipers = new List<MemorySelection>();
        if (indices.Length == 0) return snipers;
        HandDiscarded?.Invoke(owner, indices.Length);
        // Send in hand order, then remove backwards so ownership, locks and reveals stay with the surviving copy.
        foreach (var index in indices)
        {
            var id = player.Hand[index];
            var landed = SendToMemory(player, id, CardZone.Hand, index, notify: false);
            RecordDiscard(owner, id, landed);
            if (landed != null && CardCatalog.Get(id).EquipOnDiscard) snipers.Add(landed);
        }
        foreach (var index in indices.Reverse())
        {
            ShiftTargetedHandChoice(owner, index);
            player.RemoveHandCard(index);
        }
        return snipers;
    }

    private void ContinueReloadHands()
    {
        if (State.IsOver || State.PendingReload is not { } pending || State.Phase != Phase.ReloadResolving) return;
        var frame = _activeActivation;
        while (frame != null && frame.CardId != CardCatalog.ReloadCartridge.Id) frame = frame.Parent;
        if (frame == null) throw new InvalidOperationException("Falta la activación de Cartucho de Re-carga.");
        _activeActivation = frame;
        while (State.PendingReload == pending && State.Phase == Phase.ReloadResolving)
        {
            if (pending.Stage >= 2 && TryRunDiscardEffects()) return;
            var stage = pending.Stage++;
            var owner = stage is 0 or 2 ? pending.Actor : 1 - pending.Actor;
            if (stage <= 1)
            {
                if (pending.SniperIndices[owner].Count > 0)
                {
                    BeginSniperChoices(owner, pending.SniperIndices[owner].Select((index, entry) =>
                        new MemorySelection(pending.SniperOwners?[owner][entry] ?? owner, index)));
                    return;
                }
            }
            else if (stage <= 3)
            {
                DrawCards(State.Players[owner], CardCatalog.Get(frame.CardId).Draw,
                    new EngineContinuation { Kind = ContinuationKind.ReloadHandsResume, Actor = owner });
                return;
            }
            else
            {
                State.PendingReload = null; State.Phase = frame.ResumePhase; _activeActivation = frame.Parent;
                State.LastCombat = "Cartucho de Re-carga · Ambos jugadores han robado 4 cartas.";
                Log(State.LastCombat);
                CompletePendingPlay();
            }
        }
    }
}
