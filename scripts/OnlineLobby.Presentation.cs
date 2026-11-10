using Godot;
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
    private string _presentedRoomCode = "", _copiedCode = "", _lobbyNotice = "";
    private double _copyFeedbackSeconds;
    private ScrollContainer _lobbyPage = null!;
    private string _shownLobbyProblem = "";
    private static readonly Color LobbyMint = new("5ed6b3");
    private static readonly Color LobbyBlue = new("9ebdff");

    private void BuildLobby()
    {
        Theme = LobbyTheme();
        var background = new ArenaBackground(); AddChild(background);
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

        var heading = new VBoxContainer(); _lobbyHeading = heading; heading.AddThemeConstantOverride("separation", 4); column.AddChild(heading);
        _lobbyTitle = Text("Sala de duelos", 42); heading.AddChild(_lobbyTitle);
        heading.AddChild(Description("Invita a un rival o entra a su sala para comenzar."));

        var identity = Surface(column, 16); _identityPanel = identity;
        var identityRow = new HBoxContainer(); identityRow.AddThemeConstantOverride("separation", 20); identity.AddChild(identityRow);
        var identityLabel = Caption("TU NOMBRE", BoardArt.Muted);
        identityLabel.CustomMinimumSize = new(100, 0); identityLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        identityRow.AddChild(identityLabel);
        _name = new LineEdit { Name = "PlayerName", PlaceholderText = "¿Cómo te llamas?", Text = "Jugador", MaxLength = 24,
            CustomMinimumSize = new(0, 48), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identityRow.AddChild(_name);

        var cards = new HBoxContainer { Name = "RoomEntryCards" }; cards.AddThemeConstantOverride("separation", 18);
        column.AddChild(cards); _entryCards = cards;
        var create = EntryCard(cards, GameIcon.Flag, LobbyMint, "Crear una sala", "Tu duelo, tu invitación.");
        var explanation = Description("Crea una sala privada y comparte el código con tu rival.");
        create.AddChild(explanation); _entryDescriptions.Add(explanation);
        create.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new(0, 10) });
        _create = AddButton(create, "Crear sala", () => { _lobbyNotice = _localError = ""; _lobby.Create(_name.Text); });
        AccentButton(_create, LobbyMint);

        var join = EntryCard(cards, GameIcon.Exchange, LobbyBlue, "Tengo un código", "Entra a la sala de tu rival.");
        _code = new LineEdit { Name = "RoomCode", PlaceholderText = "Código de 8 caracteres", MaxLength = 8,
            CustomMinimumSize = new(0, 48), Alignment = HorizontalAlignment.Center };
        _code.AddThemeFontSizeOverride("font_size", 20); join.AddChild(_code);
        join.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        _join = AddButton(join, "Unirse", JoinRoom);
        AccentButton(_join, LobbyBlue);
        _code.TextSubmitted += _ => { if (!_join.Disabled) JoinRoom(); };

        _roomPanel = Surface(column, 24); _roomPanel.Visible = false;
        var roomColumn = new VBoxContainer(); roomColumn.AddThemeConstantOverride("separation", 14); _roomPanel.AddChild(roomColumn);
        roomColumn.AddChild(Caption("TU SALA · DUELO PRIVADO", LobbyMint));
        var roomRow = new HBoxContainer(); roomRow.AddThemeConstantOverride("separation", 16); roomColumn.AddChild(roomRow);
        _room = Text("", 34); _room.SizeFlagsHorizontal = SizeFlags.ExpandFill; roomRow.AddChild(_room);
        _copy = AddButton(roomRow, "Copiar código", CopyRoomCode);
        _copy.Name = "CopyRoomCode";
        AccentButton(_copy, LobbyMint);
        _players = Text("", 18); roomColumn.AddChild(_players);
        _preparation = Description(""); roomColumn.AddChild(_preparation);
        _leave = AddButton(roomColumn, "Cerrar sala", _lobby.Leave);
        _leave.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _leave.TooltipText = "Cierra esta sala de preparación para ambos jugadores.";

        var connection = new HBoxContainer(); connection.AddThemeConstantOverride("separation", 10); column.AddChild(connection);
        _status = Description("Conectando…"); _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter; connection.AddChild(_status);
        BuildConnectionNotice();
        Resized += AdjustLobbyLayout; AdjustLobbyLayout();
    }

    private void UpdateLobbyPresentation(bool inRoom)
    {
        _entryCards.Visible = !inRoom;
        _roomPanel.Visible = inRoom;
    }

    private void JoinRoom() { _lobbyNotice = _localError = ""; _lobby.Join(_code.Text, _name.Text); }

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

    private void PresentLobbyState(RoomInfo? room, bool ready, bool connecting, RoomRequest pending, string error, string message)
    {
        var roomCode = room?.Code ?? "";
        if (_presentedRoomCode != roomCode) { _copyFeedbackSeconds = 0; _copiedCode = ""; }
        _presentedRoomCode = roomCode;
        var failed = _localError.Length > 0 ? _localError : ready ? error : "";
        _status.Text = failed.Length > 0 ? failed switch
        {
            "No se encontró la sala." => "No encontramos esa sala. Revisa el código con tu rival y vuelve a intentarlo.",
            "La sala ya está completa." => "La sala está completa o el duelo ya empezó. Pide a tu rival un código nuevo.",
            _ => failed
        } : connecting ? "Conectando con Relick…" : !ready ? ""
            : pending != RoomRequest.None ? message : _lobbyNotice.Length > 0 && room == null ? _lobbyNotice : message == "Conectado" ? "" : message;
        _status.GetParent<Control>().Visible = _status.Text.Length > 0;
        UpdateLobbyPresentation(room != null);
        _status.Modulate = failed.Length > 0 ? ArenaPresentation.Coral : Colors.White;
        var busy = pending != RoomRequest.None;
        _create.Disabled = _join.Disabled = !ready || busy || room != null;
        _create.Text = pending == RoomRequest.Create ? "Creando…" : "Crear sala";
        _join.Text = pending == RoomRequest.Join ? "Entrando…" : "Unirse";
        _name.Editable = _code.Editable = room == null && !busy;
        _copy.Visible = _leave.Visible = room != null;
        _copy.Disabled = !ready || busy;
        _copy.Text = _copiedCode == roomCode && _copyFeedbackSeconds > 0 ? "Copiado ✓" : "Copiar código";
        _leave.Disabled = !ready || busy;
        _leave.Text = pending == RoomRequest.Leave ? "Cerrando…" : "Cerrar sala";
        PresentConnectionNotice(ready, connecting);
        _room.Text = room == null ? "Sin sala" : $"Sala · {room.Code}";
        _players.Text = room == null ? "" : $"{room.PlayerOne}  vs  {(room.PlayerTwo.Length > 0 ? room.PlayerTwo : "Esperando rival…")}\nTu lugar: jugador {room.MySeat + 1}";
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
        var label = Text(text, 12); label.AddThemeColorOverride("font_color", color); return label;
    }

    private static Label Description(string text)
    {
        var label = Text(text, 15); label.AddThemeColorOverride("font_color", BoardArt.Muted); return label;
    }

    private static PanelContainer Surface(Node parent, int padding)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var style = RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 8);
        style.ContentMarginLeft = style.ContentMarginRight = padding;
        style.ContentMarginTop = style.ContentMarginBottom = padding;
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel); return panel;
    }

    private VBoxContainer EntryCard(Node parent, GameIcon icon, Color accent, string title, string subtitle)
    {
        var panel = Surface(parent, 24);
        panel.CustomMinimumSize = new(0, 264); _entryPanels.Add(panel);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12); panel.AddChild(column);
        var header = new HBoxContainer(); header.AddThemeConstantOverride("separation", 14); column.AddChild(header);
        header.AddChild(new LobbySymbol { Icon = icon, Accent = accent, CustomMinimumSize = new(48, 48) });
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; titles.AddThemeConstantOverride("separation", 2); header.AddChild(titles);
        var label = Text(title, 24); label.AddThemeColorOverride("font_color", BoardArt.Ink); titles.AddChild(label); _entryTitles.Add(label);
        var description = Description(subtitle); titles.AddChild(description); _entryDescriptions.Add(description);
        return column;
    }

    private static void AccentButton(Button button, Color accent) => RelickVisuals.Primary(button, accent);

    private static Theme LobbyTheme() => RelickVisuals.Controls();

}

public partial class LobbySymbol : Control
{
    public GameIcon Icon { get; init; }
    public Color Accent { get; init; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), BoardArt.Tint(Accent, 0.09f), BoardArt.Tint(Accent, 0.25f), 12);
        BoardArt.Icon(this, Icon, Size / 2, 25, Accent);
    }
}
