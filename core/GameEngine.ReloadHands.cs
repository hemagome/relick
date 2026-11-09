namespace Relick.Core;

public sealed class PendingReload
{
    public int Actor { get; set; }
    public int Stage { get; set; }
    public List<int>[] SniperIndices { get; set; } = [[], []];
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
        foreach (var owner in new[] { actor, 1 - actor })
        {
            var p = State.Players[owner]; var cards = p.Hand.ToArray(); var start = p.Memory.Count;
            for (var i = 0; i < cards.Length; i++)
            {
                if (CardCatalog.Get(cards[i]).EquipOnDiscard) pending.SniperIndices[owner].Add(start + i);
            }
            if (cards.Length > 0) HandDiscarded?.Invoke(owner, cards.Length);
            p.Hand.Clear(); p.HideHandCards(); p.Memory.AddRange(cards);
            Log($"{p.Name} descarta su mano de {cards.Length} carta(s) por Cartucho de Re-carga.");
        }
        State.Phase = Phase.ReloadResolving;
        ContinueReloadHands();
    }

    private void ContinueReloadHands()
    {
        if (State.IsOver || State.PendingReload is not { } pending || State.Phase != Phase.ReloadResolving) return;
        var frame = _activeActivation;
        while (frame != null && !CardCatalog.Get(frame.CardId).ReloadBothHands) frame = frame.Parent;
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
                    BeginSniperChoices(owner, pending.SniperIndices[owner]);
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
            }
        }
    }
}
