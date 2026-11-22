using Godot;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private Control _entryCards = null!;
    private Control _roomPanel = null!;
    private Label _lobbyTitle = null!;
    private VBoxContainer _lobbyColumn = null!;
    private Control _lobbyHeading = null!;
    private PanelContainer _identityPanel = null!;
    private readonly List<PanelContainer> _entryPanels = [];
    private readonly List<Label> _entryTitles = [];
    private readonly List<Control> _entryDescriptions = [];
    private readonly List<VBoxContainer> _entryColumns = [];
    private readonly List<LobbySymbol> _entrySymbols = [];
    private string _presentedRoomCode = "", _copiedCode = "", _lobbyNotice = "";
    private double _copyFeedbackSeconds;
    private ScrollContainer _lobbyPage = null!;
    private string _shownLobbyProblem = "";
    private Button _quickMatch = null!;
    private Button _decks = null!;
    private DeckScreen? _deckScreen;
    private Label _quickMatchStatus = null!;
    private bool _presentedSearching;
    private double _searchSeconds;

    private void BuildLobby()
    {
        Theme = LobbyTheme();
        var background = new LobbyBackground(); AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content = new MarginContainer(); AddChild(_content);
        _content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var page = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        _lobbyPage = page;
        _content.AddChild(page);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 18); page.AddChild(column); _lobbyColumn = column;

        var navigation = new HBoxContainer(); column.AddChild(navigation);
        var brand = RelickVisuals.Logo();
        brand.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; brand.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        navigation.AddChild(brand);
        navigation.AddThemeConstantOverride("separation", 16);
        navigation.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        // The same speaker that opens the sound controls beside the duel menu.
        var sound = new IconButton { Name = "LobbyAudioSettings", Symbol = GameIcon.Speaker, CustomMinimumSize = new(52, 48),
            TooltipText = "Sonido", AccessibilityName = "Sonido", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        sound.Pressed += () => GetNode<AudioPreferences>("/root/AudioPreferences").OpenControls();
        navigation.AddChild(sound);
        _account = new MenuButton { Name = "AccountMenu", Text = "Cuenta", Flat = false, Visible = false,
            CustomMinimumSize = new(110, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        navigation.AddChild(_account);
        var accountMenu = _account.GetPopup();
        accountMenu.AddItem("Cerrar sesión", 0); accountMenu.AddItem("Eliminar cuenta…", 1);
        accountMenu.IdPressed += id => { if (id == 0) SignOut(); else ConfirmDeleteAccount(); };
        ArenaViewport.TouchMenu(accountMenu);

        var heading = new VBoxContainer(); _lobbyHeading = heading; heading.AddThemeConstantOverride("separation", 4); column.AddChild(heading);
        _lobbyTitle = Text("Sala de duelos", MenuVisuals.Type.Hero); heading.AddChild(_lobbyTitle);
        _lobbyTitle.AddThemeFontOverride("font", RelickVisuals.Display);

        var identity = Surface(column, 16); _identityPanel = identity;
        var identityRow = new HBoxContainer(); identityRow.AddThemeConstantOverride("separation", 20); identity.AddChild(identityRow);
        _profileButton = new Button { Name = "OpenProfile", CustomMinimumSize = new(56, 56), Visible = false, TooltipText = "Mi perfil",
            AccessibilityName = "Mi perfil", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _profileAvatar = new ProfileAvatar { Name = "ProfileButtonAvatar", MouseFilter = MouseFilterEnum.Ignore };
        _profileButton.AddChild(_profileAvatar); _profileAvatar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        _profileButton.Pressed += OpenProfile;
        identityRow.AddChild(_profileButton);
        var identityLabel = Caption("TU NOMBRE", BoardArt.Muted);
        identityLabel.CustomMinimumSize = new(100, 0); identityLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        identityRow.AddChild(identityLabel);
        _name = new LineEdit { Name = "PlayerName", PlaceholderText = "¿Cómo te llamas?", Text = "Jugador", MaxLength = 24,
            CustomMinimumSize = new(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identityRow.AddChild(_name);
        _decks = AddButton(identityRow, "Entrenamiento", OpenDecks);
        _decks.Name = "OpenDecks"; _decks.ClipText = true; _decks.CustomMinimumSize = new(240, 48);
        _decks.Icon = DeckScreen.Thumbnail(RelikarioCatalog.Yuno.Id); _decks.ExpandIcon = true; _decks.AddThemeConstantOverride("icon_max_width", 40); _decks.AddThemeConstantOverride("h_separation", 10);
        _decks.TooltipText = "Elige el mazo con el que juegas o arma uno nuevo.";
        _name.TextSubmitted += _ => { SaveAccountName(); _name.ReleaseFocus(); };
        _name.FocusExited += SaveAccountName;

        var cards = new HBoxContainer { Name = "RoomEntryCards" }; cards.AddThemeConstantOverride("separation", 18);
        column.AddChild(cards); _entryCards = cards;
        var quick = EntryCard(cards, GameIcon.Sword, RelickVisuals.State.Own, "Partida rápida", "Casual · Sin clasificación");
        // «Buscar rival» and the subtitle already say what this card does; the slot stays for layout toggles.
        _quickExplanation = new Control { Name = "QuickExplanation", MouseFilter = MouseFilterEnum.Ignore }; quick.AddChild(_quickExplanation);
        _matchFeedback = new HBoxContainer { Name = "MatchmakingFeedback", Visible = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _matchFeedback.AddThemeConstantOverride("separation", 10); quick.AddChild(_matchFeedback);
        _matchLink = new MatchmakingLink { Name = "MatchmakingLink", SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 || ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool() };
        _matchFeedback.AddChild(_matchLink);
        _quickMatchStatus = Text("", MenuVisuals.Type.Control); _quickMatchStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _quickMatchStatus.SizeFlagsVertical = SizeFlags.ShrinkCenter; _matchFeedback.AddChild(_quickMatchStatus);
        _quickIdleSpace = new Control { SizeFlagsVertical = SizeFlags.ExpandFill }; quick.AddChild(_quickIdleSpace);
        _quickMatch = AddButton(quick, "Buscar rival", QuickMatch);
        _quickMatch.Name = "QuickMatch"; RelickVisuals.Primary(_quickMatch);

        // The code field and «Crear sala» / «Unirse» explain this card on their own.
        var join = EntryCard(cards, GameIcon.Flag, RelickVisuals.State.Target, "Sala privada", "");
        _code = new LineEdit { Name = "RoomCode", PlaceholderText = "Código de 8 caracteres", MaxLength = 8,
            CustomMinimumSize = new(0, 48), Alignment = HorizontalAlignment.Center };
        _code.AddThemeFontSizeOverride("font_size", MenuVisuals.Type.Subtitle); join.AddChild(_code);
        join.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var privateActions = new HBoxContainer(); privateActions.AddThemeConstantOverride("separation", 10); join.AddChild(privateActions);
        _create = AddButton(privateActions, "Crear sala", () => { _lobbyNotice = _localError = ""; SaveAccountName(); _lobby.Create(_name.Text); });
        _create.CustomMinimumSize = new(0, 48); _create.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _join = AddButton(privateActions, "Unirse", JoinRoom);
        _join.CustomMinimumSize = new(0, 48); _join.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _code.TextSubmitted += _ => { if (!_join.Disabled) JoinRoom(); };
        BuildPlannedModes(column);

        _roomPanel = Surface(column, 24); _roomPanel.Visible = false;
        var roomColumn = new VBoxContainer(); roomColumn.AddThemeConstantOverride("separation", 14); _roomPanel.AddChild(roomColumn);
        roomColumn.AddChild(Caption("TU SALA · DUELO PRIVADO", BoardArt.Muted));
        var roomRow = new HBoxContainer(); roomRow.AddThemeConstantOverride("separation", 16); roomColumn.AddChild(roomRow);
        _room = Text("", MenuVisuals.Type.Hero); _room.SizeFlagsHorizontal = SizeFlags.ExpandFill; roomRow.AddChild(_room);
        _copy = AddButton(roomRow, "Copiar código", CopyRoomCode);
        _copy.Name = "CopyRoomCode";
        RelickVisuals.Primary(_copy);
        _players = Text("", MenuVisuals.Type.Control); roomColumn.AddChild(_players);
        _preparation = Description(""); roomColumn.AddChild(_preparation);
        _leave = AddButton(roomColumn, "Cerrar sala", _lobby.Leave);
        _leave.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _leave.TooltipText = "Cierra esta sala de preparación para ambos jugadores.";

        var connection = new HBoxContainer(); connection.AddThemeConstantOverride("separation", 10); column.AddChild(connection);
        _status = Description("Conectando…"); _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter; connection.AddChild(_status);
        BuildConnectionNotice();
        BuildLoginScreen();
        Resized += AdjustLobbyLayout; AdjustLobbyLayout();
    }

    private void UpdateLobbyPresentation(bool inRoom)
    {
        _entryCards.Visible = _plannedModes.Visible = !inRoom;
        _roomPanel.Visible = inRoom;
    }

    private void OpenDecks()
    {
        if (_deckScreen != null || _arena != null) return;
        _deckScreen = new DeckScreen { Connection = _lobby, Guest = _lobby.Account?.Guest == true };
        _deckScreen.Closed += CloseDecks;
        _content.Visible = false; AddChild(_deckScreen);
    }

    private void CloseDecks()
    {
        if (_deckScreen == null) return;
        RemoveChild(_deckScreen); _deckScreen.QueueFree(); _deckScreen = null;
        _content.Visible = _arena == null && !_loginScreen.Visible;
    }

    private void PresentEquippedDeck(DeckInfo? deck)
    {
        _decks.Text = deck?.Name ?? "Entrenamiento";
        var thumbnail = DeckScreen.Thumbnail(deck?.Relikario ?? RelikarioCatalog.Yuno.Id);
        _decks.Icon = _matchLink.Thumbnail = thumbnail;
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
        _create.Disabled = _join.Disabled = !ready || busy || searching || room != null;
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
        PresentProfileButton(ready && !busy && !searching && room == null);
        PresentEquippedDeck(_lobby.Decks.FirstOrDefault(deck => deck.Active));
        _copy.Visible = _leave.Visible = room != null;
        _copy.Disabled = !ready || busy;
        _copy.Text = _copiedCode == roomCode && _copyFeedbackSeconds > 0 ? "Copiado ✓" : "Copiar código";
        _leave.Disabled = !ready || busy;
        _leave.Text = pending == RoomRequest.Leave ? "Cerrando…" : "Cerrar sala";
        PresentConnectionNotice(ready, connecting);
        // The caption above already says it is your room; the seat number tells the player nothing.
        _room.Text = room == null ? "Sin sala" : room.Code;
        _players.Text = room == null ? "" : $"{room.PlayerOne}  vs  {(room.PlayerTwo.Length > 0 ? room.PlayerTwo : "Esperando rival…")}";
        _preparation.Text = "Comparte el código con tu rival. Entraréis al tablero cuando se una.";
        var problem = failed.Length > 0 ? failed : !ready && !connecting ? "desconectado" : "";
        if (problem != _shownLobbyProblem)
        {
            _shownLobbyProblem = problem;
            if (problem.Length > 0) CallDeferred(nameof(ScrollToLobbyStatus));
        }
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
        panel.CustomMinimumSize = new(0, 264); _entryPanels.Add(panel);
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
