using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private Control _entryCards = null!;
    private Control _roomPanel = null!;
    private BoxContainer _playMain = null!;
    private VBoxContainer _stageColumn = null!, _modesColumn = null!;
    private MarginContainer _playMargin = null!;
    private RelikarioHero _hero = null!;
    private Control _versus = null!;
    private SeatCard _searchOwn = null!, _searchRival = null!, _seatOne = null!, _seatTwo = null!;
    private PanelContainer _deckPanel = null!, _quickPanel = null!, _privatePanel = null!;
    private Label _deckName = null!, _deckState = null!;
    private readonly List<PanelContainer> _entryPanels = [];
    private readonly List<Label> _entryTitles = [];
    private readonly List<Control> _entryDescriptions = [];
    private LobbyBackground _background = null!;
    private readonly List<VBoxContainer> _entryColumns = [];
    private readonly List<LobbySymbol> _entrySymbols = [];
    private AcceptDialog _joinDialog = null!;
    private Label _joinError = null!;
    private Button _openJoin = null!;
    private string _presentedRoomCode = "", _copiedCode = "", _lobbyNotice = "";
    private double _copyFeedbackSeconds;
    private ScrollContainer _lobbyPage = null!;
    private string _shownLobbyProblem = "";
    private Button _quickMatch = null!;
    private Button _decks = null!;
    private Label _quickMatchStatus = null!;
    private bool _presentedSearching;
    private double _searchSeconds;

    private void BuildLobby()
    {
        Theme = LobbyTheme();
        var background = new LobbyBackground(); AddChild(background); _background = background;
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content = new MarginContainer(); AddChild(_content);
        _content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildShell();
        BuildJoinDialog();
        BuildConnectionNotice();
        BuildLoginScreen();
        Resized += AdjustLobbyLayout; AdjustLobbyLayout();
    }

    // Play: the Relikario in use on one side, every mode in sight on the other.
    private Control BuildPlayPage()
    {
        var page = new ScrollContainer { Name = "PlayPage", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        _lobbyPage = page;
        _playMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(_playMargin);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 10); _playMargin.AddChild(column);

        _playMain = new HBoxContainer { Name = "RoomEntryCards", SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(_playMain); _entryCards = _playMain;
        _stageColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f };
        _playMain.AddChild(_stageColumn);
        _hero = new RelikarioHero { Name = "RelikarioHero", SizeFlagsVertical = SizeFlags.ExpandFill };
        _stageColumn.AddChild(_hero);
        var versus = new VBoxContainer { Name = "SearchVersus", Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        versus.AddThemeConstantOverride("separation", 10); _stageColumn.AddChild(versus); _versus = versus;
        _searchOwn = new SeatCard(); versus.AddChild(_searchOwn);
        var against = Caption("VS", BoardArt.Muted); against.HorizontalAlignment = HorizontalAlignment.Center; versus.AddChild(against);
        _searchRival = new SeatCard { Accent = RelickVisuals.State.Rival }; versus.AddChild(_searchRival);

        _deckPanel = Surface(_stageColumn, 16);
        var deckRow = new HBoxContainer(); deckRow.AddThemeConstantOverride("separation", 10); _deckPanel.AddChild(deckRow);
        var deckTexts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        deckTexts.AddThemeConstantOverride("separation", 0); deckRow.AddChild(deckTexts);
        _deckState = Caption("MAZO EN USO", BoardArt.Muted); deckTexts.AddChild(_deckState);
        _deckName = ArenaPresentation.Text("", MenuVisuals.Type.Subtitle); _deckName.Name = "EquippedDeck"; _deckName.ClipText = true;
        _deckName.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        deckTexts.AddChild(_deckName);
        _decks = AddButton(deckRow, "Cambiar", OpenDecks);
        _decks.Name = "OpenDecks"; _decks.CustomMinimumSize = new(96, 48); _decks.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _decks.TooltipText = "Elige el mazo con el que juegas o arma uno nuevo.";

        _modesColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _playMain.AddChild(_modesColumn);
        var quick = EntryCard(_modesColumn, GameIcon.Sword, RelickVisuals.State.Own, "Partida rápida", "Casual · Sin clasificación");
        _quickPanel = _entryPanels[^1]; _quickPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        // «Buscar rival» and the subtitle already say what this card does; the slot stays for layout toggles.
        _quickExplanation = new Control { Name = "QuickExplanation", MouseFilter = MouseFilterEnum.Ignore }; quick.AddChild(_quickExplanation);
        _matchFeedback = new HBoxContainer { Name = "MatchmakingFeedback", Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _matchFeedback.AddThemeConstantOverride("separation", 10); quick.AddChild(_matchFeedback);
        _matchLink = new MatchmakingLink { Name = "MatchmakingLink", SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ReducedMotion = InterfacePreferences.ReducedMotion };
        _matchFeedback.AddChild(_matchLink);
        _quickMatchStatus = Text("", MenuVisuals.Type.Control); _quickMatchStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _quickMatchStatus.SizeFlagsVertical = SizeFlags.ShrinkCenter; _matchFeedback.AddChild(_quickMatchStatus);
        _quickIdleSpace = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; quick.AddChild(_quickIdleSpace);
        _quickMatch = AddButton(quick, "Buscar rival", QuickMatch);
        _quickMatch.Name = "QuickMatch"; RelickVisuals.Primary(_quickMatch);

        var join = EntryCard(_modesColumn, GameIcon.Flag, RelickVisuals.State.Target, "Sala privada", "");
        _privatePanel = _entryPanels[^1];
        var privateActions = new HBoxContainer(); privateActions.AddThemeConstantOverride("separation", 10); join.AddChild(privateActions);
        _create = AddButton(privateActions, "Crear sala", () => { _lobbyNotice = _localError = ""; SaveAccountName(); _lobby.Create(_name.Text); });
        _create.CustomMinimumSize = new(0, 48); _create.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _openJoin = AddButton(privateActions, "Tengo un código", OpenJoinDialog);
        _openJoin.Name = "OpenJoin"; _openJoin.CustomMinimumSize = new(0, 48); _openJoin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildPlannedModes(_modesColumn);

        BuildWaitingRoom(column);

        var connection = new HBoxContainer(); connection.AddThemeConstantOverride("separation", 10); column.AddChild(connection);
        _status = Description("Conectando…"); _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter; connection.AddChild(_status);
        page.Resized += AdjustPlayLayout;
        return page;
    }

    private VBoxContainer _waitingColumn = null!;
    private void BuildWaitingRoom(Node parent)
    {
        var room = Surface(parent, 24); room.Visible = false; room.SizeFlagsVertical = SizeFlags.ExpandFill; _roomPanel = room;
        _waitingColumn = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _waitingColumn.AddThemeConstantOverride("separation", 14); room.AddChild(_waitingColumn);
        var caption = Caption("TU SALA · DUELO PRIVADO", BoardArt.Muted); caption.HorizontalAlignment = HorizontalAlignment.Center;
        _waitingColumn.AddChild(caption);
        _room = Text("", MenuVisuals.Type.Hero); _room.HorizontalAlignment = HorizontalAlignment.Center;
        _room.AddThemeFontOverride("font", RelickVisuals.Strong); _waitingColumn.AddChild(_room);
        var seats = new HBoxContainer { Name = "WaitingSeats" }; seats.AddThemeConstantOverride("separation", 10); _waitingColumn.AddChild(seats);
        _seatOne = new SeatCard { Name = "SeatOne" }; seats.AddChild(_seatOne);
        var against = Caption("VS", BoardArt.Muted); against.AutowrapMode = TextServer.AutowrapMode.Off;
        against.SizeFlagsVertical = SizeFlags.ShrinkCenter; seats.AddChild(against);
        _seatTwo = new SeatCard { Name = "SeatTwo", Accent = RelickVisuals.State.Rival }; seats.AddChild(_seatTwo);
        _preparation = Description(""); _preparation.HorizontalAlignment = HorizontalAlignment.Center; _waitingColumn.AddChild(_preparation);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12); _waitingColumn.AddChild(actions);
        _leave = AddButton(actions, "Cerrar sala", _lobby.Leave);
        _leave.TooltipText = "Cierra esta sala de preparación para ambos jugadores.";
        _copy = AddButton(actions, "Copiar código", CopyRoomCode);
        _copy.Name = "CopyRoomCode"; _copy.CustomMinimumSize = new(200, 48);
        RelickVisuals.Primary(_copy);
    }

    private void BuildJoinDialog()
    {
        _joinDialog = new ConfirmationDialog { Name = "JoinDialog", Title = "Tengo un código", OkButtonText = "Unirse",
            CancelButtonText = "Cancelar", DialogHideOnOk = false };
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 12);
        column.AddChild(Description("Escribe el código que te compartió tu rival."));
        _code = new LineEdit { Name = "RoomCode", PlaceholderText = "Código de 8 caracteres", MaxLength = 8,
            CustomMinimumSize = new(0, 56), Alignment = HorizontalAlignment.Center };
        _code.AddThemeFontSizeOverride("font_size", MenuVisuals.Type.Subtitle);
        column.AddChild(_code);
        _joinError = Description(""); _joinError.Name = "JoinError"; _joinError.Visible = false;
        _joinError.AddThemeColorOverride("font_color", ArenaPresentation.Coral);
        column.AddChild(_joinError);
        MenuDialog.Fill(_joinDialog, column, 420);
        AddChild(_joinDialog); _joinDialog.Hide();
        _join = _joinDialog.GetOkButton();
        _joinDialog.Confirmed += () => { if (!_join.Disabled) JoinRoom(); };
        _code.TextSubmitted += _ => { if (!_join.Disabled) JoinRoom(); };
    }

    private void OpenJoinDialog()
    {
        _localError = _lobbyNotice = "";
        _joinError.Visible = false;
        MenuDialog.Show(_joinDialog);
        _code.GrabFocus();
    }

    private void UpdateLobbyPresentation(bool inRoom)
    {
        _entryCards.Visible = _plannedModes.Visible = !inRoom;
        _roomPanel.Visible = inRoom;
    }

    private void PresentEquippedDeck(DeckInfo? deck)
    {
        var relikario = deck?.Relikario ?? RelikarioCatalog.Yuno.Id;
        _deckName.Text = deck?.Name ?? "Mazo de entrenamiento";
        _deckName.TooltipText = _deckName.Text;
        // A chosen home Relikario replaces the deck's on the stage; the search still shows the deck's.
        var home = _lobby.HomeHero;
        _hero.Relikario = home.Length > 0 ? home : relikario;
        if (_page != MenuPage.Customize) _background.Backdrop = _lobby.HomeBackdrop;
        _matchLink.Thumbnail = DeckScreen.Thumbnail(relikario);
    }

    private void JoinRoom() { _lobbyNotice = _localError = ""; SaveAccountName(); _lobby.Join(_code.Text, _name.Text); }

    private void QuickMatch()
    {
        _lobbyNotice = _localError = "";
        if (_lobby.Searching) _lobby.CancelQuickMatch(); else { SaveAccountName(); _lobby.FindQuickMatch(_name.Text); }
        if (_lobby.PendingRoomRequest == RoomRequest.FindQuickMatch) _quickMatchRequested = true;
    }

    private void CopyRoomCode()
    {
        if (_copy.Disabled || _presentedRoomCode.Length == 0) return;
        if (!DisplayServer.HasFeature(DisplayServer.Feature.Clipboard))
        { _localError = "No se pudo copiar. Comparte el código que aparece en tu sala."; return; }
        DisplayServer.ClipboardSet(_presentedRoomCode);
        if (DisplayServer.ClipboardGet() != _presentedRoomCode)
        { _localError = "No se pudo copiar. Comparte el código que aparece en tu sala."; return; }
        _localError = ""; _copiedCode = _presentedRoomCode; _copyFeedbackSeconds = 2;
        _copy.Text = "Copiado ✓";
    }

    private void PresentLobbyState(RoomInfo? room, bool ready, bool connecting, RoomRequest pending, string error, string message, bool searching = false, bool quickMatchAvailable = true, bool matched = false)
    {
        searching &= room == null;
        if (searching && !_presentedSearching) _searchSeconds = 0;
        if (_presentedSearching && !searching && ready && room == null && pending == RoomRequest.None)
            _lobbyNotice = message.Length == 0 ? "" : "La búsqueda terminó. Pulsa Buscar rival para intentarlo de nuevo.";
        _presentedSearching = searching;
        var roomCode = room?.Code ?? "";
        if (_presentedRoomCode != roomCode) { _copyFeedbackSeconds = 0; _copiedCode = ""; }
        _presentedRoomCode = roomCode;
        var failed = _localError.Length > 0 ? _localError : ready ? error : "";
        _status.Text = failed.Length > 0 ? failed switch
        {
            "No se encontró la sala." => "No encontramos esa sala. Revisa el código con tu rival y vuelve a intentarlo.",
            "La sala ya está completa." => "La sala está completa o el duelo ya empezó. Pide a tu rival un código nuevo.",
            _ => failed
        } : connecting ? "Conectando con Relick…" : !ready || searching && pending == RoomRequest.None ? ""
            : pending != RoomRequest.None ? message : _lobbyNotice.Length > 0 && room == null ? _lobbyNotice : message == "Conectado" ? "" : message;
        _status.GetParent<Control>().Visible = _status.Text.Length > 0;
        UpdateLobbyPresentation(room != null);
        _status.Modulate = failed.Length > 0 ? ArenaPresentation.Coral : Colors.White;
        var busy = pending != RoomRequest.None;
        _create.Disabled = _join.Disabled = _openJoin.Disabled = !ready || busy || searching || room != null;
        _quickMatch.Disabled = !ready || !quickMatchAvailable || busy || room != null;
        _quickMatch.TooltipText = quickMatchAvailable ? "" : "La partida rápida no está disponible en este servidor.";
        _quickMatch.Text = pending == RoomRequest.FindQuickMatch ? "Iniciando…" : pending == RoomRequest.CancelQuickMatch ? "Cancelando…" : searching ? "Cancelar búsqueda" : "Buscar rival";
        // Searching is the action; calling it off is not.
        var cancels = searching && !matched || pending == RoomRequest.CancelQuickMatch;
        if (cancels == RelickVisuals.IsPrimary(_quickMatch))
        {
            if (cancels) RelickVisuals.Secondary(_quickMatch); else RelickVisuals.Primary(_quickMatch);
        }
        PresentMatchmakingFeedback(searching, ready, pending, matched);
        _create.Text = pending == RoomRequest.Create ? "Creando…" : "Crear sala";
        _join.Text = pending == RoomRequest.Join ? "Entrando…" : "Unirse";
        _name.Editable = _code.Editable = room == null && !busy && !searching;
        PresentAccountName(_name.Editable);
        _decks.Disabled = !ready || busy || searching || room != null;
        PresentProfileState(ready && !busy && !searching && room == null);
        PresentAccountSettings();
        PresentNavigation(ready, busy, searching || matched, room != null);
        PresentEquippedDeck(_lobby.Decks.FirstOrDefault(deck => deck.Active));
        _copy.Visible = _leave.Visible = room != null;
        _copy.Disabled = !ready || busy;
        _copy.Text = _copiedCode == roomCode && _copyFeedbackSeconds > 0 ? "Copiado ✓" : "Copiar código";
        _leave.Disabled = !ready || busy;
        _leave.Text = pending == RoomRequest.Leave ? "Cerrando…" : "Cerrar sala";
        PresentConnectionNotice(ready, connecting);
        // The caption above already says it is your room; the seat number tells the player nothing.
        _room.Text = room == null ? "Sin sala" : room.Code;
        _preparation.Text = "Comparte el código con tu rival. Entrarán al tablero cuando se una.";
        PresentSeats(room, searching || matched, matched);
        // The room, the search and a request for either all live on the play page.
        if (room != null || searching) ReturnToPlay();
        if (_joinDialog.Visible)
        {
            if (room != null || searching) _joinDialog.Hide();
            _joinError.Text = failed.Length > 0 ? _status.Text : "";
            _joinError.Visible = failed.Length > 0;
            _join.Text = pending == RoomRequest.Join ? "Entrando…" : "Unirse";
        }
        var problem = failed.Length > 0 ? failed : !ready && !connecting ? "desconectado" : "";
        if (problem != _shownLobbyProblem)
        {
            _shownLobbyProblem = problem;
            if (problem.Length > 0) CallDeferred(nameof(ScrollToLobbyStatus));
        }
    }

    private void PresentSeats(RoomInfo? room, bool searching, bool matched)
    {
        _hero.Visible = !searching;
        _versus.Visible = searching;
        var compact = TightLobby;
        var profile = _lobby.Profile;
        var avatar = profile?.Avatar ?? ProfileCatalog.DefaultAvatar;
        var frame = profile?.Frame ?? ProfileCatalog.DefaultFrame;
        if (searching)
        {
            _searchOwn.Present(OwnProfile().Name, _deckName.Text, avatar, frame, compact);
            _searchRival.Present(matched ? "Rival encontrado" : "Buscando rival…", "", null, null, compact);
        }
        if (room == null) return;
        void Seat(SeatCard card, int seat, string name)
        {
            var mine = seat == room.MySeat;
            if (name.Length == 0) { card.Present("Esperando rival…", "", null, null, compact); return; }
            var shown = mine ? profile : _lobby.RoomProfile(seat);
            card.Present(name, ProfileCatalog.Title(shown?.Title).Name, shown?.Avatar ?? ProfileCatalog.DefaultAvatar, shown?.Frame ?? ProfileCatalog.DefaultFrame, compact);
        }
        Seat(_seatOne, 0, room.PlayerOne);
        Seat(_seatTwo, 1, room.PlayerTwo);
    }

    private void ScrollToLobbyStatus()
    {
        if (_shownLobbyProblem.Length > 0 && _status.IsVisibleInTree()) _lobbyPage.EnsureControlVisible(_status);
    }

    private static Label Caption(string text, Color color)
    {
        var label = Text(text, MenuVisuals.Type.Caption); label.AddThemeColorOverride("font_color", color); return label;
    }

    private static Label Description(string text)
    {
        var label = Text(text, MenuVisuals.Type.Body); label.AddThemeColorOverride("font_color", BoardArt.Muted); return label;
    }

    private static PanelContainer Surface(Node parent, int padding)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = RelickVisuals.Frame(MenuVisuals.Tone.Surface, MenuVisuals.Tone.Edge, MenuVisuals.Cut.Panel);
        style.ContentMarginLeft = style.ContentMarginRight = padding;
        style.ContentMarginTop = style.ContentMarginBottom = padding;
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel); return panel;
    }

    private VBoxContainer EntryCard(Node parent, GameIcon icon, Color accent, string title, string subtitle)
    {
        var panel = Surface(parent, 24);
        _entryPanels.Add(panel);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12); panel.AddChild(column); _entryColumns.Add(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 14); column.AddChild(header);
        var symbol = new LobbySymbol { Icon = icon, Accent = accent, CustomMinimumSize = new(48, 48) };
        header.AddChild(symbol); _entrySymbols.Add(symbol);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 2); header.AddChild(titles);
        var label = Text(title, MenuVisuals.Type.Subtitle); label.AddThemeColorOverride("font_color", BoardArt.Ink); titles.AddChild(label); _entryTitles.Add(label);
        titles.Alignment = BoxContainer.AlignmentMode.Center;
        if (subtitle.Length > 0) { var description = Description(subtitle); titles.AddChild(description); _entryDescriptions.Add(description); }
        return column;
    }

    private static Theme LobbyTheme() => RelickVisuals.Controls();

}

public partial class LobbySymbol : Control
{
    public GameIcon Icon { get; init; }
    public Color Accent { get; init; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), BoardArt.Tint(Accent, 0.09f), BoardArt.Tint(Accent, 0.25f), MenuVisuals.Cut.Control);
        BoardArt.Icon(this, Icon, Size / 2, Math.Min(25, Size.Y * 0.6f), Accent);
    }
}
