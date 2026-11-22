using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    // Waiting for a rated rival. The client renews it every five seconds and each renewal widens the search.
    [Table(Accessor = "RankedSearch")]
    public partial struct RankedSearch
    {
        [PrimaryKey] public Identity Player;
        public ConnectionId Connection;
        public string SearchId;
        public string Name;
        public ulong Season;
        public int Value;
        public long JoinedAt;
        public long ExpiresAt;
    }

    [SpacetimeDB.Type]
    public partial struct RankedSearchInfo
    {
        public string SearchId;
        public int Value;
    }

    [View(Accessor = "MyRankedSearch", Public = true)]
    public static RankedSearchInfo? MyRankedSearch(ViewContext ctx) =>
        ctx.Db.RankedSearch.Player.Find(ctx.Sender) is RankedSearch search
            ? new RankedSearchInfo { SearchId = search.SearchId, Value = search.Value } : null;

    private const string GuestsDoNotCompete = "Como invitado no puedes competir. Inicia sesión con una cuenta para puntuar.";

    [Reducer]
    public static void FindRankedMatch(ReducerContext ctx, string name, string searchId)
    {
        name = PlayerName(ctx, name);
        if (RequireAccount(ctx).Guest) throw new Exception(GuestsDoNotCompete);
        if (!Guid.TryParseExact(searchId, "N", out _)) throw new Exception("Identificador de búsqueda inválido.");
        if (ctx.Db.Member.Player.Find(ctx.Sender) != null) return;
        var open = ctx.Db.Season.State.Filter(SeasonOpen).ToList();
        if (open.Count == 0) throw new Exception("No hay una temporada abierta.");
        RequireNoQuickSearch(ctx);
        RequireNotCheckedIn(ctx);
        var now = Now(ctx);
        if (ctx.Db.RankedSearch.Player.Find(ctx.Sender) is RankedSearch own)
        {
            if (own.ExpiresAt > now) return;
            ctx.Db.RankedSearch.Player.Delete(ctx.Sender);
        }
        if (ctx.Db.RankedSearch.Count >= 500) throw new Exception("La búsqueda está llena. Inténtalo más tarde.");
        var search = ctx.Db.RankedSearch.Insert(new RankedSearch
        {
            Player = ctx.Sender, Connection = ctx.ConnectionId ?? throw new Exception("Se necesita una conexión activa."),
            SearchId = searchId, Name = name, Season = open[0].Id, Value = RatingIn(ctx, ctx.Sender, open[0].Id).Value,
            JoinedAt = now, ExpiresAt = now + QuickSearchLease
        });
        PairRanked(ctx, search);
    }

    [Reducer]
    public static void KeepRankedMatchAlive(ReducerContext ctx, string searchId)
    {
        if (ctx.Db.RankedSearch.Player.Find(ctx.Sender) is not RankedSearch search || search.SearchId != searchId) return;
        var now = Now(ctx);
        if (search.ExpiresAt <= now) { ctx.Db.RankedSearch.Player.Delete(ctx.Sender); return; }
        search.Connection = ctx.ConnectionId ?? search.Connection;
        search.ExpiresAt = now + QuickSearchLease;
        ctx.Db.RankedSearch.Player.Update(search);
        PairRanked(ctx, search);
    }

    [Reducer]
    public static void CancelRankedMatch(ReducerContext ctx, string searchId)
    {
        if (ctx.Db.RankedSearch.Player.Find(ctx.Sender) is RankedSearch search && search.SearchId == searchId)
            ctx.Db.RankedSearch.Player.Delete(ctx.Sender);
    }

    private static void PairRanked(ReducerContext ctx, RankedSearch mine)
    {
        var now = Now(ctx);
        var others = ctx.Db.RankedSearch.Iter().Where(s => s.Player != mine.Player)
            .OrderBy(s => Math.Abs(s.Value - mine.Value)).ThenBy(s => s.JoinedAt).ToList();
        foreach (var other in others)
        {
            if (other.ExpiresAt <= now || ctx.Db.Member.Player.Find(other.Player) != null)
            { ctx.Db.RankedSearch.Player.Delete(other.Player); continue; }
            if (other.Season != mine.Season) continue;
            var waited = (now - Math.Min(mine.JoinedAt, other.JoinedAt)) / 1_000_000.0;
            if (Math.Abs(other.Value - mine.Value) > CompetitiveRules.Window(waited)) continue;
            if (PairingsToday(ctx, mine.Player, other.Player) >= CompetitiveRules.PairDuelsPerDay) continue;

            var (first, second) = other.JoinedAt <= mine.JoinedAt ? (other, mine) : (mine, other);
            var room = CreateRoomFor(ctx, first.Player, first.Name);
            MarkRatedRoom(ctx, room.Code, RankedMode, mine.Season);
            CompleteRoom(ctx, room, ctx.Db.Member.Player.Find(first.Player)!.Value, second.Player, second.Name);
            RememberPairing(ctx, first.Player, second.Player);
            ctx.Db.RankedSearch.Player.Delete(first.Player);
            ctx.Db.RankedSearch.Player.Delete(second.Player);
            return;
        }
    }

    private static void RequireNoRankedSearch(ReducerContext ctx)
    {
        if (ctx.Db.RankedSearch.Player.Find(ctx.Sender) is not RankedSearch search) return;
        if (search.ExpiresAt > Now(ctx)) throw new Exception("Cancela la búsqueda clasificatoria primero.");
        ctx.Db.RankedSearch.Player.Delete(ctx.Sender);
    }
}
