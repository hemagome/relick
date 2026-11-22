using SpacetimeDB;
using Relick.Core;
using System.Text.Json;

public static partial class Module
{
    private const string QuickMode = "quick", RankedMode = "ranked", TournamentMode = "tournament";
    // What a rated room can end in, besides the winning seat.
    private const int DrawResult = -1, NoResult = -2, BothAbsent = -3;

#if RELICK_SHORT_COMPETITION
    // Dedicated local test build, never the deployed module.
    private const long RatedDecisionLimit = 6_000_000, RatedPreparationLimit = 6_000_000, TournamentCheckIn = 6_000_000;
#else
    private const long RatedDecisionLimit = CompetitiveRules.DecisionLimit, RatedPreparationLimit = CompetitiveRules.PreparationLimit,
        TournamentCheckIn = CompetitiveRules.CheckInLimit;
#endif

    // How a room was made. Its own table, like the loadouts: a room without a row is private.
    [Table(Accessor = "RoomMode")]
    public partial struct RoomMode
    {
        [PrimaryKey] public string RoomCode;
        public string Mode;
        public ulong Reference; // The season or the tournament match.
        public long DealtAt;
        public bool Settled;
    }

    // Ends a rated duel when whoever must act stops acting.
    [Table(Accessor = "RatedWatch", Scheduled = nameof(CheckRatedRoom), ScheduledAt = nameof(RatedWatch.ScheduledAt))]
    public partial struct RatedWatch
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        [Unique] public string RoomCode;
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Type]
    public partial struct RoomModeInfo
    {
        public string RoomCode;
        public string Mode;
        public long DecisionLimit;
    }

    [View(Accessor = "MyRoomMode", Public = true)]
    public static RoomModeInfo? MyRoomMode(ViewContext ctx) =>
        ctx.Db.Member.Player.Find(ctx.Sender) is Member me && ctx.Db.RoomMode.RoomCode.Find(me.RoomCode) is RoomMode mode
            ? new RoomModeInfo { RoomCode = mode.RoomCode, Mode = mode.Mode, DecisionLimit = mode.Mode == QuickMode ? 0 : RatedDecisionLimit }
            : null;

    private static void RequireOwner(ReducerContext ctx)
    {
        if (ctx.Sender != Owner) throw new Exception("Solo el dueño de la base puede administrar las competencias.");
    }

    private static long Now(ReducerContext ctx) => ctx.Timestamp.MicrosecondsSinceUnixEpoch;

    private static ScheduleAt At(ReducerContext ctx, long due) =>
        new ScheduleAt.Time(ctx.Timestamp + new TimeDuration(Math.Max(1, due - Now(ctx))));

    private static void MarkQuickRoom(ReducerContext ctx, string code) =>
        ctx.Db.RoomMode.Insert(new RoomMode { RoomCode = code, Mode = QuickMode, DealtAt = Now(ctx) });

    private static void MarkRatedRoom(ReducerContext ctx, string code, string mode, ulong reference)
    {
        ctx.Db.RoomMode.Insert(new RoomMode { RoomCode = code, Mode = mode, Reference = reference, DealtAt = Now(ctx) });
        Watch(ctx, code, Now(ctx) + RatedPreparationLimit);
    }

    private static void Watch(ReducerContext ctx, string code, long due)
    {
        if (ctx.Db.RatedWatch.RoomCode.Find(code) is RatedWatch old) ctx.Db.RatedWatch.ScheduledId.Delete(old.ScheduledId);
        ctx.Db.RatedWatch.Insert(new RatedWatch { RoomCode = code, ScheduledAt = At(ctx, due) });
    }

    /// <summary>Hands a room's result to its mode, once: later calls find it settled.</summary>
    private static void SettleRoom(ReducerContext ctx, string code, int result, string note = "")
    {
        if (ctx.Db.RoomMode.RoomCode.Find(code) is not RoomMode { Settled: false } mode || mode.Mode == QuickMode) return;
        mode.Settled = true;
        ctx.Db.RoomMode.RoomCode.Update(mode);
        if (ctx.Db.RatedWatch.RoomCode.Find(code) is RatedWatch watch) ctx.Db.RatedWatch.ScheduledId.Delete(watch.ScheduledId);
        if (mode.Mode == RankedMode) ApplyRated(ctx, mode, result, note);
        else if (mode.Mode == TournamentMode) ResolveTournamentRoom(ctx, mode, result);
    }

    /// <summary>Closing a rated room that was dealt is a defeat; the caller loses.</summary>
    private static void ForfeitOnLeave(ReducerContext ctx, Member me)
    {
        if (ctx.Db.CurrentDuel.RoomCode.Find(me.RoomCode) is CurrentDuel { Finished: true }) return;
        SettleRoom(ctx, me.RoomCode, 1 - me.Seat, $"{me.Name} salió antes de empezar.");
    }

    private static void ForgetRoomMode(ReducerContext ctx, string code)
    {
        SettleRoom(ctx, code, NoResult, "La sala venció sin resultado.");
        ctx.Db.RoomMode.RoomCode.Delete(code);
        if (ctx.Db.RatedWatch.RoomCode.Find(code) is RatedWatch watch) ctx.Db.RatedWatch.ScheduledId.Delete(watch.ScheduledId);
    }

    [Reducer]
    public static void CheckRatedRoom(ReducerContext ctx, RatedWatch watch)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede revisar los duelos con puntuación.");
        if (ctx.Db.RatedWatch.ScheduledId.Find(watch.ScheduledId) == null) return;
        var code = watch.RoomCode;
        if (ctx.Db.RoomMode.RoomCode.Find(code) is not RoomMode { Settled: false } mode) return;
        var now = Now(ctx);
        if (ctx.Db.Opening.RoomCode.Find(code) == null)
        {
            var due = mode.DealtAt + RatedPreparationLimit;
            if (now < due) { Watch(ctx, code, due); return; }
            var members = ctx.Db.Member.RoomCode.Filter(code).ToList();
            var absent = members.Where(m => ctx.Db.Preparation.Player.Find(m.Player) == null).ToList();
            // With neither confirmed, the one who did not even choose a skill is the absent one.
            if (absent.Count == 2)
            {
                var idle = absent.Where(m => ctx.Db.InitialSkill.Player.Find(m.Player) == null).ToList();
                if (idle.Count == 1) absent = idle;
            }
            if (absent.Count == 1) SettleRoom(ctx, code, 1 - absent[0].Seat, $"{absent[0].Name} no preparó su mano a tiempo.");
            else SettleRoom(ctx, code, BothAbsent, "Nadie preparó su mano a tiempo.");
            DeleteRoom(ctx, code);
            return;
        }
        var current = EnsureCurrentDuel(ctx, code);
        if (current.Finished) return;
        var clock = ctx.Db.DuelBoard.RoomCode.Find(code) is DuelBoard board ? TurnClock.FromBoard(board.Json) : null;
        var since = clock?.UpdatedAt ?? mode.DealtAt;
        if (now < since + RatedDecisionLimit) { Watch(ctx, code, since + RatedDecisionLimit); return; }
        ForfeitOnTime(ctx, current, clock?.Running);
    }

    private static void ForfeitOnTime(ReducerContext ctx, CurrentDuel current, int? running)
    {
        var random = new DuelRandom(); random.Record(max => ctx.Rng.Next(max));
        var game = GameEngine.FromSnapshot(current.SnapshotJson, random);
        var seat = running ?? game.State.DecisionPlayer;
        var visuals = DuelVisualEvents.Capture(game);
        if (!game.Forfeit(seat, $"{game.State.Players[seat].Name} agotó el tiempo para decidir.").Success) return;
        current.Revision++; current.Finished = true; current.SnapshotJson = game.SaveSnapshot(false);
        ctx.Db.CurrentDuel.RoomCode.Update(current);
        CacheDuel(ctx, current.RoomCode, game, current.Revision, visuals);
        RecordOutcome(ctx, current.RoomCode, game.State.Winner);
        AppendAudit(ctx, current.RoomCode, current.Revision, JsonSerializer.Serialize(new DuelEntry
            { Actor = seat, Move = new() { Kind = DuelMoveKind.Concede }, RandomValues = random.Finish() }, DuelJson.Default.DuelEntry));
        TouchRoom(ctx, current.RoomCode);
    }
}
