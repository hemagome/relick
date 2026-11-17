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
            if (p.Hand.Count > 0) HandDiscarded?.Invoke(owner, p.Hand.Count);
            Log($"{p.Name} descarta su mano de {p.Hand.Count} carta(s) por Cartucho de Re-carga.");
            for (var i = 0; i < p.Hand.Count; i++)
            {
                // One face-down packet is announced above; borrowed copies still reach their owner's memory.
                var landed = SendToMemory(p, p.Hand[i], CardZone.Hand, i, notify: false);
                if (landed == null || !CardCatalog.Get(p.Hand[i]).EquipOnDiscard) continue;
                pending.SniperIndices[owner].Add(landed.Index); sniperOwners[owner].Add(landed.Owner);
            }
            p.Hand.Clear(); p.HideHandCards(); p.ForeignHandSlots = null;
            p.ClearCorruptionMarks(CardZone.Hand);
        }
        if (Enumerable.Range(0, 2).Any(owner => sniperOwners[owner].Any(seat => seat != owner))) pending.SniperOwners = sniperOwners;
        State.Phase = Phase.ReloadResolving;
        ContinueReloadHands();
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
                State.PendingReload = null; State.Phase = Phase.Actions; _activeActivation = frame.Parent;
                State.LastCombat = "Cartucho de Re-carga · Ambos jugadores han robado 4 cartas.";
                Log(State.LastCombat);
                CompletePendingPlay();
            }
        }
    }
}
