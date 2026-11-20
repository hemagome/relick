using SpacetimeDB;
using Relick.Core;
using System.Text.Json;

public static partial class Module
{
    [SpacetimeDB.Type]
    public partial struct DuelInfo
    {
        public string RoomCode;
        public ulong Revision;
        public string Json;
    }

    private static DuelOpening InitialDuel(Opening opening, IEnumerable<Member> members, IEnumerable<Card> roomCards, Func<Identity, List<string>?> trapsOf,
        Func<Identity, (string Sleeve, string Dice)?> styleOf, Func<Identity, string?> relikarioOf)
    {
        var players = members.OrderBy(m => m.Seat).ToArray();
        var cards = roomCards.ToList();
        var bonuses = new[] { opening.BonusOne, opening.BonusTwo };
        return new DuelOpening
        {
            Names = players.Select(p => p.Name).ToArray(), StartingSeat = opening.StartingSeat,
            Hands = players.Select(p => cards.Where(c => c.Owner == p.Player && c.InHand).OrderBy(c => c.Position).Select(c => c.Definition).ToList()).ToArray(),
            Decks = players.Select(p => cards.Where(c => c.Owner == p.Player && !c.InHand && c.Id != bonuses[p.Seat]).OrderBy(c => c.Position).Select(c => c.Definition).ToList()).ToArray(),
            Bonuses = bonuses.Select(id => (string?)cards.Single(c => c.Id == id).Definition).ToArray(),
            // Rooms dealt before Trap decks existed have no loadout and keep the training Traps.
            Traps = players.Select(p => trapsOf(p.Player) ?? CardCatalog.TrainingTraps()).ToArray(),
            // Cosmetic only: each seat's sleeve and die, classic when the room was dealt before styles existed.
            Sleeves = players.Select(p => styleOf(p.Player)?.Sleeve ?? CosmeticCatalog.DefaultSleeve).ToArray(),
            Dice = players.Select(p => styleOf(p.Player)?.Dice ?? CosmeticCatalog.DefaultDice).ToArray(),
            Relikarios = players.Select(p => relikarioOf(p.Player) ?? RelikarioCatalog.Yuno.Id).ToArray()
        };
    }

    // Compatibility view. It joins cached packets; it never runs or replays the engine.
    [View(Accessor = "MyDuel", Public = true)]
    public static DuelInfo? MyDuel(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me ||
            ctx.Db.DuelBoard.RoomCode.Find(me.RoomCode) is not DuelBoard board ||
            ctx.Db.DuelHand.Player.Find(ctx.Sender) is not DuelHand hand ||
            ctx.Db.DuelChoices.Player.Find(ctx.Sender) is not DuelChoices choices) return null;
        var packet = JsonSerializer.Deserialize(board.Json, DuelJson.Default.DuelBoardPacket)!;
        var snapshot = DuelPackets.Combine(packet, JsonSerializer.Deserialize(hand.Json, DuelJson.Default.DuelPrivateData)!,
            JsonSerializer.Deserialize(choices.Json, DuelJson.Default.ListDuelOption)!);
        return new DuelInfo { RoomCode = me.RoomCode, Revision = board.Revision,
            Json = JsonSerializer.Serialize(snapshot, DuelJson.Default.DuelSnapshot) };
    }
}
