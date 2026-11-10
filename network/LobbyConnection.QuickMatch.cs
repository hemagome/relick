using System.Diagnostics;
using SpacetimeDB;

namespace Relick.Network;

public sealed partial class LobbyConnection
{
    public bool QuickMatchAvailable { get; private set; }
    public string? QuickSearchId => _connection?.Db.MyQuickSearch.Iter().SingleOrDefault()?.SearchId;
    public bool Searching => QuickSearchId != null && Room == null;
    private long _quickSearchRefreshed;
    private string? _cancelQuickSearchId;
    private string? _quickSearchSession;

    public void FindQuickMatch(string name)
    {
        if (!Ready || !QuickMatchAvailable || PendingRoomRequest != RoomRequest.None || Room != null || Searching || !ValidName(name)) return;
        ClearResult();
        RequestRoom(RoomRequest.FindQuickMatch, () => _connection!.Reducers.FindQuickMatch(name.Trim(), Guid.NewGuid().ToString("N")));
    }

    public void CancelQuickMatch()
    {
        if (!Ready || PendingRoomRequest != RoomRequest.None || !Searching) return;
        _cancelQuickSearchId = QuickSearchId;
        _quickSearchSession = _sessionToken;
        RequestRoom(RoomRequest.CancelQuickMatch, () => _connection!.Reducers.CancelQuickMatch(_cancelQuickSearchId!));
    }

    private void RegisterQuickMatchReplies()
    {
        _connection!.Reducers.OnFindQuickMatch += (context, _, _) => RoomReply(context);
        _connection.Reducers.OnCancelQuickMatch += (context, searchId) =>
        {
            if (context.Event.Status is Status.Committed or Status.Failed or Status.OutOfEnergy && _cancelQuickSearchId == searchId)
                _cancelQuickSearchId = null;
            RoomReply(context);
        };
    }

    private void RecoverQuickMatch()
    {
        _quickSearchRefreshed = 0;
        if (_cancelQuickSearchId == null) return;
        if (_quickSearchSession == _sessionToken && QuickSearchId == _cancelQuickSearchId && Room == null)
            RequestRoom(RoomRequest.CancelQuickMatch, () => _connection!.Reducers.CancelQuickMatch(_cancelQuickSearchId));
        else _cancelQuickSearchId = null;
    }

    private void TickQuickMatch()
    {
        if (!Ready || !Searching || PendingRoomRequest != RoomRequest.None || _cancelQuickSearchId != null) return;
        if (_quickSearchRefreshed != 0 && Stopwatch.GetElapsedTime(_quickSearchRefreshed).TotalSeconds < 5) return;
        _quickSearchRefreshed = Stopwatch.GetTimestamp();
        try { _connection!.Reducers.KeepQuickMatchAlive(QuickSearchId!); }
        catch (Exception) { LoseConnection(); }
    }
}
