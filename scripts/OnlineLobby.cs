using Godot;
using Relick.Network;
using System.Security.Cryptography;
using System.Text;

namespace Relick;

public partial class OnlineLobby : Control
{
    private readonly LobbyConnection _lobby = new();
    private Label _status = null!;
    private Label _room = null!;
    private LineEdit _name = null!;
    private LineEdit _code = null!;
    private Button _create = null!;
    private Button _join = null!;
    private Button _leave = null!;
    private Button _copy = null!;
    private Label _preparation = null!;
    private string _tokenPath = "";
    private string _server = "";
    private string _database = "";
    private string _localError = "";
    private MarginContainer _content = null!;
    private OnlineArena? _arena;
    private bool _resultExitRequested;

    public override void _Ready()
    {
        Name = "OnlineLobby";
        ManagedShutdown.Install();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ArenaViewport.Configure(GetWindow());
        GetWindow().SizeChanged += () => ArenaViewport.Configure(GetWindow());
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += QuitWithAudio;
        BuildLobby();
        GetNode<MusicDirector>("/root/MusicDirector").ShowMenu();
#if RELICK_DEV_TOOLS
        if (StartOfflineDevTool()) return;
#endif
        InterfacePreferences.Load();
        _matchLink.ReducedMotion = InterfacePreferences.ReducedMotion;
        var config = new ConfigFile();
        config.Load("res://network/server.cfg");
        _server = OS.GetEnvironment("RELICK_SERVER");
        if (_server.Length == 0) _server = config.GetValue("server", "uri", "http://127.0.0.1:3100").AsString();
        _database = OS.GetEnvironment("RELICK_DATABASE");
        if (_database.Length == 0) _database = config.GetValue("server", "database", "relick-dev").AsString();
        var profile = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--pvp-profile=")) ?? "default";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_server + "\n" + _database + "\n" + profile)));
        _tokenPath = ProjectSettings.GlobalizePath("user://pvp-" + key + ".token");
        // An account's credential is short-lived and renewed from its own session file; only the anonymous one is kept as is.
        _lobby.TokenReceived += token => { if (_auth == null) SavePrivate(_tokenPath, token); };
        StartSession(config, key);
#if RELICK_DEV_TOOLS
        StartOnlineDevTools();
#endif
    }

    private void Connect()
    {
        _localError = "";
        if (ConnectWithAccount()) return;
        try
        {
            var token = System.IO.File.Exists(_tokenPath) ? System.IO.File.ReadAllText(_tokenPath) : null;
            _lobby.Connect(_server, _database, token);
        }
        catch (Exception)
        {
            _lobby.Dispose();
            _localError = "No se pudo conectar. Comprueba tu conexión y pulsa Reconectar.";
        }
    }

    public override void _Process(double delta)
    {
        if (!UpdateAccount()) return;
        _lobby.Tick();
        if (AccountWasDeleted()) return;
        var room = _lobby.Room;
        if (HoldForQuickMatch(room, _lobby.Ready, _lobby.Searching, _lobby.PendingRoomRequest, delta)) return;
        if (_presentedRoomCode.Length > 0 && room == null && _lobby.Ready)
            _lobbyNotice = ClosedRoomNotice(_presentedRoomCode);
        if (_arena == null && room == null && _lobby.LastResult != null && !_resultExitRequested)
            OpenArena(_lobby.LastResultRoom);
        if (_arena != null && _lobby.Ready && (room == null && !_arena.HasFinishedResult && _lobby.LastResult == null ||
            _resultExitRequested && room?.Code != _arena.RoomCode))
        {
            var closedCode = _arena.RoomCode;
            RemoveChild(_arena); _arena.QueueFree(); _arena = null; _content.Visible = true;
            // The lobby keeps the classic cloth; the chosen playmat dresses only the arena.
            RelickVisuals.Playmat = RelickPlaymat.Classic;
            GetNode<MusicDirector>("/root/MusicDirector").ShowMenu();
            _lobby.ClearResult(); _resultExitRequested = false;
            _lobbyNotice = ClosedRoomNotice(closedCode);
        }
        // A room recovered while browsing decks takes the player back to it.
        if (room != null) ReturnToPlay();
        if (_arena == null && room != null && (room.Dealt || _lobby.Duel != null))
        {
            OpenArena(room!.Code);
        }
        _copyFeedbackSeconds = Math.Max(0, _copyFeedbackSeconds - delta);
        if (_lobby.Ready && _lobby.Searching) _searchSeconds += delta;
        PresentLobbyState(room, _lobby.Ready, _lobby.Connecting || _accountStage == AccountStage.Restoring, _lobby.PendingRoomRequest, _lobby.RoomError, _lobby.Message, _lobby.Searching, _lobby.QuickMatchAvailable);
    }

    private void OpenArena(string roomCode)
    {
        _resultExitRequested = false;
        RelickVisuals.Playmat = ProfilePlaymats.Of(_lobby.Profile?.Playmat);
        var arena = new OnlineArena { Connection = _lobby, RoomCode = roomCode };
        arena.ReturnToLobbyRequested += () =>
        {
            _resultExitRequested = true;
            if (_lobby.Room?.Code == roomCode) _lobby.Leave(roomCode);
        };
        _arena = arena; _joinDialog.Hide(); _content.Visible = false; AddChild(arena);
    }

    private bool TightLobby => Size.Y < 720;

    private void AdjustLobbyLayout()
    {
        if (_content == null) return;
        // The rail sits at the edge, where a phone in landscape has its notch.
        if (OS.HasFeature("mobile")) ArenaViewport.ApplyInsets(_content, 0, 0);
        var shortScreen = ArenaViewport.Short(this);
        var tight = TightLobby;
        _matchLink.Compact = tight;
        _quickMatchStatus.AddThemeFontSizeOverride("font_size", tight ? MenuVisuals.Type.Body : MenuVisuals.Type.Control);
        _quickExplanation.Visible = _matchLink.State == MatchmakingLinkState.Idle && !tight;
        foreach (var description in _entryDescriptions) description.Visible = !tight;
        foreach (var title in _entryTitles) title.AddThemeFontSizeOverride("font_size", tight ? MenuVisuals.Type.Control : Size.Y >= 900 ? MenuVisuals.Type.Title : MenuVisuals.Type.Subtitle);
        foreach (var column in _entryColumns) column.AddThemeConstantOverride("separation", tight ? 6 : 12);
        foreach (var symbol in _entrySymbols) symbol.CustomMinimumSize = tight ? new(28, 28) : new(48, 48);
        foreach (var slot in _plannedSlots) slot.Compact = tight;
        _rankedCard.Compact = _tournamentCard.Compact = tight;
        _plannedModes.AddThemeConstantOverride("separation", tight ? 8 : 14);
        foreach (var panel in _entryPanels) Pad(panel, shortScreen ? 10 : tight ? 14 : 22);
        Pad(_deckPanel, tight ? 8 : 16);
        Pad((PanelContainer)_roomPanel, shortScreen ? 10 : 24);
        // Tall enough for the search feedback, so searching never moves the button.
        var tall = Size.Y >= 900;
        _quickMatch.CustomMinimumSize = new(150, tall ? 64 : 48);
        _quickMatch.AddThemeFontSizeOverride("font_size", tall ? MenuVisuals.Type.Subtitle : MenuVisuals.Type.Control);
        _quickPanel.CustomMinimumSize = new(0, shortScreen ? 168 : tight ? 178 : 246);
        _deckName.AddThemeFontSizeOverride("font_size", tight ? MenuVisuals.Type.Body : MenuVisuals.Type.Subtitle);
        _deckState.Visible = !shortScreen;
        _modesColumn.AddThemeConstantOverride("separation", tight ? 8 : 16);
        _stageColumn.AddThemeConstantOverride("separation", tight ? 8 : 16);
        _playMain.AddThemeConstantOverride("separation", tight ? 10 : 32);
        _waitingColumn.AddThemeConstantOverride("separation", shortScreen ? 6 : 24);
        AdjustPlayLayout();
    }

    private void AdjustPlayLayout()
    {
        if (_lobbyPage == null || _playMargin == null) return;
        var area = _lobbyPage.Size;
        var shortScreen = ArenaViewport.Short(this);
        var side = (int)Math.Max(shortScreen ? 10 : 24, (area.X - 1240) / 2);
        var above = (int)Math.Max(shortScreen ? 8 : 20, (area.Y - 800) / 2);
        foreach (var edge in new[] { "left", "right" }) _playMargin.AddThemeConstantOverride("margin_" + edge, side);
        foreach (var edge in new[] { "top", "bottom" }) _playMargin.AddThemeConstantOverride("margin_" + edge, above);
    }

    private static void Pad(PanelContainer panel, int padding)
    {
        if (panel.GetThemeStylebox("panel") is not StyleBoxFlat style) return;
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = padding;
    }

    public override void _ExitTree() { _authCancel?.Cancel(); _lobby.Dispose(); }
    private static Label Text(string text, int size = MenuVisuals.Type.Control)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }
    private static Button AddButton(Node parent, string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(150, 48) };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }
}
