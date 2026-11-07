using Relick.Network.Types;
using Relick.Core;
using System.Text.Json;

if (args.FirstOrDefault()?.StartsWith("--") == true) return await ScalabilityChecks.Run(args);

var server = args.ElementAtOrDefault(0) ?? "http://127.0.0.1:3100";
var database = args.ElementAtOrDefault(1) ?? "relick-dev";
var clients = new List<Client>();
var services = new List<Relick.Network.LobbyConnection>();
Client Connect(string? token = null)
{
    var client = new Client();
    var builder = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
        .OnConnect((connection, _, value) =>
        {
            client.Token = value;
            connection.SubscriptionBuilder().OnApplied(_ => client.Ready = true)
                .OnError((_, error) => client.Fatal = error.Message)
                .Subscribe(new[] { "SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_preparation", "SELECT * FROM my_initial_skill", "SELECT * FROM my_duel", "SELECT * FROM my_move_result" });
        }).OnConnectError(error => client.Fatal = error.Message);
    if (token != null) builder = builder.WithToken(token);
    client.Connection = builder.Build();
    client.Connection.OnUnhandledReducerError += (_, error) => client.Errors.Add(error.Message);
    string? lastRejection = null;
    void Result(MoveResult result)
    {
        if (!result.Accepted && lastRejection != result.CommandId) { lastRejection = result.CommandId; client.Errors.Add(result.Error); }
    }
    client.Connection.Db.MyMoveResult.OnInsert += (_, result) => Result(result);
    client.Connection.Db.MyMoveResult.OnUpdate += (_, _, result) => Result(result);
    clients.Add(client);
    Until(() => client.Ready);
    return client;
}
void Until(Func<bool> condition)
{
    var deadline = DateTime.UtcNow.AddSeconds(20);
    do
    {
        foreach (var client in clients.Where(c => c.Connection.IsActive)) client.Connection.FrameTick();
        foreach (var service in services) service.Tick();
        var fatal = clients.FirstOrDefault(c => c.Fatal != null)?.Fatal;
        if (fatal != null) throw new Exception(fatal);
        if (condition()) return;
        Thread.Sleep(10);
    } while (DateTime.UtcNow < deadline);
    throw new Exception("Tiempo agotado esperando al servidor.");
}
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
void Rejected(Client client, Action command, string message)
{
    var errors = client.Errors.Count;
    command();
    Until(() => client.Errors.Count > errors);
    Check(true, message);
}
string OpeningFingerprint(Client client) => System.Text.Json.JsonSerializer.Serialize(
    new { Start = client.Preparation, Hand = client.Hand }, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });

try
{
    var a = Connect(); var b = Connect(); var outsider = Connect();
    Check(a.Room == null && b.Room == null && outsider.Room == null, "Identidades nuevas sin acceso a otras salas");
    a.Connection.Reducers.CreateRoom("Ana");
    Until(() => a.Room != null);
    var code = a.Room!.Code;
    Check(a.Hand.Length == 0 && b.Room == null, "Sala de espera privada y reparto aplazado");
    Rejected(a, () => a.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N")), "No se puede confirmar antes de entrar el rival");
    b.Connection.Reducers.JoinRoom(code.ToLowerInvariant(), "Beto");
    Until(() => a.InitialSkills != null && b.InitialSkills != null);
    Check(a.Hand.Length == 0 && b.Hand.Length == 0 && a.InitialSkills!.Options.ToHashSet().SetEquals(["destiny-shuffle", "magnet-shuffle"]), "Antes de elegir solo se ofrecen las habilidades del propio mazo");
    Rejected(a, () => a.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N")), "Mulligan antes de elegir rechazado");
    Rejected(a, () => a.Connection.Reducers.ChooseInitialSkill(code, "attack", Guid.NewGuid().ToString("N")), "Una carta que no es habilidad se rechaza");
    Rejected(a, () => a.Connection.Reducers.ChooseInitialSkill(code, "missing", Guid.NewGuid().ToString("N")), "Una habilidad fuera del mazo se rechaza");
    Rejected(outsider, () => outsider.Connection.Reducers.ChooseInitialSkill(code, "destiny-shuffle", Guid.NewGuid().ToString("N")), "Un tercero no elige habilidades ajenas");
    var skillCommand = Guid.NewGuid().ToString("N");
    a.Connection.Reducers.ChooseInitialSkill(code, "destiny-shuffle", skillCommand);
    b.Connection.Reducers.ChooseInitialSkill(code, "magnet-shuffle", Guid.NewGuid().ToString("N"));
    Until(() => a.Hand.Length == 5 && b.Hand.Length == 5);
    var skillIds = a.Hand.Select(c => c.Id).ToArray();
    a.Connection.Reducers.ChooseInitialSkill(code, "destiny-shuffle", skillCommand);
    Rejected(a, () => a.Connection.Reducers.ChooseInitialSkill(code, "magnet-shuffle", Guid.NewGuid().ToString("N")), "Una segunda elección no cambia la mano");
    Check(a.Hand.Select(c => c.Id).SequenceEqual(skillIds), "Reintentar la misma elección no vuelve a repartir");
    Check(a.Room!.MySeat == 0 && b.Room!.MySeat == 1 && a.Room.Dealt && b.Room.Dealt, "Dos jugadores ven la misma sala y sus lugares");
    Check(a.Hand[0].Definition == "destiny-shuffle" && b.Hand[0].Definition == "magnet-shuffle", "Mano inicial consistente con la práctica");
    Check(!a.Hand.Select(c => c.Id).Intersect(b.Hand.Select(c => c.Id)).Any(), "Copias de cartas con identificadores únicos por jugador");
    Check(outsider.Room == null && outsider.Hand.Length == 0, "Un tercero no recibe la sala ni las manos");
    outsider.Connection.Reducers.JoinRoom(code, "Tercero");
    Until(() => outsider.Errors.Count > 0);
    Check(outsider.Room == null && outsider.Hand.Length == 0, "Servidor rechaza al tercer jugador");
    var denied = false;
    outsider.Connection.SubscriptionBuilder().OnError((_, _) => denied = true).Subscribe(new[] { "SELECT * FROM card" });
    Until(() => denied);
    Check(denied, "Consulta directa a cartas privadas rechazada por el servidor");
    // A retry is followed by a failed reducer as an ordering barrier.
    var ids = a.Hand.Select(c => c.Id).ToArray();
    a.Connection.Reducers.CreateRoom("Ana");
    a.Connection.Reducers.JoinRoom(code, "Ana");
    a.Connection.Reducers.JoinRoom("INVALIDA", "Ana");
    var retryErrors = a.Errors.Count;
    Until(() => a.Errors.Count > retryErrors);
    Check(a.Hand.Select(c => c.Id).SequenceEqual(ids), "Reintentos no crean salas ni duplican el reparto");
    var token = a.Token;
    a.Connection.Disconnect();
    clients.Remove(a);
    var restored = Connect(token);
    Until(() => restored.Hand.Length == 5);
    Check(restored.Room!.Code == code && restored.InitialSkills?.Chosen == "destiny-shuffle" && restored.Hand.Select(c => c.Id).SequenceEqual(ids), "Reconexión conserva identidad, sala y cartas");
    Rejected(outsider, () => outsider.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N")), "Una identidad ajena no puede confirmar la preparación");
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, [b.Hand[0].Id], Guid.NewGuid().ToString("N")), "Rechazo de cartas pertenecientes al rival");
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, [ids[0], ids[0]], Guid.NewGuid().ToString("N")), "Rechazo de identificadores repetidos");
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, [ulong.MaxValue], Guid.NewGuid().ToString("N")), "Rechazo de una carta inexistente");
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, [], "invalido"), "Identificador de comando inválido rechazado");
    Check(restored.Preparation?.MyConfirmed == false && restored.Hand.Select(c => c.Id).SequenceEqual(ids), "Solicitudes inválidas no modifican la mano ni confirman");
    var selectedA = ids.Take(2).ToList();
    var originalB = b.Hand.Select(c => c.Id).ToList();
    var commandA = Guid.NewGuid().ToString("N");
    var commandB = Guid.NewGuid().ToString("N");
    restored.Connection.Reducers.ConfirmPreparation(code, selectedA, commandA);
    Until(() => restored.Preparation?.MyConfirmed == true && b.Preparation?.OpponentConfirmed == true);
    Check(restored.Preparation?.Started == false && b.Preparation?.MyConfirmed == false && restored.Hand.Select(c => c.Id).SequenceEqual(ids), "La primera confirmación espera al rival sin adelantar cartas o dados");
    restored.Connection.Reducers.ConfirmPreparation(code, selectedA.AsEnumerable().Reverse().ToList(), commandA);
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N")), "Una confirmación posterior distinta no cambia la elección");
    var confirmedToken = restored.Token;
    restored.Connection.Disconnect(); clients.Remove(restored);
    restored = Connect(confirmedToken);
    Until(() => restored.Preparation?.MyConfirmed == true);
    Check(restored.Preparation?.Started == false && restored.Hand.Length == 5 && restored.Preparation.MyReturnedCards.SequenceEqual(selectedA.Order()), "Reconectar conserva la confirmación y selección mientras se espera al rival");
    Check(b.Preparation!.MyReturnedCards.Count == 0, "El rival no recibe los identificadores seleccionados por el otro jugador");
    b.Connection.Reducers.ConfirmPreparation(code, originalB, commandB);
    Until(() => restored.Preparation?.Started == true && b.Preparation?.Started == true);
    var opening = restored.Preparation!;
    Check(opening.RollOne is >= 1 and <= 6 && opening.RollTwo is >= 1 and <= 6 && opening.RollOne != opening.RollTwo &&
        opening.StartingSeat == (opening.RollOne > opening.RollTwo ? 0 : 1), "Dados de servidor sin empate determinan quién empieza");
    Check(opening.BonusOne.Length > 0 && opening.BonusTwo.Length > 0 && b.Preparation!.BonusOne == opening.BonusOne &&
        b.Preparation.RollOne == opening.RollOne && b.Preparation.RollTwo == opening.RollTwo, "Ambos jugadores reciben los mismos Bonus y resultado de inicio");
    Check(restored.Hand.Length == (opening.StartingSeat == 0 ? 6 : 5) && b.Hand.Length == (opening.StartingSeat == 1 ? 6 : 5), "El jugador inicial roba una carta al empezar el primer turno");
    Check(!restored.Hand.Select(c => c.Id).Intersect(selectedA).Any() && ids.Skip(2).All(id => restored.Hand.Any(c => c.Id == id)), "Cambio parcial conserva las cartas elegidas para mantener");
    Check(!b.Hand.Select(c => c.Id).Intersect(originalB).Any(), "Se pueden cambiar las cinco cartas de la mano inicial");
    Check(opening.HandCountOne + opening.DeckCountOne + 1 == 20 && opening.HandCountTwo + opening.DeckCountTwo + 1 == 20, "Se conservan las veinte cartas por jugador con Bonus fuera del mazo");
    var snapshot = OpeningFingerprint(restored);
    restored.Connection.Reducers.ConfirmPreparation(code, selectedA, commandA);
    b.Connection.Reducers.ConfirmPreparation(code, originalB, commandB);
    Rejected(restored, () => restored.Connection.Reducers.ConfirmPreparation(code, selectedA, Guid.NewGuid().ToString("N")), "Confirmación nueva después del inicio rechazada");
    Check(OpeningFingerprint(restored) == snapshot, "Reintentos después del inicio no repiten cambios, Bonus, dados ni robo");
    var startToken = restored.Token;
    restored.Connection.Disconnect(); clients.Remove(restored);
    restored = Connect(startToken);
    Until(() => restored.Preparation?.Started == true);
    Check(OpeningFingerprint(restored) == snapshot, "Reconexión restaura exactamente el inicio persistido");
    Check(outsider.Preparation == null && outsider.Hand.Length == 0, "El inicio y las cartas nuevas siguen aislados de terceros");
    restored.Connection.Reducers.LeaveRoom();
    Until(() => restored.Room == null && b.Room == null && b.Hand.Length == 0);
    Check(restored.Hand.Length == 0, "Cerrar sala elimina acceso y cartas de ambos jugadores");
    Check(restored.Preparation == null && b.Preparation == null, "Cerrar sala retira también la vista de preparación");

    // Exercise the production connection class, including an in-flight confirmation retry.
    using var service = new Relick.Network.LobbyConnection();
    var serviceToken = "";
    service.TokenReceived += token => serviceToken = token;
    services.Add(service);
    service.Connect(server, database, null);
    Until(() => service.Ready);
    service.Create("Cliente Godot");
    Until(() => service.Room != null);
    var nextCode = service.Room!.Code;
    b.Connection.Reducers.JoinRoom(nextCode, "Beto");
    Until(() => b.InitialSkills?.Selected == false && service.InitialSkills?.Selected == false);
    b.Connection.Reducers.ChooseInitialSkill(nextCode, "magnet-shuffle", Guid.NewGuid().ToString("N")); service.ChooseInitialSkill("destiny-shuffle");
    service.Connect(server, database, serviceToken);
    Until(() => b.Hand.Length == 5 && service.Hand.Length == 5);
    Rejected(b, () => b.Connection.Reducers.ConfirmPreparation(code, [], commandB), "Una confirmación atrasada no se aplica a otra sala");
    Check(b.Preparation?.MyConfirmed == false, "La sala nueva empieza sin confirmaciones heredadas");
    service.Confirm([]);
    service.Connect(server, database, serviceToken);
    Until(() => service.Ready && service.Preparation?.MyConfirmed == true && !service.ConfirmationPending);
    b.Connection.Reducers.ConfirmPreparation(nextCode, [], Guid.NewGuid().ToString("N"));
    Until(() => service.Duel != null && b.Preparation?.Started == true);
    Check(service.Duel!.Players[service.Duel.MySeat].Hand[0] == "destiny-shuffle" && b.Hand[0].Definition == "magnet-shuffle", "Conservar la mano funciona en ambos lugares y con reintento del cliente real");

    var fighterA = Connect(serviceToken);
    Until(() => fighterA.Duel != null && b.Duel != null);
    Check(fighterA.Duel!.Revision == 0 && b.Duel!.Revision == 0 && outsider.Duel == null, "La arena inicial está disponible solo para los miembros de la sala");
    Check(fighterA.Duel.Players[1].Hand.All(id => id == null) && b.Duel!.Players[0].Hand.All(id => id == null), "Cada arena oculta la mano del rival");
    Rejected(outsider, () => outsider.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), 0, []), "Un tercero no puede jugar ni elegir actor");
    var starter = fighterA.Duel.CurrentPlayer == 0 ? fighterA : b;
    var learn = starter.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Learn);
    Rejected(starter, () => starter.Connection.Reducers.PlayDuel(nextCode, 0, Guid.NewGuid().ToString("N"), learn, []), "El protocolo anterior exige actualizar el cliente");
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), int.MaxValue, []), "Opción inventada rechazada por el servidor");
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), learn, [0]), "Descartes adicionales no pueden modificar una jugada normal");
    var learnId = Guid.NewGuid().ToString("N");
    starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn, []);
    Until(() => fighterA.Duel?.Revision == 1 && b.Duel?.Revision == 1);
    Check(starter.Duel!.Players[starter.Duel.MySeat].Skills.Length == 1, "Enseñar una habilidad actualiza ambos tableros");
    Check(fighterA.Hand.Length == 0 && b.Hand.Length == 0, "Las manos iniciales dejan de usarse cuando el motor de combate toma el control");
    starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn, []);
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn + 1, []), "Un identificador reutilizado con otra jugada se rechaza");
    Check(starter.Duel.Revision == 1 && starter.Duel.Players[starter.Duel.MySeat].Skills.Length == 1, "Reenviar una jugada confirmada no la ejecuta dos veces");
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), learn, []), "Una selección de un tablero anterior se rechaza");
    Rejected(starter, () => starter.Connection.Reducers.LeaveRoom(), "Cerrar una sala no elimina un duelo en curso");
    using (var turnService = new Relick.Network.LobbyConnection())
    {
        services.Add(turnService); turnService.Connect(server, database, starter.Token);
        Until(() => turnService.Ready && turnService.Duel?.Revision == 1);
        var advance = turnService.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Advance);
        turnService.Play(advance, expectedRevision: 1, expectedRoom: "OTRA-SALA");
        Check(!turnService.PlayPending && turnService.Duel.Revision == 1, "Un botón de otra sala no se envía como jugada de la sala actual");
        turnService.Play(advance);
        turnService.Connect(server, database, starter.Token);
        Until(() => turnService.Ready && !turnService.PlayPending && turnService.Duel?.Revision == 2);
        Check(turnService.Duel.Phase == Phase.Actions, "Reconectar durante una jugada confirma su entrega sin repetir el avance");
        services.Remove(turnService);
    }
    Until(() => fighterA.Duel?.Revision == 2 && b.Duel?.Revision == 2);
    var combatSteps = 0;
    var decisions = new HashSet<Phase>();
    while (fighterA.Duel!.Winner == null && combatSteps++ < 600)
    {
        var actor = fighterA.Duel.DecisionPlayer == 0 ? fighterA : b;
        var before = actor.Duel!; decisions.Add(before.Phase);
        var candidates = before.Options.Select((o, i) => (Option: o, Index: i)).Where(p => p.Option.Move.Kind != DuelMoveKind.Concede).ToList();
        if (candidates.Count == 0) throw new Exception("Sin jugadas legales en " + before.Phase);
        var choice = candidates.FirstOrDefault(p => p.Option.RequiredDiscards > 0);
        if (choice.Option == null) choice = candidates[0];
        var errors = actor.Errors.Count;
        actor.Connection.Reducers.PlayDuelV2(nextCode, before.Revision, Guid.NewGuid().ToString("N"), choice.Index, Enumerable.Range(0, choice.Option.RequiredDiscards).ToList());
        Until(() =>
        {
            if (actor.Errors.Count > errors) throw new Exception(choice.Option.Label + ": " + actor.Errors.Last());
            return fighterA.Duel?.Revision == before.Revision + 1 && b.Duel?.Revision == before.Revision + 1;
        });
        if (combatSteps % 12 == 0)
        {
            var previous = b.Duel!;
            b.Connection.Disconnect(); clients.Remove(b);
            b = Connect(b.Token);
            Until(() => b.Duel?.Revision == previous.Revision);
            if (b.Duel!.Phase != previous.Phase || b.Duel.Die != previous.Die) throw new Exception("La reconexión alteró una decisión o un dado.");
        }
        if (combatSteps % 20 == 0) Console.WriteLine($"PROGRESS combate: {combatSteps} jugadas, turno {fighterA.Duel.Turn}, fase {fighterA.Duel.Phase}");
    }
    Check(fighterA.Duel.Winner != null && b.Duel!.Winner == fighterA.Duel.Winner, "Duelo completo resuelto por el servidor con ganador consistente");
    Check(fighterA.Duel.Options.Count == 0 && b.Duel!.Options.Count == 0, "La partida terminada no acepta nuevas jugadas");
    Check(decisions.Contains(Phase.AttackRerollChoice) || decisions.Contains(Phase.DieRerollChoice), "La partida remota ejercita decisiones de dado persistidas");
    service.Leave();
    Until(() => service.Room == null && b.Room == null && fighterA.Duel == null);
    Check(service.Duel == null && b.Duel == null, "Cerrar el resultado limpia las vistas de combate y sus registros");
    foreach (var table in new[] { "duel", "duel_step" })
    {
        var forbidden = false;
        outsider.Connection.SubscriptionBuilder().OnError((_, _) => forbidden = true).Subscribe(new[] { "SELECT * FROM " + table });
        Until(() => forbidden);
        Check(forbidden, "El estado inicial y registro privado no se pueden consultar: " + table);
    }
    service.Create("Abandono"); Until(() => service.Room != null);
    var surrenderCode = service.Room!.Code;
    b.Connection.Reducers.JoinRoom(surrenderCode, "Beto");
    Until(() => b.InitialSkills?.Selected == false && service.InitialSkills?.Selected == false);
    b.Connection.Reducers.ChooseInitialSkill(surrenderCode, "magnet-shuffle", Guid.NewGuid().ToString("N")); service.ChooseInitialSkill("magnet-shuffle");
    Until(() => b.Hand.Length == 5 && service.Hand.Length == 5);
    service.Confirm([]); b.Connection.Reducers.ConfirmPreparation(surrenderCode, [], Guid.NewGuid().ToString("N"));
    Until(() => service.Duel != null && b.Duel != null);
    var surrender = b.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede);
    b.Connection.Reducers.PlayDuelV2(surrenderCode, 0, Guid.NewGuid().ToString("N"), surrender, []);
    Until(() => service.Duel?.Winner == 0 && b.Duel?.Winner == 0);
    Check(b.Duel.Options.Count == 0, "Abandonar concede la victoria al rival y termina el duelo");
    service.Leave(); Until(() => service.Room == null && b.Room == null);
    Console.WriteLine("Todas las comprobaciones PvP pasaron.");
    return 0;
}
finally
{
    foreach (var client in clients) if (client.Connection.IsActive) client.Connection.Disconnect();
}

sealed class Client
{
    public DbConnection Connection = null!;
    public bool Ready;
    public string? Fatal;
    public string Token = "";
    public readonly List<string> Errors = [];
    public RoomInfo? Room => Connection.Db.MyRoom.Iter().SingleOrDefault();
    public InitialSkillInfo? InitialSkills => Connection.Db.MyInitialSkill.Iter().SingleOrDefault();
    public PreparationInfo? Preparation => Connection.Db.MyPreparation.Iter().SingleOrDefault();
    public DuelSnapshot? Duel => Connection.Db.MyDuel.Iter().SingleOrDefault() is { } row ? JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelSnapshot) : null;
    public HandCard[] Hand => Connection.Db.MyHand.Iter().OrderBy(c => c.Position).ToArray();
}
