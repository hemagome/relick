using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    // Cosmetic choices of the public profile. A player without a row shows the defaults.
    [Table(Accessor = "Profile")]
    public partial struct Profile
    {
        [PrimaryKey] public Identity Player;
        public string Avatar;
        public string Title;
        public string Frame;
        public string Playmat;
    }

    // Finished duels per player and Relikario base form. Rows exist only after a first finished duel.
    [Table(Accessor = "PlayerRecord")]
    public partial struct PlayerRecord
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Player;
        public string Relikario;
        public uint Wins;
        public uint Losses;
        public uint Draws;
    }

    /// <summary>What another player may see of a profile: the cosmetics and the totals, never the identity.</summary>
    [SpacetimeDB.Type]
    public partial struct ProfileInfo
    {
        public byte Seat; // Seat in the caller's room; zero in my_profile.
        public string RoomCode; // Empty in my_profile.
        public string Name;
        public string Avatar;
        public string Title;
        public string Frame;
        public string Playmat;
        public uint Wins;
        public uint Losses;
        public uint Draws;
        public string Favorite; // Empty before a first finished duel.
    }

    private static ProfileInfo ProfileOf(ViewContext ctx, Identity player, string name)
    {
        var profile = ctx.Db.Profile.Player.Find(player);
        var records = ctx.Db.PlayerRecord.Player.Filter(player).Select(r => new RelikarioRecord(r.Relikario, r.Wins, r.Losses, r.Draws)).ToList();
        return new ProfileInfo
        {
            Name = name,
            Avatar = ProfileCatalog.Avatar(profile?.Avatar),
            Title = ProfileCatalog.Title(profile?.Title).Id,
            Frame = ProfileCatalog.Frame(profile?.Frame).Id,
            Playmat = ProfileCatalog.Playmat(profile?.Playmat).Id,
            Wins = (uint)records.Sum(r => r.Wins), Losses = (uint)records.Sum(r => r.Losses), Draws = (uint)records.Sum(r => r.Draws),
            Favorite = ProfileCatalog.Favorite(records) ?? "", RoomCode = ""
        };
    }

    [View(Accessor = "MyProfile", Public = true)]
    public static ProfileInfo? MyProfile(ViewContext ctx) =>
        ctx.Db.Account.Player.Find(ctx.Sender) is Account account ? ProfileOf(ctx, ctx.Sender, account.Name) : null;

    /// <summary>The profiles of both seats of the caller's room, so a rival is visible only while sharing a room.</summary>
    [View(Accessor = "MyRoomProfiles", Public = true, PrimaryKey = "Seat")]
    public static IEnumerable<ProfileInfo> MyRoomProfiles(ViewContext ctx)
    {
        if (ctx.Db.Member.Player.Find(ctx.Sender) is not Member me) return new List<ProfileInfo>();
        return ctx.Db.Member.RoomCode.Filter(me.RoomCode).Select(member =>
        {
            var info = ProfileOf(ctx, member.Player, member.Name);
            info.Seat = member.Seat; info.RoomCode = me.RoomCode;
            return info;
        }).ToList();
    }

    [Reducer]
    public static void SetProfile(ReducerContext ctx, string avatar, string title, string frame, string playmat)
    {
        RequireAccount(ctx);
        if (!ProfileCatalog.IsAvatar(avatar)) throw new Exception("Ese retrato no existe.");
        if (!ProfileCatalog.IsTitle(title)) throw new Exception("Ese título no existe.");
        if (!ProfileCatalog.IsFrame(frame)) throw new Exception("Ese marco no existe.");
        if (!ProfileCatalog.IsPlaymat(playmat)) throw new Exception("Ese tapete no existe.");
        var row = new Profile { Player = ctx.Sender, Avatar = avatar, Title = title, Frame = frame, Playmat = playmat };
        if (ctx.Db.Profile.Player.Find(ctx.Sender) is Profile old)
        {
            if (old.Avatar != avatar || old.Title != title || old.Frame != frame || old.Playmat != playmat) ctx.Db.Profile.Player.Update(row);
        }
        else ctx.Db.Profile.Insert(row);
    }

    /// <summary>Counts a duel that has just finished for both seats. Callers run it once, when the duel turns finished.</summary>
    private static void RecordOutcome(ReducerContext ctx, string roomCode, int? winner)
    {
        if (winner == null) return;
        foreach (var member in ctx.Db.Member.RoomCode.Filter(roomCode).ToList())
        {
            var relikario = ProfileCatalog.StatsKey(ctx.Db.LoadoutRelikario.Player.Find(member.Player)?.Relikario);
            var existing = ctx.Db.PlayerRecord.Player.Filter(member.Player).Where(r => r.Relikario == relikario).Cast<PlayerRecord?>().FirstOrDefault();
            var row = existing ?? new PlayerRecord { Player = member.Player, Relikario = relikario };
            if (winner == -1) row.Draws++; else if (winner == member.Seat) row.Wins++; else row.Losses++;
            if (existing == null) ctx.Db.PlayerRecord.Insert(row); else ctx.Db.PlayerRecord.Id.Update(row);
        }
    }
}
