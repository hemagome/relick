using Godot;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private MatchmakingLink _matchLink = null!;
    private HBoxContainer _matchFeedback = null!;
    private Control _quickIdleSpace = null!, _quickExplanation = null!;
    private bool _quickMatchRequested;
    private string _matchConfirmationRoom = "";
    private double _matchConfirmationRemaining;
    internal int PlayedMatchConfirmations { get; private set; }

    private void PresentMatchmakingFeedback(bool searching, bool ready, RoomRequest pending, bool matched = false)
    {
        var state = matched ? MatchmakingLinkState.Matched : pending is RoomRequest.CancelQuickMatch or RoomRequest.CancelRankedMatch ? MatchmakingLinkState.Canceling
            : searching ? MatchmakingLinkState.Searching : pending is RoomRequest.FindQuickMatch or RoomRequest.FindRankedMatch ? MatchmakingLinkState.Starting : MatchmakingLinkState.Idle;
        var visible = state != MatchmakingLinkState.Idle;
        _matchFeedback.Visible = visible; _quickIdleSpace.Visible = !visible;
        _quickExplanation.Visible = !visible && !ArenaViewport.Short(this);
        _matchLink.Present(state, ready);
        _quickMatchStatus.Visible = visible;
        _quickMatchStatus.Text = state switch
        {
            MatchmakingLinkState.Matched => "Rival encontrado",
            MatchmakingLinkState.Starting => "Iniciando búsqueda…",
            MatchmakingLinkState.Canceling => "Cancelando búsqueda…",
            MatchmakingLinkState.Searching => $"Buscando rival…\n{(int)_searchSeconds / 60:00}:{(int)_searchSeconds % 60:00}",
            _ => ""
        };
    }

    // This short presentation never creates a room, predicts a rival or confirms a search.
    private bool HoldForQuickMatch(RoomInfo? room, bool ready, bool searching, RoomRequest pending, double delta)
    {
        if (_arena != null) return false;
        if (room == null)
        {
            if (ready)
            {
                if (searching || pending is RoomRequest.FindQuickMatch or RoomRequest.FindRankedMatch) _quickMatchRequested = true;
                else if (pending == RoomRequest.None) _quickMatchRequested = false;
                _matchConfirmationRoom = ""; _matchConfirmationRemaining = 0;
            }
            return false;
        }
        if (!room.Dealt) return false;
        if (_matchConfirmationRoom != room.Code)
        {
            if (!_quickMatchRequested) return false;
            _quickMatchRequested = false;
            _matchConfirmationRoom = room.Code;
            PlayedMatchConfirmations++;
            _matchConfirmationRemaining = MatchmakingLink.ConfirmationSeconds;
        }
        else if (ready) _matchConfirmationRemaining = Math.Max(0, _matchConfirmationRemaining - delta);
        if (_matchConfirmationRemaining <= 0) return false;
        PresentLobbyState(null, ready, _lobby.Connecting, RoomRequest.None, "", "", matched: true);
        _quickMatch.Text = "Preparando duelo…";
        _quickMatch.Disabled = _create.Disabled = _join.Disabled = _openJoin.Disabled = true;
        _name.Editable = _code.Editable = false;
        return true;
    }
}
