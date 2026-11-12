namespace Relick.Core;

public sealed class DuelBoardPacket
{
    public DuelSnapshot Board { get; set; } = new();
    public ulong[] HandVersions { get; set; } = [0, 0];
    public ulong[] OptionVersions { get; set; } = [0, 0];
}
public sealed class DuelPrivateData
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoPassCombos { get; set; }
    public int MySeat { get; set; }
    public string?[] OwnHand { get; set; } = [];
    public string?[] OpponentHand { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int?[]? OwnHandOwners { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int?[]? OpponentHandOwners { get; set; }
}
public static class DuelPackets
{
    public static DuelSnapshot Combine(DuelBoardPacket packet, DuelPrivateData hand, List<DuelOption> options)
    {
        var board = packet.Board;
        board.MySeat = hand.MySeat;
        board.AutoPassCombos = hand.AutoPassCombos;
        board.Players[hand.MySeat].Hand = hand.OwnHand;
        // The private hand replaces the whole slot array, so its owners must replace it too:
        // keeping the public array would assign owners to the wrong cards (or to hidden ones).
        board.Players[hand.MySeat].HandOwners = hand.OwnHandOwners;
        var opponent = board.Players[1 - hand.MySeat];
        if (hand.OpponentHand.Length > 0)
        {
            opponent.Hand = hand.OpponentHand;
            opponent.HandOwners = hand.OpponentHandOwners;
        }
        else if (opponent.Hand.Length == 0) opponent.Hand = new string?[opponent.HandCount];
        board.Options = options;
        return board;
    }
}
