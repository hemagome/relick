using SpacetimeDB;

public static partial class Module
{
    private const long QuickSearchLease = 15_000_000;

    [Table(Accessor = "QuickSearch", Scheduled = nameof(ExpireQuickSearch), ScheduledAt = nameof(QuickSearch.ScheduledAt))]
    public partial struct QuickSearch
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        [Unique] public Identity Player;
        public ConnectionId Connection;
        public string SearchId;
        public string Name;
        public long JoinedAt;
        public long ExpiresAt;
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Type]
    public partial struct QuickSearchInfo
    {
        public string SearchId;
    }

    [View(Accessor = "MyQuickSearch", Public = true)]
    public static QuickSearchInfo? MyQuickSearch(ViewContext ctx) =>
        ctx.Db.QuickSearch.Player.Find(ctx.Sender) is QuickSearch search
            ? new QuickSearchInfo { SearchId = search.SearchId } : null;

    [Reducer]
    public static void FindQuickMatch(ReducerContext ctx, string name, string searchId)
    {
        name = PlayerName(ctx, name);
        if (!Guid.TryParseExact(searchId, "N", out _)) throw new Exception("Identificador de búsqueda inválido.");
        if (ctx.Db.Member.Player.Find(ctx.Sender) != null) return;
        RequireNoRankedSearch(ctx);
        RequireNotCheckedIn(ctx);
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        if (ctx.Db.QuickSearch.Player.Find(ctx.Sender) is QuickSearch own)
        {
            if (own.ExpiresAt > now) return; // One entry per identity, including repeated requests or tabs.
            ctx.Db.QuickSearch.ScheduledId.Delete(own.ScheduledId);
        }
        // The development queue is bounded. Match oldest live entries first.
        var candidates = ctx.Db.QuickSearch.Iter().OrderBy(s => s.JoinedAt).ThenBy(s => s.ScheduledId).ToList();
        foreach (var candidate in candidates)
        {
            if (candidate.ExpiresAt <= now || ctx.Db.Member.Player.Find(candidate.Player) != null)
            { ctx.Db.QuickSearch.ScheduledId.Delete(candidate.ScheduledId); continue; }
            var room = CreateRoomFor(ctx, candidate.Player, candidate.Name);
            MarkQuickRoom(ctx, room.Code);
            CompleteRoom(ctx, room, ctx.Db.Member.Player.Find(candidate.Player)!.Value, ctx.Sender, name);
            ctx.Db.QuickSearch.ScheduledId.Delete(candidate.ScheduledId);
            return;
        }
        if (ctx.Db.QuickSearch.Count >= 500) throw new Exception("La búsqueda está llena. Inténtalo más tarde.");
        ctx.Db.QuickSearch.Insert(new QuickSearch
        {
            Player = ctx.Sender, Connection = ctx.ConnectionId ?? throw new Exception("Se necesita una conexión activa."),
            SearchId = searchId, Name = name, JoinedAt = now, ExpiresAt = now + QuickSearchLease,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(QuickSearchLease))
        });
    }

    [Reducer]
    public static void CancelQuickMatch(ReducerContext ctx, string searchId)
    {
        // A late cancellation must never close a matched room or cancel a newer search.
        if (ctx.Db.QuickSearch.Player.Find(ctx.Sender) is QuickSearch search && search.SearchId == searchId)
            ctx.Db.QuickSearch.ScheduledId.Delete(search.ScheduledId);
    }

    [Reducer]
    public static void KeepQuickMatchAlive(ReducerContext ctx, string searchId)
    {
        if (ctx.Db.QuickSearch.Player.Find(ctx.Sender) is not QuickSearch search || search.SearchId != searchId) return;
        var now = ctx.Timestamp.MicrosecondsSinceUnixEpoch;
        if (search.ExpiresAt <= now)
        { ctx.Db.QuickSearch.ScheduledId.Delete(search.ScheduledId); return; }
        if (search.ExpiresAt - now > QuickSearchLease - 5_000_000) return;
        ctx.Db.QuickSearch.ScheduledId.Delete(search.ScheduledId);
        search.ScheduledId = 0;
        search.Connection = ctx.ConnectionId ?? search.Connection;
        search.ExpiresAt = now + QuickSearchLease;
        search.ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(QuickSearchLease));
        ctx.Db.QuickSearch.Insert(search);
    }

    [Reducer]
    public static void ExpireQuickSearch(ReducerContext ctx, QuickSearch search)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede expirar búsquedas.");
        if (ctx.Db.QuickSearch.ScheduledId.Find(search.ScheduledId) is QuickSearch current &&
            current.ExpiresAt <= ctx.Timestamp.MicrosecondsSinceUnixEpoch)
            ctx.Db.QuickSearch.ScheduledId.Delete(current.ScheduledId);
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void ClientDisconnected(ReducerContext ctx)
    {
        if (ctx.Db.QuickSearch.Player.Find(ctx.Sender) is QuickSearch search && search.Connection == ctx.ConnectionId)
            ctx.Db.QuickSearch.ScheduledId.Delete(search.ScheduledId);
        if (ctx.Db.RankedSearch.Player.Find(ctx.Sender) is RankedSearch ranked && ranked.Connection == ctx.ConnectionId)
            ctx.Db.RankedSearch.Player.Delete(ctx.Sender);
    }

    private static void RequireNoQuickSearch(ReducerContext ctx)
    {
        if (ctx.Db.QuickSearch.Player.Find(ctx.Sender) is QuickSearch search)
        {
            if (search.ExpiresAt > ctx.Timestamp.MicrosecondsSinceUnixEpoch)
                throw new Exception("Cancela la búsqueda de rival antes de entrar a una sala privada.");
            ctx.Db.QuickSearch.ScheduledId.Delete(search.ScheduledId);
        }
    }
}
