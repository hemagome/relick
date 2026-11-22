using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    [Table(Accessor = "Unlock")]
    public partial struct Unlock
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Player;
        public string Key;
    }

    [Table(Accessor = "Emblem")]
    public partial struct Emblem
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Player;
        public string Kind;
        public string Title;
        public string Detail;
        public long At;
    }

    [SpacetimeDB.Type]
    public partial struct UnlockInfo
    {
        public string Key;
    }

    [SpacetimeDB.Type]
    public partial struct EmblemInfo
    {
        public ulong Id;
        public string Kind;
        public string Title;
        public string Detail;
        public long At;
    }

    /// <summary>What a rival may see of someone's competitive standing, only while sharing a room.</summary>
    [SpacetimeDB.Type]
    public partial struct SeatStandingInfo
    {
        public byte Seat;
        public string RoomCode;
        public bool Rated;
        public int Value;
        public uint Played;
        public List<EmblemInfo> Emblems;
    }

    private static EmblemInfo InfoOf(Emblem e) => new() { Id = e.Id, Kind = e.Kind, Title = e.Title, Detail = e.Detail, At = e.At };

    [View(Accessor = "MyUnlocks", Public = true, PrimaryKey = "Key")]
    public static IEnumerable<UnlockInfo> MyUnlocks(ViewContext ctx) =>
        ctx.Db.Unlock.Player.Filter(ctx.Sender).Select(u => new UnlockInfo { Key = u.Key }).ToList();

    [View(Accessor = "MyEmblems", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<EmblemInfo> MyEmblems(ViewContext ctx) =>
        ctx.Db.Emblem.Player.Filter(ctx.Sender).Select(InfoOf).ToList();

    [View(Accessor = "MyRoomStandings", Public = true, PrimaryKey = "Seat")]
    public static IEnumerable<SeatStandingInfo> MyRoomStandings(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return new List<SeatStandingInfo>();
        var open = ctx.Db.Season.State.Filter(SeasonOpen).ToList();
        return ctx.Db.Member.RoomCode.Filter(me.RoomCode).ToList().Select(member =>
        {
            var info = new SeatStandingInfo
            {
                Seat = member.Seat, RoomCode = me.RoomCode,
                Emblems = ctx.Db.Emblem.Player.Filter(member.Player).OrderByDescending(e => e.Id).Take(3).Select(InfoOf).ToList()
            };
            if (open.Count > 0)
                foreach (var row in ctx.Db.Rating.Player.Filter(member.Player))
                    if (row.Season == open[0].Id) { info.Rated = true; info.Value = row.Value; info.Played = row.Played; }
            return info;
        }).ToList();
    }

    private static void Grant(ReducerContext ctx, Identity player, Reward reward)
    {
        if (ctx.Db.Account.Player.Find(player) == null || ctx.Db.Unlock.Player.Filter(player).Any(u => u.Key == reward.Key)) return;
        ctx.Db.Unlock.Insert(new Unlock { Player = player, Key = reward.Key });
    }

    private static void AwardEmblem(ReducerContext ctx, Identity player, string kind, string title, string detail)
    {
        if (ctx.Db.Account.Player.Find(player) == null) return;
        ctx.Db.Emblem.Insert(new Emblem { Player = player, Kind = kind, Title = title, Detail = detail, At = Now(ctx) });
    }

    /// <summary>An earned cosmetic is refused until the server granted it, whatever the client shows.</summary>
    private static void RequireUnlocked(ReducerContext ctx, RewardKind kind, string id)
    {
        if (RewardCatalog.Of(kind, id) is not { } reward) return;
        if (!ctx.Db.Unlock.Player.Filter(ctx.Sender).Any(u => u.Key == reward.Key))
            throw new Exception("Todavía no ganas ese cosmético: " + reward.HowToEarn + ".");
    }

    private static void EraseRewards(ReducerContext ctx, Identity player)
    {
        foreach (var row in ctx.Db.Unlock.Player.Filter(player).ToList()) ctx.Db.Unlock.Id.Delete(row.Id);
        foreach (var row in ctx.Db.Emblem.Player.Filter(player).ToList()) ctx.Db.Emblem.Id.Delete(row.Id);
    }
}
