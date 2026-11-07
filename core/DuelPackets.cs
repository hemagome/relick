namespace Relick.Core;

public sealed class DuelBoardPacket
{
    public DuelSnapshot Board { get; set; } = new();
    public ulong[] HandVersions { get; set; } = [0, 0];
    public ulong[] OptionVersions { get; set; } = [0, 0];
}
public sealed class DuelPrivateData
{
    public int MySeat { get; set; }
    public string?[] OwnHand { get; set; } = [];
    public string?[] OpponentHand { get; set; } = [];
}
public static class DuelPackets
{
    public static DuelSnapshot Combine(DuelBoardPacket packet, DuelPrivateData hand, List<DuelOption> options)
    {
        var board = packet.Board;
        board.MySeat = hand.MySeat;
        board.Players[hand.MySeat].Hand = hand.OwnHand;
        var opponent = board.Players[1 - hand.MySeat];
        if (hand.OpponentHand.Length > 0) opponent.Hand = hand.OpponentHand;
        else if (opponent.Hand.Length == 0) opponent.Hand = new string?[opponent.HandCount];
        board.Options = options;
        return board;
    }
}
