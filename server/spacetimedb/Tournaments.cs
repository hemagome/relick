using SpacetimeDB;
using Relick.Core;

public static partial class Module
{
    private const byte TournamentSignup = 0, TournamentRunning = 1, TournamentFinished = 2, TournamentCancelled = 3;
    private const byte NoSide = 0, SideA = 1, SideB = 2, NeitherSide = 3;

    // One elimination tournament is live at a time. The owner opens it; without one the mode shows as planned.
    [Table(Accessor = "Tournament")]
    public partial struct Tournament
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public byte State;
        public string Name;
        public uint Capacity;
        public long StartsAt;
        public uint Round;
        public uint Rounds;
        public string Champion;
    }

    [Table(Accessor = "TournamentEntry")]
    public partial struct TournamentEntry
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public ulong Tournament;
        [SpacetimeDB.Index.BTree] public Identity Player;
        public string Name;
        public uint Seed;
        public uint OutRound; // Zero while still in. The champion's is one past the last round.
    }

    [Table(Accessor = "TournamentMatch")]
    public partial struct TournamentMatch
    {
        [PrimaryKey, AutoInc] public ulong Id;
        [SpacetimeDB.Index.BTree] public ulong Tournament;
        public uint Round;
        public uint Position;
        public ulong EntryA; // Zero is an empty place.
        public ulong EntryB;
        public byte Result;
        public bool ReadyA;
        public bool ReadyB;
        public string RoomCode;
        public long Deadline;
        public uint Draws;
    }

    [Table(Accessor = "TournamentTimer", Scheduled = nameof(CheckTournament), ScheduledAt = nameof(TournamentTimer.ScheduledAt))]
    public partial struct TournamentTimer
    {
        [PrimaryKey, AutoInc] public ulong ScheduledId;
        [SpacetimeDB.Index.BTree] public ulong Tournament;
        public ulong Match; // Zero starts the tournament.
        public ScheduleAt ScheduledAt;
    }

    [SpacetimeDB.Type]
    public partial struct TournamentInfo
    {
        public ulong Id;
        public byte State;
        public string Name;
        public uint Capacity;
        public uint Entered;
        public long StartsAt;
        public uint Round;
        public uint Rounds;
        public string Champion;
    }

    [SpacetimeDB.Type]
    public partial struct EntrantInfo
    {
        public ulong Id;
        public string Name;
        public uint Seed;
        public uint OutRound;
    }

    [SpacetimeDB.Type]
    public partial struct BracketInfo
    {
        public ulong Id;
        public uint Round;
        public uint Position;
        public ulong EntryA;
        public ulong EntryB;
        public byte Result;
        public bool Playing;
    }

    [SpacetimeDB.Type]
    public partial struct MyTournamentInfo
    {
        public ulong Tournament;
        public ulong Entry;
        public uint OutRound;
        public ulong Match; // Zero without a duel waiting.
        public string Rival;
        public bool Ready;
        public bool RivalReady;
        public bool Playing;
        public long Deadline;
        public uint Draws;
    }

    private static Tournament? ShownTournament(IEnumerable<Tournament> signup, IEnumerable<Tournament> running,
        IEnumerable<Tournament> finished, IEnumerable<Tournament> cancelled)
    {
        foreach (var live in signup.Concat(running)) return live;
        foreach (var past in finished.Concat(cancelled).OrderByDescending(t => t.Id)) return past;
        return null;
    }

    private static Tournament? Shown(AnonymousViewContext ctx) => ShownTournament(ctx.Db.Tournament.State.Filter(TournamentSignup),
        ctx.Db.Tournament.State.Filter(TournamentRunning), ctx.Db.Tournament.State.Filter(TournamentFinished),
        ctx.Db.Tournament.State.Filter(TournamentCancelled));

    private static Tournament? Shown(ViewContext ctx) => ShownTournament(ctx.Db.Tournament.State.Filter(TournamentSignup),
        ctx.Db.Tournament.State.Filter(TournamentRunning), ctx.Db.Tournament.State.Filter(TournamentFinished),
        ctx.Db.Tournament.State.Filter(TournamentCancelled));

    [View(Accessor = "CurrentTournament", Public = true)]
    public static TournamentInfo? CurrentTournament(AnonymousViewContext ctx) => Shown(ctx) is Tournament t
        ? new TournamentInfo
        {
            Id = t.Id, State = t.State, Name = t.Name, Capacity = t.Capacity, StartsAt = t.StartsAt, Round = t.Round, Rounds = t.Rounds,
            Champion = t.Champion, Entered = (uint)ctx.Db.TournamentEntry.Tournament.Filter(t.Id).Count()
        }
        : null;

    // Entrants' names are public to whoever plays; their identities never leave the server.
    [View(Accessor = "TournamentEntrants", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<EntrantInfo> TournamentEntrants(AnonymousViewContext ctx) => Shown(ctx) is Tournament t
        ? ctx.Db.TournamentEntry.Tournament.Filter(t.Id)
            .Select(e => new EntrantInfo { Id = e.Id, Name = e.Name, Seed = e.Seed, OutRound = e.OutRound }).ToList()
        : new List<EntrantInfo>();

    [View(Accessor = "TournamentBracket", Public = true, PrimaryKey = "Id")]
    public static IEnumerable<BracketInfo> TournamentBracket(AnonymousViewContext ctx) => Shown(ctx) is Tournament t
        ? ctx.Db.TournamentMatch.Tournament.Filter(t.Id).Select(m => new BracketInfo
        {
            Id = m.Id, Round = m.Round, Position = m.Position, EntryA = m.EntryA, EntryB = m.EntryB, Result = m.Result,
            Playing = m.RoomCode.Length > 0
        }).ToList()
        : new List<BracketInfo>();

    [View(Accessor = "MyTournament", Public = true)]
    public static MyTournamentInfo? MyTournament(ViewContext ctx)
    {
        if (Shown(ctx) is not Tournament t) return null;
        foreach (var entry in ctx.Db.TournamentEntry.Player.Filter(ctx.Sender))
        {
            if (entry.Tournament != t.Id) continue;
            var info = new MyTournamentInfo { Tournament = t.Id, Entry = entry.Id, OutRound = entry.OutRound, Rival = "" };
            if (t.State != TournamentRunning) return info;
            foreach (var match in ctx.Db.TournamentMatch.Tournament.Filter(t.Id))
            {
                if (match.Result != NoSide || match.EntryA != entry.Id && match.EntryB != entry.Id) continue;
                var first = match.EntryA == entry.Id;
                info.Match = match.Id; info.Deadline = match.Deadline; info.Draws = match.Draws; info.Playing = match.RoomCode.Length > 0;
                info.Ready = first ? match.ReadyA : match.ReadyB;
                info.RivalReady = first ? match.ReadyB : match.ReadyA;
                info.Rival = ctx.Db.TournamentEntry.Id.Find(first ? match.EntryB : match.EntryA)?.Name ?? "";
            }
            return info;
        }
        return null;
    }

    [Reducer]
    public static void OpenTournament(ReducerContext ctx, string name, uint capacity, uint minutesUntilStart)
    {
        RequireOwner(ctx);
        CreateTournament(ctx, name, capacity, minutesUntilStart);
    }

    [Reducer]
    public static void StartTournament(ReducerContext ctx)
    {
        RequireOwner(ctx);
        BeginSignedUpTournament(ctx);
    }

    [Reducer]
    public static void CancelTournament(ReducerContext ctx)
    {
        RequireOwner(ctx);
        CancelLiveTournament(ctx);
    }

    private static void CreateTournament(ReducerContext ctx, string name, uint capacity, uint minutesUntilStart)
    {
        name = CompetitionName(name);
        if (capacity is < CompetitiveRules.MinEntrants or > CompetitiveRules.MaxEntrants)
            throw new Exception($"Un torneo admite de {CompetitiveRules.MinEntrants} a {CompetitiveRules.MaxEntrants} duelistas.");
        if (minutesUntilStart is < 1 or > 43_200) throw new Exception("El torneo empieza entre 1 minuto y 30 días después.");
        if (ctx.Db.Tournament.State.Filter(TournamentSignup).Any() || ctx.Db.Tournament.State.Filter(TournamentRunning).Any())
            throw new Exception("Ya hay un torneo en marcha.");
        var starts = Now(ctx) + minutesUntilStart * 60_000_000L;
        var tournament = ctx.Db.Tournament.Insert(new Tournament { State = TournamentSignup, Name = name, Capacity = capacity, StartsAt = starts, Champion = "" });
        ctx.Db.TournamentTimer.Insert(new TournamentTimer { Tournament = tournament.Id, ScheduledAt = At(ctx, starts) });
    }

    private static void BeginSignedUpTournament(ReducerContext ctx)
    {
        var waiting = ctx.Db.Tournament.State.Filter(TournamentSignup).ToList();
        if (waiting.Count == 0) throw new Exception("No hay un torneo con inscripción abierta.");
        Begin(ctx, waiting[0]);
    }

    private static void CancelLiveTournament(ReducerContext ctx)
    {
        var live = ctx.Db.Tournament.State.Filter(TournamentSignup).Concat(ctx.Db.Tournament.State.Filter(TournamentRunning)).ToList();
        if (live.Count == 0) throw new Exception("No hay un torneo que cancelar.");
        Close(ctx, live[0], TournamentCancelled);
    }

    private static void Close(ReducerContext ctx, Tournament tournament, byte state)
    {
        tournament.State = state;
        ctx.Db.Tournament.Id.Update(tournament);
        foreach (var timer in ctx.Db.TournamentTimer.Tournament.Filter(tournament.Id).ToList())
            ctx.Db.TournamentTimer.ScheduledId.Delete(timer.ScheduledId);
    }

    [Reducer]
    public static void JoinTournament(ReducerContext ctx, string name)
    {
        name = PlayerName(ctx, name);
        if (RequireAccount(ctx).Guest) throw new Exception(GuestsDoNotCompete);
        var open = ctx.Db.Tournament.State.Filter(TournamentSignup).ToList();
        if (open.Count == 0) throw new Exception("No hay una inscripción abierta.");
        var entries = ctx.Db.TournamentEntry.Tournament.Filter(open[0].Id).ToList();
        if (entries.Any(e => e.Player == ctx.Sender)) return;
        if (entries.Count >= open[0].Capacity) throw new Exception("El torneo ya está lleno.");
        ctx.Db.TournamentEntry.Insert(new TournamentEntry { Tournament = open[0].Id, Player = ctx.Sender, Name = name });
    }

    [Reducer]
    public static void WithdrawFromTournament(ReducerContext ctx)
    {
        foreach (var tournament in ctx.Db.Tournament.State.Filter(TournamentSignup).ToList())
            foreach (var entry in ctx.Db.TournamentEntry.Tournament.Filter(tournament.Id).Where(e => e.Player == ctx.Sender).ToList())
                ctx.Db.TournamentEntry.Id.Delete(entry.Id);
    }

    /// <summary>Says the caller is here for their match, or no longer is. Both here: the room is dealt.</summary>
    [Reducer]
    public static void SetTournamentReady(ReducerContext ctx, bool ready)
    {
        RequireAccount(ctx);
        if (PendingMatch(ctx, ctx.Sender) is not { } pending) throw new Exception("No tienes un duelo de torneo pendiente.");
        var (match, first) = pending;
        if (match.RoomCode.Length > 0) return;
        if (ready)
        {
            if (ctx.Db.Member.Player.Find(ctx.Sender) != null) throw new Exception("Cierra tu sala antes de entrar al torneo.");
            RequireNoQuickSearch(ctx);
            RequireNoRankedSearch(ctx);
        }
        if (first) match.ReadyA = ready; else match.ReadyB = ready;
        if (match.ReadyA && match.ReadyB)
        {
            var a = ctx.Db.TournamentEntry.Id.Find(match.EntryA)!.Value;
            var b = ctx.Db.TournamentEntry.Id.Find(match.EntryB)!.Value;
            var room = CreateRoomFor(ctx, a.Player, a.Name);
            MarkRatedRoom(ctx, room.Code, TournamentMode, match.Id);
            CompleteRoom(ctx, room, ctx.Db.Member.Player.Find(a.Player)!.Value, b.Player, b.Name);
            match.RoomCode = room.Code; match.ReadyA = match.ReadyB = false;
        }
        ctx.Db.TournamentMatch.Id.Update(match);
    }

    private static (TournamentMatch Match, bool First)? PendingMatch(ReducerContext ctx, Identity player)
    {
        foreach (var tournament in ctx.Db.Tournament.State.Filter(TournamentRunning).ToList())
        {
            var entries = ctx.Db.TournamentEntry.Player.Filter(player).Where(e => e.Tournament == tournament.Id).ToList();
            if (entries.Count == 0) continue;
            foreach (var match in ctx.Db.TournamentMatch.Tournament.Filter(tournament.Id).ToList())
            {
                if (match.Result != NoSide) continue;
                if (match.EntryA == entries[0].Id) return (match, true);
                if (match.EntryB == entries[0].Id) return (match, false);
            }
        }
        return null;
    }

    private static void RequireNotCheckedIn(ReducerContext ctx)
    {
        if (PendingMatch(ctx, ctx.Sender) is { } pending && (pending.First ? pending.Match.ReadyA : pending.Match.ReadyB))
            throw new Exception("Estás esperando tu duelo de torneo. Sal de la espera primero.");
    }

    [Reducer]
    public static void CheckTournament(ReducerContext ctx, TournamentTimer timer)
    {
        if (ctx.Sender != ctx.DatabaseIdentity) throw new Exception("Solo el planificador puede avanzar los torneos.");
        if (ctx.Db.Tournament.Id.Find(timer.Tournament) is not Tournament tournament) return;
        if (timer.Match == 0)
        {
            if (tournament.State == TournamentSignup) Begin(ctx, tournament);
            return;
        }
        if (tournament.State != TournamentRunning || ctx.Db.TournamentMatch.Id.Find(timer.Match) is not TournamentMatch match) return;
        // A draw replays the match with a later deadline and leaves this timer stale.
        if (match.Result != NoSide || match.RoomCode.Length > 0 || Now(ctx) < match.Deadline) return;
        Decide(ctx, match, match.ReadyA ? SideA : match.ReadyB ? SideB : NeitherSide);
    }

    private static void Begin(ReducerContext ctx, Tournament tournament)
    {
        var entries = ctx.Db.TournamentEntry.Tournament.Filter(tournament.Id).OrderBy(e => e.Id).ToList();
        if (entries.Count < CompetitiveRules.MinEntrants) { Close(ctx, tournament, TournamentCancelled); return; }
        for (var i = entries.Count - 1; i > 0; i--)
        {
            var j = ctx.Rng.Next(i + 1); (entries[i], entries[j]) = (entries[j], entries[i]);
        }
        for (var i = 0; i < entries.Count; i++)
        {
            var seeded = entries[i]; seeded.Seed = (uint)i + 1;
            ctx.Db.TournamentEntry.Id.Update(seeded);
        }
        tournament.State = TournamentRunning;
        tournament.Round = 1;
        tournament.Rounds = (uint)CompetitiveRules.Rounds(entries.Count);
        ctx.Db.Tournament.Id.Update(tournament);
        CreateRound(ctx, tournament, CompetitiveRules.FirstRound(entries.Count).Select(seed => seed == 0 ? 0 : entries[seed - 1].Id).ToList());
    }

    private static void CreateRound(ReducerContext ctx, Tournament tournament, List<ulong> places)
    {
        var deadline = Now(ctx) + TournamentCheckIn;
        for (var i = 0; i < places.Count; i += 2)
        {
            var (a, b) = (places[i], places[i + 1]);
            var match = ctx.Db.TournamentMatch.Insert(new TournamentMatch
            {
                Tournament = tournament.Id, Round = tournament.Round, Position = (uint)i / 2, EntryA = a, EntryB = b, RoomCode = "", Deadline = deadline,
                Result = a == 0 && b == 0 ? NeitherSide : b == 0 ? SideA : a == 0 ? SideB : NoSide
            });
            if (match.Result == NoSide)
                ctx.Db.TournamentTimer.Insert(new TournamentTimer { Tournament = tournament.Id, Match = match.Id, ScheduledAt = At(ctx, deadline) });
        }
        AdvanceIfRoundIsOver(ctx, tournament.Id);
    }

    private static void Decide(ReducerContext ctx, TournamentMatch match, byte result)
    {
        match.Result = result; match.RoomCode = ""; match.ReadyA = match.ReadyB = false;
        ctx.Db.TournamentMatch.Id.Update(match);
        if (result != SideA) MarkOut(ctx, match.EntryA, match.Round);
        if (result != SideB) MarkOut(ctx, match.EntryB, match.Round);
        AdvanceIfRoundIsOver(ctx, match.Tournament);
    }

    private static void MarkOut(ReducerContext ctx, ulong id, uint round)
    {
        if (ctx.Db.TournamentEntry.Id.Find(id) is not TournamentEntry entry) return;
        entry.OutRound = round;
        ctx.Db.TournamentEntry.Id.Update(entry);
    }

    private static void AdvanceIfRoundIsOver(ReducerContext ctx, ulong id)
    {
        if (ctx.Db.Tournament.Id.Find(id) is not Tournament { State: TournamentRunning } tournament) return;
        var round = ctx.Db.TournamentMatch.Tournament.Filter(id).Where(m => m.Round == tournament.Round).OrderBy(m => m.Position).ToList();
        if (round.Any(m => m.Result == NoSide)) return;
        var winners = round.Select(m => m.Result == SideA ? m.EntryA : m.Result == SideB ? m.EntryB : 0).ToList();
        if (tournament.Round < tournament.Rounds)
        {
            tournament.Round++;
            ctx.Db.Tournament.Id.Update(tournament);
            CreateRound(ctx, tournament, winners);
            return;
        }
        if (ctx.Db.TournamentEntry.Id.Find(winners[0]) is TournamentEntry champion)
        {
            champion.OutRound = tournament.Rounds + 1;
            ctx.Db.TournamentEntry.Id.Update(champion);
            tournament.Champion = champion.Name;
            foreach (var reward in RewardCatalog.ForChampion) Grant(ctx, champion.Player, reward);
            AwardEmblem(ctx, champion.Player, "tournament", tournament.Name, "Campeón");
        }
        Close(ctx, tournament, TournamentFinished);
    }

    private static void ResolveTournamentRoom(ReducerContext ctx, RoomMode mode, int result)
    {
        if (ctx.Db.TournamentMatch.Id.Find(mode.Reference) is not TournamentMatch { Result: NoSide } match ||
            ctx.Db.Tournament.Id.Find(match.Tournament) is not Tournament { State: TournamentRunning }) return;
        // Seat zero is always the first entry of the match.
        if (result == 0) { Decide(ctx, match, SideA); return; }
        if (result == 1) { Decide(ctx, match, SideB); return; }
        if (result == BothAbsent) { Decide(ctx, match, NeitherSide); return; }
        match.Draws++;
        if (match.Draws >= CompetitiveRules.TournamentDraws)
        {
            var seedA = ctx.Db.TournamentEntry.Id.Find(match.EntryA)?.Seed ?? uint.MaxValue;
            var seedB = ctx.Db.TournamentEntry.Id.Find(match.EntryB)?.Seed ?? uint.MaxValue;
            Decide(ctx, match, seedA <= seedB ? SideA : SideB);
            return;
        }
        match.RoomCode = ""; match.ReadyA = match.ReadyB = false; match.Deadline = Now(ctx) + TournamentCheckIn;
        ctx.Db.TournamentMatch.Id.Update(match);
        ctx.Db.TournamentTimer.Insert(new TournamentTimer { Tournament = match.Tournament, Match = match.Id, ScheduledAt = At(ctx, match.Deadline) });
    }

    private static void EraseTournamentEntries(ReducerContext ctx, Identity player)
    {
        foreach (var entry in ctx.Db.TournamentEntry.Player.Filter(player).ToList())
        {
            if (ctx.Db.Tournament.Id.Find(entry.Tournament) is not Tournament tournament) continue;
            if (tournament.State == TournamentSignup) { ctx.Db.TournamentEntry.Id.Delete(entry.Id); continue; }
            var pending = tournament.State == TournamentRunning ? PendingMatch(ctx, player) : null;
            // The bracket keeps its shape, without the person.
            var blank = entry; blank.Player = default; blank.Name = "Cuenta eliminada";
            ctx.Db.TournamentEntry.Id.Update(blank);
            if (tournament.Champion == entry.Name && entry.OutRound > tournament.Rounds)
            {
                tournament.Champion = blank.Name;
                ctx.Db.Tournament.Id.Update(tournament);
            }
            if (pending is { } waiting) Decide(ctx, waiting.Match, waiting.First ? SideB : SideA);
        }
    }
}
