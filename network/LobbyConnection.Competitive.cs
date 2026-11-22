using System.Diagnostics;
using Relick.Core;
using Relick.Network.Types;
using SpacetimeDB;

namespace Relick.Network;

// Ranked seasons, tournaments and what they award. Subscribed apart: a server without them leaves both modes planned.
public sealed partial class LobbyConnection
{
    public bool CompetitiveAvailable { get; private set; }
    /// <summary>Changes with anything competitive, so screens rebuild only then.</summary>
    public int CompetitiveRevision { get; private set; }
    /// <summary>Changes only with what the player has earned, so the wardrobe ignores other people's enrolments.</summary>
    public int UnlockRevision { get; private set; }
    public bool CompetitionPending { get; private set; }
    public string CompetitionError { get; private set; } = "";
    private long _rankedRefreshed;
    private string? _cancelRankedId;

    public SeasonInfo? Season => _connection?.Db.CurrentSeason.Iter().SingleOrDefault();
    public StandingInfo? Standing => Season is { } season ? _connection?.Db.MyStandings.Season.Find(season.Id) : null;
    public RatedResult? RatedResult => _connection?.Db.MyRatedResult.Iter().SingleOrDefault();
    public string? RankedSearchId => _connection?.Db.MyRankedSearch.Iter().SingleOrDefault()?.SearchId;
    public bool SearchingRanked => RankedSearchId != null && Room == null;
    public RoomModeInfo? RoomMode => _connection?.Db.MyRoomMode.Iter().SingleOrDefault() is { } mode && mode.RoomCode == Room?.Code ? mode : null;
    public TournamentInfo? Tournament => _connection?.Db.CurrentTournament.Iter().SingleOrDefault();
    public MyTournamentInfo? MyTournament =>
        _connection?.Db.MyTournament.Iter().SingleOrDefault() is { } mine && mine.Tournament == Tournament?.Id ? mine : null;
    public IReadOnlyList<EntrantInfo> Entrants => _connection?.Db.TournamentEntrants.Iter().OrderBy(e => e.Seed).ThenBy(e => e.Id).ToList() ?? [];
    public IReadOnlyList<BracketInfo> Bracket => _connection?.Db.TournamentBracket.Iter().OrderBy(m => m.Round).ThenBy(m => m.Position).ToList() ?? [];
    public IReadOnlyList<EmblemInfo> Emblems => _connection?.Db.MyEmblems.Iter().OrderByDescending(e => e.Id).ToList() ?? [];
    public SeatStandingInfo? RoomStanding(int seat) =>
        _connection?.Db.MyRoomStandings.Seat.Find((byte)seat) is { } info && info.RoomCode == Room?.Code ? info : null;

    /// <summary>Free cosmetics are everyone's; earned ones only once the server granted them.</summary>
    public bool Owns(RewardKind kind, string id) =>
        RewardCatalog.Of(kind, id) is not { } reward || _connection?.Db.MyUnlocks.Key.Find(reward.Key) != null;

    public void FindRankedMatch(string name)
    {
        if (!Ready || !CompetitiveAvailable || Season is not { Open: true } || PendingRoomRequest != RoomRequest.None || Room != null || Searching || !ValidName(name)) return;
        ClearResult();
        RequestRoom(RoomRequest.FindRankedMatch, () => _connection!.Reducers.FindRankedMatch(name.Trim(), Guid.NewGuid().ToString("N")));
    }

    public void CancelRankedMatch()
    {
        if (!Ready || PendingRoomRequest != RoomRequest.None || !SearchingRanked) return;
        _cancelRankedId = RankedSearchId;
        RequestRoom(RoomRequest.CancelRankedMatch, () => _connection!.Reducers.CancelRankedMatch(_cancelRankedId!));
    }

    /// <summary>A screen that opens does not repeat the refusal of an earlier request.</summary>
    public void ForgetRefusals()
    {
        RoomError = CompetitionError = "";
    }

    public void JoinTournament(string name)
    {
        if (ValidName(name)) RequestCompetition(reducers => reducers.JoinTournament(name.Trim()));
    }

    public void WithdrawFromTournament() => RequestCompetition(reducers => reducers.WithdrawFromTournament());

    public void SetTournamentReady(bool ready)
    {
        if (ready) ClearResult();
        RequestCompetition(reducers => reducers.SetTournamentReady(ready));
    }

    private void RequestCompetition(Action<RemoteReducers> send)
    {
        if (!Ready || !CompetitiveAvailable || CompetitionPending) return;
        CompetitionPending = true; CompetitionError = ""; CompetitiveRevision++;
        try { send(_connection!.Reducers); }
        catch (Exception) { CompetitionPending = false; CompetitionError = "No se pudo enviar la solicitud. Comprueba tu conexión."; }
    }

    private void SubscribeCompetitive(DbConnection connection)
    {
        connection.SubscriptionBuilder().OnApplied(_ => { CompetitiveAvailable = true; CompetitiveRevision++; _rankedRefreshed = 0; })
            .OnError((_, _) => CompetitiveAvailable = false)
            .Subscribe(["SELECT * FROM current_season", "SELECT * FROM my_standings", "SELECT * FROM my_rated_result", "SELECT * FROM my_ranked_search",
                "SELECT * FROM my_room_mode", "SELECT * FROM my_unlocks", "SELECT * FROM my_emblems", "SELECT * FROM my_room_standings",
                "SELECT * FROM current_tournament", "SELECT * FROM tournament_entrants", "SELECT * FROM tournament_bracket", "SELECT * FROM my_tournament"]);
    }

    private void RegisterCompetitiveReplies()
    {
        void Changed() => CompetitiveRevision++;
        var db = _connection!.Db;
        db.CurrentSeason.OnInsert += (_, _) => Changed(); db.CurrentSeason.OnDelete += (_, _) => Changed();
        db.MyStandings.OnInsert += (_, _) => Changed(); db.MyStandings.OnUpdate += (_, _, _) => Changed(); db.MyStandings.OnDelete += (_, _) => Changed();
        db.MyRatedResult.OnInsert += (_, _) => Changed(); db.MyRatedResult.OnUpdate += (_, _, _) => Changed(); db.MyRatedResult.OnDelete += (_, _) => Changed();
        void Unlocked() { UnlockRevision++; Changed(); }
        db.MyUnlocks.OnInsert += (_, _) => Unlocked(); db.MyUnlocks.OnDelete += (_, _) => Unlocked();
        db.MyEmblems.OnInsert += (_, _) => Changed(); db.MyEmblems.OnDelete += (_, _) => Changed();
        db.CurrentTournament.OnInsert += (_, _) => Changed(); db.CurrentTournament.OnDelete += (_, _) => Changed();
        db.TournamentEntrants.OnInsert += (_, _) => Changed(); db.TournamentEntrants.OnUpdate += (_, _, _) => Changed(); db.TournamentEntrants.OnDelete += (_, _) => Changed();
        db.TournamentBracket.OnInsert += (_, _) => Changed(); db.TournamentBracket.OnUpdate += (_, _, _) => Changed(); db.TournamentBracket.OnDelete += (_, _) => Changed();
        db.MyTournament.OnInsert += (_, _) => Changed(); db.MyTournament.OnDelete += (_, _) => Changed();

        void Reply(ReducerEventContext context)
        {
            CompetitionPending = false;
            CompetitionError = context.Event.Status is Status.Failed(var reason) ? reason
                : context.Event.Status is Status.Committed ? "" : "El servidor no pudo atender la solicitud.";
            Changed();
        }
        var reducers = _connection.Reducers;
        reducers.OnFindRankedMatch += (context, _, _) => RoomReply(context);
        reducers.OnCancelRankedMatch += (context, searchId) =>
        {
            if (_cancelRankedId == searchId) _cancelRankedId = null;
            RoomReply(context);
        };
        reducers.OnJoinTournament += (context, _) => Reply(context);
        reducers.OnWithdrawFromTournament += context => Reply(context);
        reducers.OnSetTournamentReady += (context, _) => Reply(context);
        _cancelRankedId = null;
        CompetitionPending = false; CompetitionError = ""; Changed();
    }

    // Each renewal also widens the range of acceptable rivals on the server.
    private void TickRankedMatch()
    {
        if (!Ready || !SearchingRanked || PendingRoomRequest != RoomRequest.None || _cancelRankedId != null) return;
        if (_rankedRefreshed != 0 && Stopwatch.GetElapsedTime(_rankedRefreshed).TotalSeconds < 5) return;
        _rankedRefreshed = Stopwatch.GetTimestamp();
        try { _connection!.Reducers.KeepRankedMatchAlive(RankedSearchId!); }
        catch (Exception) { LoseConnection(); }
    }

    private void ForgetCompetitive()
    {
        CompetitiveAvailable = CompetitionPending = false;
        CompetitionError = "";
        CompetitiveRevision++;
    }
}
