namespace Relick.Core;

public enum CardZone { Hand, Equipment, Attached, Exchange, Bonus, Memory, Corrupted, Activation, Skill, EquipmentCover, OutOfGame, Zahira }
public sealed record PileMovement(int Owner, string CardId, CardZone Source, int Index, CardZone Destination);

public sealed partial class GameEngine
{
    public event Action<PileMovement>? CardMovedToPile;

    private void NotifyPileMove(PlayerState p, string id, CardZone source, int index = -1, CardZone destination = CardZone.Memory)
        => CardMovedToPile?.Invoke(new(Array.IndexOf(State.Players, p), id, source, index, destination));

    private void SendHandToMemory(PlayerState p, int index)
    {
        var id = p.Hand[index];
        SendToMemory(p, id, CardZone.Hand, index);
        p.RemoveHandCard(index);
    }

    private void SendToMemory(PlayerState p, string id, CardZone source, int index)
    {
        var trap = CardCatalog.Get(id).CodeType == CodeType.Trap;
        NotifyPileMove(p, id, source, index, trap ? CardZone.OutOfGame : CardZone.Memory);
        if (!trap) p.Memory.Add(id);
    }

    private void ReturnHandCardToDeck(int actor, int index)
    {
        var p = State.Players[actor];
        var id = p.Hand[index];
        if (CardCatalog.Get(id).CodeType == CodeType.Trap)
        {
            SendHandToMemory(p, index);
            return;
        }
        CardReturnedToDeck?.Invoke(actor, index, id);
        p.RemoveHandCard(index);
        p.Deck.Add(id);
    }
}
