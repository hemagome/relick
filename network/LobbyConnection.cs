using Relick.Network.Types;
using SpacetimeDB.ClientApi;
using SpacetimeDB;
using Relick.Core;
using System.Text.Json;
using System.Text;
using System.Diagnostics;

namespace Relick.Network;

public enum RoomRequest { None, Create, Join, Leave, FindQuickMatch, CancelQuickMatch }

/// <summary>Only receives authorized views; it never runs the practice engine.</summary>
public sealed partial class LobbyConnection : IDisposable
{
    private DbConnection? _connection;
    private Task<bool>? _healthCheck;
    private long _connectionStarted, _lastHealthCheck, _healthCheckStarted;
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private string? _initialSkillCommand;
    private string _initialSkillRoom = "", _initialSkillDefinition = "";
    public InitialSkillInfo? InitialSkills => _connection?.Db.MyInitialSkill.Iter().SingleOrDefault();
    public bool InitialSkillPending => _initialSkillCommand != null;
    private bool _preparationHandDirty = true;
    private Relick.Network.Types.HandCard[] _preparationHand = [];
    public ulong PreparationRevision { get; private set; }
    private string? _confirmationId;
    private List<ulong> _returnedCards = [];
    private string _confirmationRoom = "";
    private string? _playId;
    private string? _comboId;
    private string _comboRoom = "";
    private bool _comboEnabled;
    public bool? RequestedComboResponses => _comboId == null ? null : _comboEnabled;
    private string _playRoom = "";
    private ulong _playRevision;
    private int _playOption;
    private List<int> _playSelection = [];
    private string? _cachedDuelJson;
    private DuelSnapshot? _cachedDuel;
    private string? _cachedHandJson, _cachedOptionsJson;
    private SubscriptionHandle? _preparationSubscription;
    private long _playStarted;
    public long GamePayloadBytes { get; private set; }
    public double LastPlayLatencyMs { get; private set; }
    public bool LastPlayAccepted { get; private set; }
    /// <summary>Why this client's latest play or COMBO preference was not applied; the arena shows it beside the decision.</summary>
    public string PlayRejection { get; private set; } = "";
    /// <summary>Grows with every rejection, so two identical reasons in a row are still two events.</summary>
    public ulong PlayRejectionSerial { get; private set; }
    public event Action<string>? TokenReceived;
    public bool Ready { get; private set; }
    public bool Connecting { get; private set; }
    public RoomRequest PendingRoomRequest { get; private set; }
    public string RoomError { get; private set; } = "";
    public DuelSnapshot? LastResult { get; private set; }
    public string LastResultRoom { get; private set; } = "";
    private string? _sessionToken;
    private string _sessionServer = "", _sessionDatabase = "";
    public ulong Epoch { get; private set; }
    internal string? RecoveredDealtRoomCode { get; private set; }
    public string Message { get; private set; } = "Sin conexión";
    public RoomInfo? Room => _connection?.Db.MyRoom.Iter().SingleOrDefault();
    public PreparationInfo? Preparation => _connection?.Db.MyPreparation.Iter().SingleOrDefault();
    public RoomRelikarioInfo? RoomRelikarios => _connection?.Db.MyRoomRelikarios.Iter().SingleOrDefault();
    private PreparationInfo? _openingResult;
    private string? _openingRoom;
    public PreparationInfo? OpeningResult => _openingRoom == Room?.Code ? _openingResult : null;
    public bool ConfirmationPending => _confirmationId != null;
    public bool PlayPending => _playId != null || _comboId != null;
    public DuelSnapshot? Duel
    {
        get
        {
            var row = _connection?.Db.MyBoard.Iter().SingleOrDefault();
            var hand = _connection?.Db.MyDuelHand.Iter().SingleOrDefault();
            var choices = _connection?.Db.MyChoices.Iter().SingleOrDefault();
            if (row == null || hand == null || choices == null || row.RoomCode != hand.RoomCode || row.RoomCode != choices.RoomCode) return null;
            if (_cachedDuelJson != row.Json || _cachedHandJson != hand.Json || _cachedOptionsJson != choices.Json)
            {
                var packet = JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelBoardPacket)!;
                var own = JsonSerializer.Deserialize(hand.Json, DuelJson.Default.DuelPrivateData)!;
                if (packet.HandVersions[own.MySeat] != hand.Revision || packet.OptionVersions[own.MySeat] != choices.Revision) return null;
                _cachedDuel = DuelPackets.Combine(packet, own, JsonSerializer.Deserialize(choices.Json, DuelJson.Default.ListDuelOption)!);
                _cachedDuelJson = row.Json; _cachedHandJson = hand.Json; _cachedOptionsJson = choices.Json;
            }
            return _cachedDuel;
        }
    }
    // Authoritative SDK rows, ordered once per change; consumers must not mutate this cached array.
    public Relick.Network.Types.HandCard[] Hand
    {
        get
        {
            if (_preparationHandDirty)
            {
                _preparationHand = _connection?.Db.MyHand.Iter().OrderBy(c => c.Position).ToArray() ?? [];
                _preparationHandDirty = false;
            }
            return _preparationHand;
        }
    }

    public void Connect(string server, string database, string? token)
    {
        if (token != _sessionToken || server != _sessionServer || database != _sessionDatabase) ClearResult();
        _sessionServer = server; _sessionDatabase = database;
        Dispose();
        Epoch++;
        RecoveredDealtRoomCode = null;
        _connectionStarted = Stopwatch.GetTimestamp();
        Connecting = true; RoomError = "";
        Message = "Conectando…";
        var builder = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
            .OnConnect((connection, _, newToken) =>
            {
                if (!Connecting) return;
                _sessionToken = newToken;
                TokenReceived?.Invoke(newToken);
                connection.SubscriptionBuilder().OnApplied(_ =>
                {
                    if (!Connecting) return;
                    RecoveredDealtRoomCode = Room is { Dealt: true } room ? room.Code : null;
                    Ready = true;
                    _lastHealthCheck = Stopwatch.GetTimestamp();
                    Connecting = false; PendingRoomRequest = RoomRequest.None;
                    Message = "Conectado";
                    connection.Reducers.EnsureDuel();
                    if (QuickMatchAvailable) RecoverQuickMatch();
                    if (Duel == null) SubscribePreparation();
                    // Reuse the same command after recovery, including when its acknowledgement was lost.
                    if (_initialSkillCommand != null) connection.Reducers.ChooseInitialSkill(_initialSkillRoom, _initialSkillDefinition, _initialSkillCommand);
                    if (_confirmationId != null) connection.Reducers.ConfirmPreparation(_confirmationRoom, _returnedCards, _confirmationId);
                    if (_playId != null) connection.Reducers.PlayDuelV2(_playRoom, _playRevision, _playId, _playOption, _playSelection);
                    if (_comboId != null) connection.Reducers.SetComboResponses(_comboRoom, _comboId, _comboEnabled);
                }).OnError((_, error) =>
                {
                    Ready = false;
                    Connecting = false;
                    Message = "No se pudo sincronizar la sala: " + error.Message;
                }).Subscribe(new[] { "SELECT * FROM my_room", "SELECT * FROM my_board", "SELECT * FROM my_duel_hand", "SELECT * FROM my_choices", "SELECT * FROM my_move_result", "SELECT * FROM my_account", "SELECT * FROM my_decks" });
                connection.SubscriptionBuilder().OnApplied(_ =>
                {
                    QuickMatchAvailable = true;
                    if (Ready) RecoverQuickMatch();
                }).OnError((_, _) => QuickMatchAvailable = false).Subscribe(["SELECT * FROM my_quick_search"]);
                // Apart, like the quick search: a server without deck styles still syncs the room and the decks.
                connection.SubscriptionBuilder().Subscribe(["SELECT * FROM my_deck_styles"]);
                connection.SubscriptionBuilder().Subscribe(["SELECT * FROM my_room_relikarios"]);
                SubscribeProfiles(connection);
                SubscribePersonalization(connection);
            })
            .OnConnectError(error => { Ready = Connecting = false; Message = "No se pudo conectar. Comprueba tu conexión y pulsa Reconectar."; })
            .OnDisconnect((_, error) => { Ready = Connecting = false; Message = "Se perdió la conexión. Pulsa Reconectar para recuperar tu sala."; });
        if (!string.IsNullOrEmpty(token)) builder = builder.WithToken(token);
        _connection = builder.Build();
        void HandChanged() { _preparationHandDirty = true; PreparationRevision++; }
        _connection.Db.MyHand.OnInsert += (_, _) => HandChanged();
        _connection.Db.MyHand.OnUpdate += (_, _, _) => HandChanged();
        _connection.Db.MyHand.OnDelete += (_, _) => HandChanged();
        _connection.Db.MyPreparation.OnInsert += (_, _) => PreparationRevision++;
        _connection.Db.MyPreparation.OnUpdate += (_, _, _) => PreparationRevision++;
        _connection.Db.MyPreparation.OnDelete += (_, _) => PreparationRevision++;
        _connection.Db.MyRoomRelikarios.OnInsert += (_, _) => PreparationRevision++;
        _connection.Db.MyRoomRelikarios.OnUpdate += (_, _, _) => PreparationRevision++;
        _connection.Db.MyRoomRelikarios.OnDelete += (_, _) => PreparationRevision++;
        _connection.Db.MyInitialSkill.OnInsert += (_, _) => PreparationRevision++;
        _connection.Db.MyInitialSkill.OnUpdate += (_, _, _) => PreparationRevision++;
        _connection.Db.MyInitialSkill.OnDelete += (_, _) => PreparationRevision++;
        _connection.Db.MyRoom.OnInsert += (_, _) => PreparationRevision++;
        _connection.Db.MyRoom.OnUpdate += (_, _, _) => PreparationRevision++;
        _connection.Db.MyRoom.OnDelete += (_, _) => PreparationRevision++;
        _connection.OnUnhandledReducerError += (_, error) => Message = error.Message;
        _connection.Reducers.OnCreateRoom += (context, _) => RoomReply(context);
        _connection.Reducers.OnJoinRoom += (context, _, _) => RoomReply(context);
        _connection.Reducers.OnLeaveRoom += context => RoomReply(context);
        _connection.Reducers.OnEnsureDuel += context => SampleServerTime(context.Event.Timestamp.MicrosecondsSinceUnixEpoch);
        RegisterQuickMatchReplies();
        RegisterDeckReplies();
        RegisterProfileReplies();
        RegisterPersonalizationReplies();
        void BoardResult(string json, string roomCode)
        {
            var packet = JsonSerializer.Deserialize(json, DuelJson.Default.DuelBoardPacket)!;
            if (packet.Board.Winner == null || Room is not { } room || room.Code != roomCode) return;
            // Keep a public, read-only result even if the room disappears in the same FrameTick.
            packet.Board.MySeat = room.MySeat; packet.Board.Options = []; packet.Board.Visuals = [];
            foreach (var player in packet.Board.Players) { player.Hand = []; player.HandOwners = null; }
            LastResult = packet.Board; LastResultRoom = roomCode;
            RememberFullResult();
        }
        void RememberFullResult()
        {
            if (Duel is { Winner: not null } final && Room is { } room)
            { LastResult = final; LastResultRoom = room.Code; }
        }
        _connection.Db.MyBoard.OnInsert += (_, row) => BoardResult(row.Json, row.RoomCode);
        _connection.Db.MyBoard.OnUpdate += (_, _, row) => BoardResult(row.Json, row.RoomCode);
        _connection.Db.MyDuelHand.OnInsert += (_, _) => RememberFullResult();
        _connection.Db.MyDuelHand.OnUpdate += (_, _, _) => RememberFullResult();
        _connection.Db.MyChoices.OnInsert += (_, _) => RememberFullResult();
        _connection.Db.MyChoices.OnUpdate += (_, _, _) => RememberFullResult();
        _connection.Db.MyBoard.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyBoard.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyDuelHand.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyDuelHand.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyChoices.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyChoices.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Reducers.OnPlayDuelV2 += (context, _, _, _, _, _) =>
        {
            SampleServerTime(context.Event.Timestamp.MicrosecondsSinceUnixEpoch);
            switch (context.Event.Status)
            {
                case Status.Committed:
                    ResolvePlayResult(); break;
                case Status.Failed(var reason):
                    _playId = null; RejectPlay(reason); break;
                case Status.OutOfEnergy:
                    _playId = null; RejectPlay("El servidor no pudo ejecutar la jugada. Inténtalo de nuevo."); break;
                default:
                    Message = "No se recibió la confirmación. Pulsa Reconectar para comprobarla."; break;
            }
        };
        _connection.Reducers.OnSetComboResponses += (context, _, _, _) =>
        {
            switch (context.Event.Status)
            {
                case Status.Committed: ResolvePlayResult(); break;
                case Status.Failed(var reason): _comboId = null; RejectPlay(reason); break;
                case Status.OutOfEnergy: _comboId = null; RejectPlay("No se pudo cambiar las respuestas COMBO. Inténtalo de nuevo."); break;
                default: Message = "No se recibió la preferencia. Pulsa Reconectar para comprobarla."; break;
            }
        };
        _connection.Reducers.OnChooseInitialSkill += (context, _, _, _) =>
        {
            switch (context.Event.Status)
            {
                case Status.Committed: _initialSkillCommand = null; Message = "Habilidad elegida"; break;
                case Status.Failed(var reason): _initialSkillCommand = null; Message = reason; break;
                case Status.OutOfEnergy: _initialSkillCommand = null; Message = "No se pudo elegir la habilidad. Inténtalo de nuevo."; break;
                default: Message = "No se recibió la elección. Pulsa Reconectar para comprobarla."; break;
            }
        };
        _connection.Reducers.OnConfirmPreparation += (context, _, _, _) =>
        {
            switch (context.Event.Status)
            {
                case Status.Committed:
                    _confirmationId = null;
                    Message = "Mano confirmada";
                    break;
                case Status.Failed(var reason):
                    _confirmationId = null;
                    Message = reason;
                    break;
                case Status.OutOfEnergy:
                    _confirmationId = null;
                    Message = "El servidor no pudo confirmar la mano. Inténtalo de nuevo.";
                    break;
                default:
                    Message = "No se recibió la confirmación. Pulsa Reconectar para comprobarla.";
                    break;
            }
        };
    }

    private void SubscribePreparation()
    {
        if (_connection == null || _preparationSubscription != null) return;
        _preparationSubscription = _connection.SubscriptionBuilder().Subscribe(new[] { "SELECT * FROM my_hand", "SELECT * FROM my_preparation", "SELECT * FROM my_initial_skill" });
    }
    private void ResolvePlayResult()
    {
        var result = _connection?.Db.MyMoveResult.Iter().SingleOrDefault();
        if (_comboId != null && result?.CommandId == _comboId)
        {
            _comboId = null;
            if (result.Accepted) Message = "Respuestas COMBO actualizadas"; else RejectPlay(result.Error);
            return;
        }
        if (_playId == null || result?.CommandId != _playId) return;
        LastPlayLatencyMs = Stopwatch.GetElapsedTime(_playStarted).TotalMilliseconds;
        LastPlayAccepted = result.Accepted;
        _playId = null;
        if (result.Accepted) Message = "Jugada confirmada"; else RejectPlay(result.Error);
    }

    /// <summary>Records a refused play. Uncertain deliveries are not rejections: they keep their command for reconnection.</summary>
    internal void RejectPlay(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) reason = "El servidor rechazó la jugada. Revisa el tablero e inténtalo de nuevo.";
        Message = reason; PlayRejection = reason; PlayRejectionSerial++;
    }
    public void Tick()
    {
        _connection?.FrameTick();
        CheckConnectionHealth();
        TickQuickMatch();
        ResolvePlayResult();
        if (Duel is { Winner: not null } final && Room is { } finalRoom)
        { LastResult = final; LastResultRoom = finalRoom.Code; }
        var preparation = Preparation;
        if (preparation?.Started == true) { _openingResult = preparation; _openingRoom = Room?.Code; }
        if (Duel != null && _preparationSubscription != null)
        {
            _preparationSubscription.Unsubscribe(); _preparationSubscription = null;
        }
        else if (Ready && Duel == null && _preparationSubscription == null) SubscribePreparation();
    }

    private void CheckConnectionHealth()
    {
        if (Connecting && Stopwatch.GetElapsedTime(_connectionStarted) >= ConnectTimeout)
        {
            LoseConnection("No se pudo conectar. Comprueba tu conexión y pulsa Reconectar.");
            return;
        }
        if (!Ready || _connection == null) return;
        if (!_connection.IsActive)
        {
            LoseConnection();
            return;
        }
        if (_healthCheck is { IsCompleted: true })
        {
            var healthy = _healthCheck.GetAwaiter().GetResult();
            _healthCheck = null;
            if (!healthy) { LoseConnection(); return; }
            _lastHealthCheck = Stopwatch.GetTimestamp();
        }
        if (_healthCheck != null)
        {
            if (Stopwatch.GetElapsedTime(_healthCheckStarted) >= HealthCheckTimeout) LoseConnection();
            return;
        }
        if (Stopwatch.GetElapsedTime(_lastHealthCheck) < HealthCheckInterval) return;
        _healthCheckStarted = Stopwatch.GetTimestamp();
        _healthCheck = CheckServer(_connection);
    }

    private static async Task<bool> CheckServer(DbConnection connection)
    {
        // A read over the same socket detects silent outages even in an idle, empty lobby.
        // It neither changes room state nor depends on the machine's Wi-Fi interface.
        try { await connection.Db.MyRoom.RemoteQuery(""); return true; }
        catch (Exception) { return false; }
    }

    private void LoseConnection(string message = "Se perdió la conexión. Pulsa Reconectar para recuperar tu sala.")
    {
        Ready = Connecting = false;
        Message = message;
        _healthCheck = null;
        // Keep cached room data and pending commands until the player reconnects.
        _connection?.Disconnect();
    }
    public void ClearResult() { LastResult = null; LastResultRoom = ""; }

    private void RoomReply(ReducerEventContext context)
    {
        var request = PendingRoomRequest;
        if (request == RoomRequest.None) return;
        PendingRoomRequest = RoomRequest.None;
        switch (context.Event.Status)
        {
            case Status.Committed:
                RoomError = "";
                Message = request switch
                {
                    RoomRequest.Leave => "Sala cerrada.",
                    RoomRequest.Create => "Sala creada. Comparte el código con tu rival.",
                    RoomRequest.FindQuickMatch => Room == null ? "Buscando rival…" : "Rival encontrado.",
                    RoomRequest.CancelQuickMatch => Room == null ? "" : "Rival encontrado.",
                    _ => "Te uniste a la sala."
                };
                break;
            case Status.Failed(var reason): RoomError = Message = reason; break;
            case Status.OutOfEnergy: RoomError = Message = "El servidor no pudo completar la solicitud. Inténtalo de nuevo."; break;
            default: RoomError = Message = "No se recibió la confirmación. Pulsa Reconectar para comprobar tu sala."; break;
        }
    }
    private bool ValidName(string name)
    {
        if (name.Trim().Length is >= 1 and <= 24 && !name.Any(char.IsControl)) return true;
        RoomError = Message = "Escribe un nombre de 1 a 24 caracteres."; return false;
    }
    private void RequestRoom(RoomRequest request, Action send)
    {
        RoomError = ""; PendingRoomRequest = request;
        Message = request switch
        {
            RoomRequest.Create => "Creando sala…", RoomRequest.Join => "Entrando a la sala…",
            RoomRequest.FindQuickMatch => "Iniciando búsqueda…", RoomRequest.CancelQuickMatch => "Cancelando búsqueda…",
            _ => "Cerrando sala…"
        };
        try { send(); }
        catch (Exception) { RoomError = Message = "No se pudo confirmar la solicitud. Pulsa Reconectar para comprobar tu sala."; }
    }
    public AccountInfo? Account => _connection?.Db.MyAccount.Iter().SingleOrDefault();
    public void SetDisplayName(string name)
    {
        if (Ready && ValidName(name) && name.Trim() != Account?.Name) _connection!.Reducers.SetDisplayName(name.Trim());
    }
    public void Create(string name)
    {
        if (!Ready || PendingRoomRequest != RoomRequest.None || Room != null || Searching || !ValidName(name)) return;
        ClearResult(); RequestRoom(RoomRequest.Create, () => _connection!.Reducers.CreateRoom(name.Trim()));
    }
    public void Join(string code, string name)
    {
        if (!Ready || PendingRoomRequest != RoomRequest.None || Room != null || Searching || !ValidName(name)) return;
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 8 || !code.All(char.IsAsciiLetterOrDigit))
        { RoomError = Message = "Escribe el código de 8 letras y números que compartió tu rival."; return; }
        ClearResult(); RequestRoom(RoomRequest.Join, () => _connection!.Reducers.JoinRoom(code, name.Trim()));
    }
    public void Leave() => Leave(Room?.Code);
    public void Leave(string? expectedRoom)
    {
        if (!Ready || PendingRoomRequest != RoomRequest.None || Room == null || Room.Code != expectedRoom) return;
        if (Duel is { Winner: null, Revision: > 0 })
        { RoomError = Message = "Abandona el duelo desde Menú antes de cerrar la sala."; return; }
        _initialSkillCommand = null;
        _confirmationId = null;
        _playId = null;
        _comboId = null;
        RequestRoom(RoomRequest.Leave, () => _connection!.Reducers.LeaveRoom());
    }

    public void SetComboResponses(bool enabled, string expectedRoom)
    {
        if (!Ready || PlayPending || Room?.Code != expectedRoom || Duel is not { Winner: null }) return;
        _comboRoom = expectedRoom; _comboEnabled = enabled; _comboId = Guid.NewGuid().ToString("N");
        try
        {
            _connection!.Reducers.SetComboResponses(_comboRoom, _comboId, enabled);
            Message = "Actualizando respuestas COMBO…";
        }
        catch (Exception) { Message = "No se pudo comprobar la preferencia. Pulsa Reconectar."; }
    }

    public void Play(int option, IEnumerable<int>? selectedCards = null, ulong? expectedRevision = null, string? expectedRoom = null, DuelSnapshot? expectedSnapshot = null)
    {
        var duel = Duel;
        if (!Ready || PlayPending || duel == null || Room == null) return;
        if ((expectedRevision != null && expectedRevision != duel.Revision) || (expectedRoom != null && expectedRoom != Room.Code) ||
            (expectedSnapshot != null && !ReferenceEquals(expectedSnapshot, duel)))
        {
            RejectPlay("La partida cambió. Revisa el tablero antes de volver a jugar.");
            return;
        }
        _playId = Guid.NewGuid().ToString("N");
        _playStarted = Stopwatch.GetTimestamp(); LastPlayAccepted = false;
        _playRoom = Room.Code; _playRevision = duel.Revision; _playOption = option;
        _playSelection = selectedCards?.Order().ToList() ?? [];
        try
        {
            _connection!.Reducers.PlayDuelV2(_playRoom, _playRevision, _playId, _playOption, _playSelection);
            Message = "Enviando jugada…";
        }
        catch (Exception) { Message = "No se pudo comprobar la jugada. Pulsa Reconectar."; }
    }

    public void ChooseInitialSkill(string definition)
    {
        if (!Ready || Room is not { Dealt: true } room || InitialSkillPending ||
            InitialSkills is not { Selected: false } skills || !skills.Options.Contains(definition)) return;
        _initialSkillRoom = room.Code; _initialSkillDefinition = definition; _initialSkillCommand = Guid.NewGuid().ToString("N");
        try
        {
            _connection!.Reducers.ChooseInitialSkill(_initialSkillRoom, definition, _initialSkillCommand);
            Message = "Eligiendo tu habilidad inicial…";
        }
        catch (Exception) { Message = "No se pudo comprobar la elección. Pulsa Reconectar."; }
    }

    public void Confirm(IEnumerable<ulong> returnedCards)
    {
        if (!Ready || Room?.Dealt != true || ConfirmationPending || Preparation?.MyConfirmed != false) return;
        _returnedCards = returnedCards.Order().ToList();
        _confirmationRoom = Room.Code;
        _confirmationId = Guid.NewGuid().ToString("N");
        try
        {
            _connection!.Reducers.ConfirmPreparation(_confirmationRoom, _returnedCards, _confirmationId);
            Message = "Confirmando tu mano…";
        }
        catch (Exception error)
        {
            // Delivery may be uncertain: retain the command so reconnection can safely retry it.
            Message = "No se pudo comprobar la confirmación. Pulsa Reconectar. " + error.Message;
        }
    }

    public void Dispose()
    {
        Ready = Connecting = false;
        _serverTime = null;
        _healthCheck = null;
        QuickMatchAvailable = false;
        _preparationHand = []; _preparationHandDirty = true; PreparationRevision++;
        _cachedDuelJson = _cachedHandJson = _cachedOptionsJson = null; _cachedDuel = null;
        if (_connection?.IsActive == true) _connection.Disconnect();
        _connection = null; _preparationSubscription = null;
        _decks = null; DeckPending = false; DeckRevision++;
        ProfilesAvailable = ProfilePending = false; ProfileRevision++;
        ForgetPersonalization();
    }
}
