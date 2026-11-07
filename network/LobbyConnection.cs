using Relick.Network.Types;
using SpacetimeDB.ClientApi;
using SpacetimeDB;
using Relick.Core;
using System.Text.Json;
using System.Text;
using System.Diagnostics;

namespace Relick.Network;

/// <summary>Only receives authorized views; it never runs the practice engine.</summary>
public sealed class LobbyConnection : IDisposable
{
    private DbConnection? _connection;
    private string? _confirmationId;
    private List<ulong> _returnedCards = [];
    private string _confirmationRoom = "";
    private string? _playId;
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
    public event Action<string>? TokenReceived;
    public bool Ready { get; private set; }
    public ulong Epoch { get; private set; }
    public string Message { get; private set; } = "Sin conexión";
    public RoomInfo? Room => _connection?.Db.MyRoom.Iter().SingleOrDefault();
    public PreparationInfo? Preparation => _connection?.Db.MyPreparation.Iter().SingleOrDefault();
    public bool ConfirmationPending => _confirmationId != null;
    public bool PlayPending => _playId != null;
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
    public Relick.Network.Types.HandCard[] Hand => _connection?.Db.MyHand.Iter().OrderBy(c => c.Position).ToArray() ?? [];

    public void Connect(string server, string database, string? token)
    {
        Dispose();
        Epoch++;
        Message = "Conectando…";
        var builder = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
            .OnConnect((connection, _, newToken) =>
            {
                TokenReceived?.Invoke(newToken);
                connection.SubscriptionBuilder().OnApplied(_ =>
                {
                    Ready = true;
                    Message = "Conectado";
                    connection.Reducers.EnsureDuel();
                    if (Duel == null) SubscribePreparation();
                    // Reuse the same command after recovery, including when its acknowledgement was lost.
                    if (_confirmationId != null) connection.Reducers.ConfirmPreparation(_confirmationRoom, _returnedCards, _confirmationId);
                    if (_playId != null) connection.Reducers.PlayDuelV2(_playRoom, _playRevision, _playId, _playOption, _playSelection);
                }).OnError((_, error) =>
                {
                    Ready = false;
                    Message = "No se pudo sincronizar la sala: " + error.Message;
                }).Subscribe(new[] { "SELECT * FROM my_room", "SELECT * FROM my_board", "SELECT * FROM my_duel_hand", "SELECT * FROM my_choices", "SELECT * FROM my_move_result" });
            })
            .OnConnectError(error => { Ready = false; Message = "No se pudo conectar: " + error.Message; })
            .OnDisconnect((_, error) => { Ready = false; Message = error == null ? "Sin conexión" : "Se perdió la conexión. Pulsa Reconectar."; });
        if (!string.IsNullOrEmpty(token)) builder = builder.WithToken(token);
        _connection = builder.Build();
        _connection.OnUnhandledReducerError += (_, error) => Message = error.Message;
        _connection.Db.MyBoard.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyBoard.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyDuelHand.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyDuelHand.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyChoices.OnInsert += (_, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Db.MyChoices.OnUpdate += (_, _, row) => GamePayloadBytes += Encoding.UTF8.GetByteCount(row.Json);
        _connection.Reducers.OnPlayDuelV2 += (context, _, _, _, _, _) =>
        {
            switch (context.Event.Status)
            {
                case Status.Committed:
                    ResolvePlayResult(); break;
                case Status.Failed(var reason):
                    _playId = null; Message = reason; break;
                case Status.OutOfEnergy:
                    _playId = null; Message = "El servidor no pudo ejecutar la jugada. Inténtalo de nuevo."; break;
                default:
                    Message = "No se recibió la confirmación. Pulsa Reconectar para comprobarla."; break;
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
        _preparationSubscription = _connection.SubscriptionBuilder().Subscribe(new[] { "SELECT * FROM my_hand", "SELECT * FROM my_preparation" });
    }
    private void ResolvePlayResult()
    {
        var result = _connection?.Db.MyMoveResult.Iter().SingleOrDefault();
        if (_playId == null || result?.CommandId != _playId) return;
        LastPlayLatencyMs = Stopwatch.GetElapsedTime(_playStarted).TotalMilliseconds;
        LastPlayAccepted = result.Accepted;
        _playId = null; Message = result.Accepted ? "Jugada confirmada" : result.Error;
    }
    public void Tick()
    {
        _connection?.FrameTick(); ResolvePlayResult();
        if (Duel != null && _preparationSubscription != null)
        {
            _preparationSubscription.Unsubscribe(); _preparationSubscription = null;
        }
        else if (Ready && Duel == null && _preparationSubscription == null) SubscribePreparation();
    }
    public void Create(string name) => Send(() => _connection!.Reducers.CreateRoom(name));
    public void Join(string code, string name) => Send(() => _connection!.Reducers.JoinRoom(code, name));
    public void Leave()
    {
        _confirmationId = null;
        _playId = null;
        Send(() => _connection!.Reducers.LeaveRoom());
    }

    public void Play(int option, IEnumerable<int>? selectedCards = null, ulong? expectedRevision = null, string? expectedRoom = null)
    {
        var duel = Duel;
        if (!Ready || PlayPending || duel == null || Room == null) return;
        if ((expectedRevision != null && expectedRevision != duel.Revision) || (expectedRoom != null && expectedRoom != Room.Code))
        {
            Message = "La partida cambió. Revisa el tablero antes de volver a jugar.";
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

    private void Send(Action send)
    {
        if (!Ready) return;
        Message = "Conectado";
        try { send(); }
        catch (Exception error) { Message = error.Message; }
    }

    public void Dispose()
    {
        Ready = false;
        if (_connection?.IsActive == true) _connection.Disconnect();
        _connection = null; _preparationSubscription = null;
    }
}
