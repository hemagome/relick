using Relick.Network;

namespace Relick;

// Clasificatoria and Torneo: planned slots until the server has a season or a tournament, then their cards and screens.
public partial class OnlineLobby
{
    private ModeCard _rankedCard = null!, _tournamentCard = null!;
    private CompetitionPanel? _competition;
    private string _entryQuickTitle = "";
    /// <summary>What the competitive screens show instead of the server's, for tests and previews.</summary>
    internal CompetitionView? PreviewCompetition { get; set; }

    private void BuildModeCards()
    {
        _rankedCard = new ModeCard { Name = "RankedCard", Symbol = GameIcon.Ranking, Title = "Clasificatoria ELO", Visible = false };
        _rankedCard.Pressed += OpenRanked;
        _tournamentCard = new ModeCard { Name = "TournamentCard", Symbol = GameIcon.Trophy, Title = "Torneo", Visible = false };
        _tournamentCard.Pressed += OpenTournamentPanel;
        _plannedModes.AddChild(_rankedCard); _plannedModes.MoveChild(_rankedCard, 1);
        _plannedModes.AddChild(_tournamentCard);
    }

    private CompetitionView Competition(bool busy) => PreviewCompetition ??
        (_lobby.CompetitiveAvailable ? CompetitionView.From(_lobby, busy) : new CompetitionView());

    private void OpenRanked()
    {
        if (_competition != null) return;
        var panel = new RankedPanel { Name = "RankedPanel" };
        panel.SearchRequested += () =>
        {
            _lobbyNotice = _localError = "";
            SaveAccountName();
            _lobby.FindRankedMatch(_name.Text);
            if (_lobby.PendingRoomRequest == RoomRequest.FindRankedMatch) _quickMatchRequested = true;
        };
        OpenCompetition(panel);
    }

    private void OpenTournamentPanel()
    {
        if (_competition != null) return;
        var panel = new TournamentPanel { Name = "TournamentPanel" };
        panel.JoinRequested += () => { SaveAccountName(); _lobby.JoinTournament(_name.Text); };
        panel.WithdrawRequested += _lobby.WithdrawFromTournament;
        panel.ReadyRequested += _lobby.SetTournamentReady;
        OpenCompetition(panel);
    }

    private void OpenCompetition(CompetitionPanel panel)
    {
        _competition = panel;
        _lobby.ForgetRefusals();
        panel.Closed += CloseCompetition;
        _playPage.Hide();
        _shell.Body.AddChild(panel);
        PresentCompetition(busy: false, inRoom: false, searching: false);
    }

    private void CloseCompetition()
    {
        if (_competition == null) return;
        _shell.Body.RemoveChild(_competition); _competition.QueueFree(); _competition = null;
        _playPage.Visible = _page == MenuPage.Play;
    }

    private void PresentCompetition(bool busy, bool inRoom, bool searching)
    {
        // A room or a search is always followed from the play page.
        if (inRoom || searching) CloseCompetition();
        var view = Competition(busy);
        var season = view.Season != null; var cup = view.Tournament != null;
        _plannedSlots[0].Visible = !season; _rankedCard.Visible = season;
        _plannedSlots[1].Visible = !cup; _tournamentCard.Visible = cup;
        _rankedCard.Disabled = _tournamentCard.Disabled = busy || searching;
        if (season) { var (state, detail, calls) = view.RankedCard(); _rankedCard.Present(state, detail, calls); }
        if (cup) { var (state, detail, calls) = view.TournamentCard(); _tournamentCard.Present(state, detail, calls); }
        if (_competition is RankedPanel ranked) ranked.Present(view);
        else if (_competition is TournamentPanel tournament) tournament.Present(view);
    }

    /// <summary>Why a room closed: for a rated one, what it cost or paid.</summary>
    private string ClosedRoomNotice(string code)
    {
        const string plain = "";
        if (code.Length == 0 || _lobby.RatedResult is not { } result || result.RoomCode != code) return plain;
        var note = result.Note.Length > 0 ? " " + result.Note : "";
        return result.Before == result.After ? "La sala se cerró." + note : $"ELO {result.Before} → {result.After} ({result.After - result.Before:+#;-#}).{note}";
    }
}
