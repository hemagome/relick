namespace Relick.Core;

public enum CardZone { Hand, Equipment, Attached, Exchange, Bonus, Memory, Corrupted, Activation }
public sealed record PileMovement(int Owner, string CardId, CardZone Source, int Index, CardZone Destination);

public sealed partial class GameEngine
{
    public event Action<PileMovement>? CardMovedToPile;

    private void NotifyPileMove(PlayerState p, string id, CardZone source, int index = -1, CardZone destination = CardZone.Memory)
        => CardMovedToPile?.Invoke(new(Array.IndexOf(State.Players, p), id, source, index, destination));

    private void SendHandToMemory(PlayerState p, int index)
    {
        var id = p.Hand[index];
        NotifyPileMove(p, id, CardZone.Hand, index);
        p.RemoveHandCard(index);
        p.Memory.Add(id);
    }
}
