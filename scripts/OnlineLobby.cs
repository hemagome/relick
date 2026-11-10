using Godot;
using Relick.Core;
using Relick.Network;
using System.Security.Cryptography;
using System.Text;

namespace Relick;

public partial class OnlineLobby : Control
{
    private readonly LobbyConnection _lobby = new();
    private Label _status = null!;
    private Label _room = null!;
    private Label _players = null!;
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
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ArenaViewport.Configure(GetWindow());
        GetWindow().SizeChanged += () => ArenaViewport.Configure(GetWindow());
        BuildLobby();
        if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--lobby-preview-dir=")))
        {
            SetProcess(false);
            CallDeferred(nameof(LobbyVisualPreview));
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--shared-arena-test") || OS.GetCmdlineUserArgs().Contains("--pvp-ui-test"))
        {
            SetProcess(false); CallDeferred(nameof(ArenaUiSmokeTest)); return;
        }
        var config = new ConfigFile();
        config.Load("res://network/server.cfg");
        _server = OS.GetEnvironment("RELICK_SERVER");
        if (_server.Length == 0) _server = config.GetValue("server", "uri", "http://127.0.0.1:3100").AsString();
        _database = OS.GetEnvironment("RELICK_DATABASE");
        if (_database.Length == 0) _database = config.GetValue("server", "database", "relick-dev").AsString();
        var profile = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--pvp-profile=")) ?? "default";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_server + "\n" + _database + "\n" + profile)));
        _tokenPath = ProjectSettings.GlobalizePath("user://pvp-" + key + ".token");
        _lobby.TokenReceived += token =>
        {
            try
            {
                // Create with user-only permissions from the start, including on Linux.
                var options = new System.IO.FileStreamOptions { Mode = System.IO.FileMode.Create, Access = System.IO.FileAccess.Write };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite;
                using var writer = new System.IO.StreamWriter(new System.IO.FileStream(_tokenPath, options));
                writer.Write(token);
            }
            catch (Exception) { GD.PushWarning("No se pudo guardar la identidad PvP para la próxima sesión."); }
        };
        Connect();
        if (OS.GetCmdlineUserArgs().Contains("--pvp-preparation-test")) CallDeferred(nameof(PreparationSmokeTest));
        if (OS.GetCmdlineUserArgs().Contains("--pvp-combat-test")) CallDeferred(nameof(CombatSmokeTest));
        if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--pvp-input-peer="))) CallDeferred(nameof(InputPeerSmokeTest));
    }

    private void Connect()
    {
        _localError = "";
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
        _lobby.Tick();
        var room = _lobby.Room;
        if (HoldForQuickMatch(room, _lobby.Ready, _lobby.Searching, _lobby.PendingRoomRequest, delta)) return;
        if (_presentedRoomCode.Length > 0 && room == null && _lobby.Ready)
            _lobbyNotice = "La sala se cerró. Puedes crear otra o entrar con un código nuevo.";
        if (_arena == null && room == null && _lobby.LastResult != null && !_resultExitRequested)
            OpenArena(_lobby.LastResultRoom);
        if (_arena != null && _lobby.Ready && (room == null && !_arena.HasFinishedResult && _lobby.LastResult == null ||
            _resultExitRequested && room?.Code != _arena.RoomCode))
        {
            RemoveChild(_arena); _arena.QueueFree(); _arena = null; _content.Visible = true;
            _lobby.ClearResult(); _resultExitRequested = false;
            _lobbyNotice = "La sala se cerró. Puedes crear otra o entrar con un código nuevo.";
        }
        if (_arena == null && room != null && (room.Dealt || _lobby.Duel != null))
        {
            OpenArena(room!.Code);
        }
        _copyFeedbackSeconds = Math.Max(0, _copyFeedbackSeconds - delta);
        if (_lobby.Ready && _lobby.Searching) _searchSeconds += delta;
        PresentLobbyState(room, _lobby.Ready, _lobby.Connecting, _lobby.PendingRoomRequest, _lobby.RoomError, _lobby.Message, _lobby.Searching, _lobby.QuickMatchAvailable);
    }

    private void OpenArena(string roomCode)
    {
        _resultExitRequested = false;
        var arena = new OnlineArena { Connection = _lobby, RoomCode = roomCode };
        arena.ReconnectRequested += Connect;
        arena.ReturnToLobbyRequested += () =>
        {
            _resultExitRequested = true;
            if (_lobby.Room?.Code == roomCode) _lobby.Leave(roomCode);
        };
        _arena = arena; _content.Visible = false; AddChild(arena);
    }

    private void AdjustLobbyLayout()
    {
        if (_content == null) return;
        var horizontal = (int)Math.Max(16, (Size.X - 1060) / 2);
        foreach (var side in new[] { "left", "right" }) _content.AddThemeConstantOverride("margin_" + side, horizontal);
        foreach (var side in new[] { "top", "bottom" }) _content.AddThemeConstantOverride("margin_" + side, ArenaViewport.Short(this) ? 12 : 28);
        var shortScreen = ArenaViewport.Short(this);
        _matchLink.Compact = shortScreen;
        _quickMatchStatus.AddThemeFontSizeOverride("font_size", shortScreen ? 14 : 16);
        if (_lobbyHeading != null) _lobbyHeading.Visible = !shortScreen;
        if (_lobbyColumn != null) _lobbyColumn.AddThemeConstantOverride("separation", shortScreen ? 12 : 18);
        foreach (var description in _entryDescriptions) description.Visible = !shortScreen;
        _quickExplanation.Visible = _matchLink.State == MatchmakingLinkState.Idle && !shortScreen;
        foreach (var title in _entryTitles) title.AddThemeFontSizeOverride("font_size", shortScreen ? 20 : 24);
        foreach (var panel in _entryPanels)
        {
            panel.CustomMinimumSize = new(0, shortScreen ? 200 : 264);
            var style = (StyleBoxFlat)panel.GetThemeStylebox("panel");
            style.ContentMarginLeft = style.ContentMarginRight = shortScreen ? 16 : 24;
            style.ContentMarginTop = style.ContentMarginBottom = shortScreen ? 16 : 24;
        }
        if (_identityPanel != null)
        {
            var style = (StyleBoxFlat)_identityPanel.GetThemeStylebox("panel");
            style.ContentMarginTop = style.ContentMarginBottom = shortScreen ? 8 : 16;
        }
    }

    public override void _ExitTree() => _lobby.Dispose();
    private static Label Text(string text, int size = 16)
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
