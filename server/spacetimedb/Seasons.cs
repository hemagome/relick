using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    private const byte SeasonOpen = 0, SeasonClosed = 1;
    private const long Day = 86_400_000_000;

    [Table(Accessor = "Season")]
    public partial struct Season
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public byte State;
        public string Name;
        public long StartsAt;
        public long EndsAt;
    }

    [Table(Accessor = "SeasonTimer", Scheduled = nameof(EndSeason), ScheduledAt = nameof(SeasonTimer.ScheduledAt))]
    public partial struct SeasonTimer
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        [Unique] public ulong Season;
        public ScheduleAt ScheduledAt;
    }

    [Table(Accessor = "Rating")]
    public partial struct Rating
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity Player;
        [SpacetimeDB.Index.BTree] public ulong Season;
        public int Value;
        public int Peak;
        public uint Played;
        public uint Wins;
        public uint Losses;
        public uint Draws;
        public uint Place; // Written when the season closes.
    }

    // The player's last rated duel, so the result screen can show what it moved.
    [Table(Accessor = "RatedResult")]
    public partial struct RatedResult
    {
        [PrimaryKey] public Identity Player;
        public string RoomCode;
        public int Before;
        public int After;
        public uint Played;
        public string Note;
        public long At;
    }

    [Table(Accessor = "RatedPair")]
    public partial struct RatedPair
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public Identity First;
        [SpacetimeDB.Index.BTree] public Identity Second;
        public long At;
    }

    [SpacetimeDB.Type]
    public partial struct SeasonInfo
    {
        public ulong Id;
        public string Name;
        public bool Open;
        public long StartsAt;
        public long EndsAt;
    }

    [SpacetimeDB.Type]
    public partial struct StandingInfo
    {
        public ulong Season;
        public int Value;
        public int Peak;
        public uint Played;
        public uint Wins;
        public uint Losses;
        public uint Draws;
        public uint Place;
    }

    /// <summary>The open season, or else the last one that closed. None: the mode is still only planned.</summary>
    [View(Accessor = "CurrentSeason", Public = true)]
    public static SeasonInfo? CurrentSeason(AnonymousViewContext ctx)
    {
        var seasons = ctx.Db.Season.State.Filter(SeasonOpen).ToList();
        if (seasons.Count == 0) seasons = ctx.Db.Season.State.Filter(SeasonClosed).OrderByDescending(s => s.Id).Take(1).ToList();
        if (seasons.Count == 0) return null;
        var season = seasons[0];
        return new SeasonInfo { Id = season.Id, Name = season.Name, Open = season.State == SeasonOpen, StartsAt = season.StartsAt, EndsAt = season.EndsAt };
    }

    [View(Accessor = "MyStandings", Public = true, PrimaryKey = "Season")]
    public static IEnumerable<StandingInfo> MyStandings(ViewContext ctx) =>
        ctx.Db.Rating.Player.Filter(ctx.Sender).Select(r => new StandingInfo
        {
            Season = r.Season, Value = r.Value, Peak = r.Peak, Played = r.Played, Wins = r.Wins, Losses = r.Losses, Draws = r.Draws, Place = r.Place
        }).ToList();

    [View(Accessor = "MyRatedResult", Public = true, PrimaryKey = "Player")]
    public static RatedResult? MyRatedResult(ViewContext ctx) => ctx.Db.RatedResult.Player.Find(ctx.Sender);

    /// <summary>Opens the ranked mode. Until the owner calls it, the game shows it as planned.</summary>
    [Reducer]
    public static void OpenSeason(ReducerContext ctx, string name, uint days)
    {
        RequireOwner(ctx);
        StartSeason(ctx, name, days);
    }

    [Reducer]
    public static void CloseSeason(ReducerContext ctx)
    {
        RequireOwner(ctx);
        CloseOpenSeason(ctx);
    }

    [Reducer]
    public static void EndSeason(ReducerContext ctx, SeasonTimer timer)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede cerrar temporadas por fecha.");
        if (ctx.Db.Season.Id.Find(timer.Season) is Season { State: SeasonOpen }) CloseOpenSeason(ctx);
    }

    private static void StartSeason(ReducerContext ctx, string name, uint days)
    {
        name = CompetitionName(name);
        if (days is < 1 or > 365) throw new Exception("Una temporada dura de 1 a 365 días.");
        if (ctx.Db.Season.State.Filter(SeasonOpen).Any()) throw new Exception("Ya hay una temporada abierta.");
        var now = Now(ctx);
        var season = ctx.Db.Season.Insert(new Season { State = SeasonOpen, Name = name, StartsAt = now, EndsAt = now + days * Day });
        ctx.Db.SeasonTimer.Insert(new SeasonTimer { Season = season.Id, ScheduledAt = At(ctx, season.EndsAt) });
    }

    private static string CompetitionName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 40 || name.Any(char.IsControl)) throw new Exception("Usa un nombre de 1 a 40 caracteres.");
        return name;
    }

    private static void CloseOpenSeason(ReducerContext ctx)
    {
        var open = ctx.Db.Season.State.Filter(SeasonOpen).ToList();
        if (open.Count == 0) throw new Exception("No hay una temporada abierta.");
        var season = open[0];
        season.State = SeasonClosed;
        season.EndsAt = Math.Min(season.EndsAt, Now(ctx));
        ctx.Db.Season.Id.Update(season);
        if (ctx.Db.SeasonTimer.Season.Find(season.Id) is SeasonTimer timer) ctx.Db.SeasonTimer.ScheduledId.Delete(timer.ScheduledId);
        foreach (var search in ctx.Db.RankedSearch.Iter().ToList()) ctx.Db.RankedSearch.Player.Delete(search.Player);

        var place = 0u;
        foreach (var row in ctx.Db.Rating.Season.Filter(season.Id).Where(r => r.Played >= CompetitiveRules.PlacementDuels)
                     .OrderByDescending(r => r.Value).ThenByDescending(r => r.Wins).ThenBy(r => r.Id).ToList())
        {
            var placed = row; placed.Place = ++place;
            ctx.Db.Rating.Id.Update(placed);
            foreach (var reward in RewardCatalog.ForSeason(row.Value, row.Played)) Grant(ctx, row.Player, reward);
            AwardEmblem(ctx, row.Player, "season", season.Name, $"{CompetitiveRules.LeagueName(CompetitiveRules.LeagueOf(row.Value))} · #{place}");
        }
    }

    private static Rating RatingIn(ReducerContext ctx, Identity player, ulong season)
    {
        var rows = ctx.Db.Rating.Player.Filter(player).ToList();
        foreach (var row in rows) if (row.Season == season) return row;
        var start = rows.Count > 0 ? CompetitiveRules.SoftReset(rows.OrderByDescending(r => r.Season).First().Value) : CompetitiveRules.StartRating;
        return ctx.Db.Rating.Insert(new Rating { Player = player, Season = season, Value = start, Peak = start });
    }

    private static void ApplyRated(ReducerContext ctx, RoomMode mode, int result, string note)
    {
        var members = ctx.Db.Member.RoomCode.Filter(mode.RoomCode).OrderBy(m => m.Seat).ToArray();
        if (members.Length != 2) return;
        var ratings = members.Select(m => RatingIn(ctx, m.Player, mode.Reference)).ToArray();
        var before = ratings.Select(r => r.Value).ToArray();
        var played = result >= DrawResult;
        if (played && ctx.Db.Season.Id.Find(mode.Reference) is not Season { State: SeasonOpen })
        {
            played = false;
            note = "La temporada cerró antes del resultado: tu puntuación no cambia.";
        }
        for (var seat = 0; seat < 2; seat++)
        {
            var row = ratings[seat];
            if (played)
            {
                var score = result == DrawResult ? 0.5 : result == seat ? 1 : 0;
                row.Value = CompetitiveRules.After(before[seat], before[1 - seat], score, (int)row.Played);
                row.Peak = Math.Max(row.Peak, row.Value);
                row.Played++;
                if (result == DrawResult) row.Draws++; else if (result == seat) row.Wins++; else row.Losses++;
                ctx.Db.Rating.Id.Update(row);
            }
            ctx.Db.RatedResult.Player.Delete(members[seat].Player);
            ctx.Db.RatedResult.Insert(new RatedResult
            {
                Player = members[seat].Player, RoomCode = mode.RoomCode, Before = before[seat], After = row.Value,
                Played = row.Played, Note = note, At = Now(ctx)
            });
        }
    }

    private static int PairingsToday(ReducerContext ctx, Identity a, Identity b)
    {
        var since = Now(ctx) - Day;
        return ctx.Db.RatedPair.First.Filter(a).Count(p => p.Second == b && p.At > since) +
            ctx.Db.RatedPair.First.Filter(b).Count(p => p.Second == a && p.At > since);
    }

    private static void RememberPairing(ReducerContext ctx, Identity a, Identity b)
    {
        var since = Now(ctx) - Day;
        foreach (var player in new[] { a, b })
            foreach (var old in ctx.Db.RatedPair.First.Filter(player).Concat(ctx.Db.RatedPair.Second.Filter(player)).Where(p => p.At <= since).ToList())
                ctx.Db.RatedPair.Id.Delete(old.Id);
        ctx.Db.RatedPair.Insert(new RatedPair { First = a, Second = b, At = Now(ctx) });
    }

    private static void EraseRatings(ReducerContext ctx, Identity player)
    {
        foreach (var row in ctx.Db.Rating.Player.Filter(player).ToList()) ctx.Db.Rating.Id.Delete(row.Id);
        foreach (var pair in ctx.Db.RatedPair.First.Filter(player).Concat(ctx.Db.RatedPair.Second.Filter(player)).ToList())
            ctx.Db.RatedPair.Id.Delete(pair.Id);
        ctx.Db.RatedResult.Player.Delete(player);
    }
}
