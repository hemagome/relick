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
    private HBoxContainer _hand = null!;
    private readonly Dictionary<ulong, HandCard> _previews = new();
    private LineEdit _name = null!;
    private LineEdit _code = null!;
    private Button _create = null!;
    private Button _join = null!;
    private Button _leave = null!;
    private Button _copy = null!;
    private Button _confirm = null!;
    private Label _preparation = null!;
    private Label _hint = null!;
    private readonly HashSet<ulong> _returnedCards = [];
    private readonly Dictionary<ulong, CheckBox> _choices = new();
    private string? _roomCode;
    private string _lastHand = "";
    private string _tokenPath = "";
    private string _server = "";
    private string _database = "";
    private string _localError = "";
    private MarginContainer _content = null!;
    private OnlineArena? _arena;
    public event Action? Closed;

    public override void _Ready()
    {
        Name = "OnlineLobby";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect { Color = new Color("0b101c") };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer(); _content = margin;
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 40);
        var page = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        margin.AddChild(page);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12);
        page.AddChild(column);
        Resized += AdjustLobbyLayout;
        AdjustLobbyLayout();
        column.AddChild(Text("RELICK · Sala PvP", 30));
        column.AddChild(Text("Crea una sala o entra con el código de tu rival."));
        _status = Text("Conectando…"); column.AddChild(_status);
        _name = new LineEdit { PlaceholderText = "Tu nombre", Text = "Jugador", MaxLength = 24, CustomMinimumSize = new(0, 48) };
        column.AddChild(_name);
        var actions = new HFlowContainer(); column.AddChild(actions);
        _create = AddButton(actions, "Crear sala", () => _lobby.Create(_name.Text));
        _code = new LineEdit { PlaceholderText = "Código de sala", MaxLength = 8, CustomMinimumSize = new Vector2(160, 48) };
        actions.AddChild(_code);
        _join = AddButton(actions, "Unirse", () => _lobby.Join(_code.Text, _name.Text));
        _room = Text("", 24); column.AddChild(_room);
        _players = Text(""); column.AddChild(_players);
        var roomActions = new HBoxContainer(); column.AddChild(roomActions);
        _copy = AddButton(roomActions, "Copiar código", () => DisplayServer.ClipboardSet(_lobby.Room?.Code ?? ""));
        _leave = AddButton(roomActions, "Cerrar sala", _lobby.Leave);
        _leave.TooltipText = "Cierra esta sala de preparación para ambos jugadores.";
        _preparation = Text(""); column.AddChild(_preparation);
        _confirm = AddButton(column, "Conservar mano y confirmar", () => _lobby.Confirm(_returnedCards));
        column.AddChild(Text("TU MANO", 18));
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 244), VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        _hand = new HBoxContainer(); _hand.AddThemeConstantOverride("separation", 12); scroll.AddChild(_hand);
        _hint = Text(""); column.AddChild(_hint);
        var footer = new HFlowContainer(); column.AddChild(footer);
        AddButton(footer, "Reconectar", Connect);
        AddButton(footer, "Volver a práctica", CloseToPractice);
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
        catch (Exception error) { _localError = "No se pudo conectar: " + error.Message; }
    }

    public override void _Process(double delta)
    {
        _lobby.Tick();
        var room = _lobby.Room;
        if (_arena != null && _lobby.Ready && room == null)
        {
            RemoveChild(_arena); _arena.QueueFree(); _arena = null; _content.Visible = true;
        }
        if (_arena == null && _lobby.Duel != null)
        {
            _arena = new OnlineArena { Connection = _lobby, RoomCode = room!.Code };
            _arena.ExitRequested += CloseToPractice; _arena.ReconnectRequested += Connect;
            _content.Visible = false; AddChild(_arena);
        }
        var preparation = _lobby.Preparation;
        if (_roomCode != room?.Code)
        {
            _roomCode = room?.Code;
            _returnedCards.Clear();
        }
        _status.Text = _localError.Length > 0 ? _localError : _lobby.Message;
        _create.Disabled = _join.Disabled = !_lobby.Ready || room != null;
        _name.Editable = _code.Editable = room == null;
        _copy.Visible = _leave.Visible = room != null;
        _leave.Disabled = !_lobby.Ready;
        _room.Text = room == null ? "Sin sala" : $"Sala · {room.Code}";
        _players.Text = room == null ? "" : $"{room.PlayerOne}  vs  {(room.PlayerTwo.Length > 0 ? room.PlayerTwo : "Esperando rival…")}\nTu lugar: jugador {room.MySeat + 1}";
        var canChoose = _lobby.Ready && room?.Dealt == true && preparation?.MyConfirmed == false && !_lobby.ConfirmationPending;
        if (preparation?.MyConfirmed == true)
        {
            _returnedCards.Clear();
            _returnedCards.UnionWith(preparation.MyReturnedCards);
        }
        _hint.Text = preparation?.Started == true ? "Preparación terminada. Entrando al duelo…" :
            "Toca las cartas que quieres cambiar y confirma una sola vez. Tu rival también debe confirmar.";
        _confirm.Visible = room?.Dealt == true && preparation?.Started != true;
        _confirm.Disabled = !canChoose;
        _confirm.Text = _lobby.ConfirmationPending ? "Confirmando…" : preparation?.MyConfirmed == true ? "Mano confirmada · esperando rival" :
            _returnedCards.Count == 0 ? "Conservar mano y confirmar" : $"Cambiar {_returnedCards.Count} carta(s) y confirmar";
        _preparation.Text = PreparationText(room, preparation);
        foreach (var (id, choice) in _choices)
        {
            choice.SetPressedNoSignal(_returnedCards.Contains(id));
            choice.Disabled = !canChoose;
            choice.Visible = preparation?.Started != true;
            if (_previews.TryGetValue(id, out var preview))
            {
                preview.Disabled = !canChoose;
                if (preview.Chosen != _returnedCards.Contains(id)) { preview.Chosen = _returnedCards.Contains(id); preview.QueueRedraw(); }
            }
        }
        var cards = _lobby.Hand;
        _returnedCards.IntersectWith(cards.Select(c => c.Id));
        var fingerprint = string.Join(",", cards.Select(c => c.Id));
        if (fingerprint == _lastHand) return;
        _lastHand = fingerprint;
        _choices.Clear(); _previews.Clear();
        foreach (var child in _hand.GetChildren()) { _hand.RemoveChild(child); child.QueueFree(); }
        foreach (var card in cards)
        {
            var definition = CardCatalog.Get(card.Definition);
            var choice = new CheckBox { Text = "Cambiar esta carta", ButtonPressed = _returnedCards.Contains(card.Id), Disabled = !canChoose, Visible = preparation?.Started != true };
            choice.Toggled += selected => { if (selected) _returnedCards.Add(card.Id); else _returnedCards.Remove(card.Id); };
            _choices.Add(card.Id, choice);
            var column = new VBoxContainer(); column.AddChild(choice);
            var preview = new HandCard { Card = definition, Chosen = _returnedCards.Contains(card.Id), Mulligan = true };
            preview.Pressed += () => { if (!choice.Disabled) choice.ButtonPressed = !choice.ButtonPressed; };
            _previews.Add(card.Id, preview); column.AddChild(preview); _hand.AddChild(column);
        }
    }

    private void AdjustLobbyLayout()
    {
        if (_content == null) return;
        var inset = ArenaViewport.Compact(this) ? 12 : 40;
        foreach (var side in new[] { "left", "right", "top", "bottom" }) _content.AddThemeConstantOverride("margin_" + side, inset);
    }

    private static string PreparationText(Network.Types.RoomInfo? room, Network.Types.PreparationInfo? preparation)
    {
        if (room == null || preparation == null) return "";
        if (!room.Dealt) return "Comparte el código con tu rival para preparar las manos.";
        if (!preparation.Started)
            return $"Tu mano: {(preparation.MyConfirmed ? "confirmada" : "por confirmar")} · Rival: {(preparation.OpponentConfirmed ? "confirmado" : "por confirmar")}";
        var starter = preparation.StartingSeat == 0 ? room.PlayerOne : room.PlayerTwo;
        return $"Preparación terminada · Turno 1 · Combate\n" +
            $"Dado de inicio: {room.PlayerOne} {preparation.RollOne} · {room.PlayerTwo} {preparation.RollTwo}. Empieza {starter}.\n" +
            $"Bonus de {room.PlayerOne}: {CardCatalog.Get(preparation.BonusOne).Name} · Bonus de {room.PlayerTwo}: {CardCatalog.Get(preparation.BonusTwo).Name}\n" +
            $"{room.PlayerOne}: {preparation.HandCountOne} en mano / {preparation.DeckCountOne} en mazo · " +
            $"{room.PlayerTwo}: {preparation.HandCountTwo} en mano / {preparation.DeckCountTwo} en mazo";
    }

    public override void _ExitTree() => _lobby.Dispose();
    private void CloseToPractice() { _lobby.Dispose(); Closed?.Invoke(); QueueFree(); }
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
