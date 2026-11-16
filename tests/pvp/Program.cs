using Relick.Network.Types;
using Relick.Core;
using System.Text.Json;

if (args.FirstOrDefault() == "--connection-health") return await ConnectionHealthChecks.Run(args);
if (args.FirstOrDefault() == "--quick-match") return await QuickMatchChecks.Run(args);
if (args.FirstOrDefault() == "--decks") return await DeckChecks.Run(args);
if (args.FirstOrDefault() == "--login") return await LoginChecks.Run(args);
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

string HistoryFingerprint(DuelSnapshot duel) => JsonSerializer.Serialize(duel.History);
void VerifyPublicHistory(DuelSnapshot first, DuelSnapshot second)
{
    var entries = first.History;
    if (entries == null || entries.Count != (int)Math.Min(first.Revision, (ulong)DuelHistory.Capacity) ||
        entries[^1].Id != first.Revision || entries.Select(e => e.Id).Distinct().Count() != entries.Count ||
        HistoryFingerprint(first) != HistoryFingerprint(second))
        throw new Exception("El historial público difiere entre jugadores, duplica jugadas o pierde su límite/orden.");
}

int VerifyHandSwap(DuelSnapshot[] before, DuelSnapshot[] after)
{
    var cues = after[0].Visuals;
    if (!cues.Any(c => c.Kind == DuelVisualKind.HandTransfer)) return 0;
    var hands = Enumerable.Range(0, 2).Select(seat => before[seat].Players[seat].Hand.ToList()).ToArray();
    var known = Enumerable.Range(0, 2).Select(seat => before[1 - seat].Players[seat].Hand.ToList()).ToArray();
    foreach (var cue in cues.Where(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.Hand))
    { hands[cue.Player].RemoveAt(cue.Value); known[cue.Player].RemoveAt(cue.Value); }
    foreach (var seat in new[] { 0, 1 })
    {
        if (!after[seat].Players[seat].Hand.SequenceEqual(hands[1 - seat]) ||
            !after[1 - seat].Players[seat].Hand.SequenceEqual(known[1 - seat]))
            throw new Exception("El intercambio remoto pierde cartas o traslada las revelaciones a otra copia.");
        var transfer = cues.SingleOrDefault(c => c.Kind == DuelVisualKind.HandTransfer && c.Player == seat);
        if (hands[seat].Count > 0 && (transfer == null || transfer.Target != 1 - seat || transfer.Value != hands[seat].Count))
            throw new Exception("La señal de intercambio no conserva la cantidad o el destinatario.");
    }
    return 1;
}

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
    Check(a.Hand.Length == 0 && b.Hand.Length == 0 && a.InitialSkills!.Options.ToHashSet().SetEquals(CardCatalog.TrainingDeck().Where(id => CardCatalog.Get(id).Kind == CardKind.Skill)), "Antes de elegir solo se ofrecen las habilidades del propio mazo");
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
    Check(opening.HandCountOne + opening.DeckCountOne + 1 == CardCatalog.TrainingDeck().Count && opening.HandCountTwo + opening.DeckCountTwo + 1 == CardCatalog.TrainingDeck().Count, "Se conservan las cartas del mazo por jugador con Bonus fuera del mazo");
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
    service.Create("   ");
    Check(service.RoomError.Contains("nombre") && service.PendingRoomRequest == Relick.Network.RoomRequest.None && service.Room == null,
        "El lobby explica un nombre inválido sin enviar ni crear sala");
    service.Join("corto", "Cliente Godot");
    Check(service.RoomError.Contains("8") && service.PendingRoomRequest == Relick.Network.RoomRequest.None,
        "El lobby exige un código completo antes de enviar");
    service.Create("Cliente Godot");
    service.Create("Solicitud repetida");
    Check(service.PendingRoomRequest == Relick.Network.RoomRequest.Create, "Crear bloquea solicitudes repetidas hasta recibir respuesta");
    Until(() => service.Room != null);
    var nextCode = service.Room!.Code;
    b.Connection.Reducers.JoinRoom(nextCode, "Beto");
    Until(() => b.InitialSkills?.Selected == false && service.InitialSkills?.Selected == false);
    b.Connection.Reducers.ChooseInitialSkill(nextCode, "magnet-shuffle", Guid.NewGuid().ToString("N")); service.ChooseInitialSkill("destiny-shuffle");
    service.Connect(server, database, serviceToken);
    Until(() => b.Hand.Length == 5 && service.Hand.Length == 5);
    Check(ReferenceEquals(service.Hand,service.Hand), "La mano ordenada se reutiliza mientras no cambian las filas");
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
    // Preferences remain private and do not invalidate the opponent's current options.
    var rivalBeforeCombo = JsonSerializer.Serialize(b.Duel!, DuelJson.Default.DuelSnapshot);
    var beforeComboSnapshot = service.Duel!;
    var ownActionsBeforeCombo = service.Duel!.Players[service.Duel.MySeat].Actions;
    service.SetComboResponses(false, nextCode);
    Until(() => !service.PlayPending && service.Duel?.AutoPassCombos == true && fighterA.Duel?.AutoPassCombos == true);
    Check(service.Duel!.Revision == 0 && b.Duel!.Revision == 0 &&
        service.Duel.Players[service.Duel.MySeat].Actions == ownActionsBeforeCombo, "Toggle COMBO no consume acciones ni avanza la revisión pública");
    Check(JsonSerializer.Serialize(b.Duel!, DuelJson.Default.DuelSnapshot) == rivalBeforeCombo,
        "El rival no recibe cambios de preferencia, opciones, dados ni animaciones");
    service.Play(0, expectedRevision: beforeComboSnapshot.Revision, expectedSnapshot: beforeComboSnapshot);
    Check(!service.PlayPending && service.Message.Contains("La partida cambió") && service.Duel!.Revision == 0,
        "Una vista anterior al toggle no puede enviar índices de opciones obsoletos con la misma revisión pública");
    service.Connect(server, database, serviceToken);
    Until(() => service.Ready && service.Duel?.AutoPassCombos == true);
    Check(true, "Reconectar conserva el modo privado de respuestas COMBO");
    service.SetComboResponses(true, nextCode);
    Until(() => !service.PlayPending && service.Duel?.AutoPassCombos == false && fighterA.Duel?.AutoPassCombos == false);
    var comboCommand = Guid.NewGuid().ToString("N");
    fighterA.Connection.Reducers.SetComboResponses(nextCode, comboCommand, false);
    Until(() => fighterA.Duel?.AutoPassCombos == true);
    fighterA.Connection.Reducers.SetComboResponses(nextCode, Guid.NewGuid().ToString("N"), true);
    Until(() => fighterA.Duel?.AutoPassCombos == false);
    fighterA.Connection.Reducers.SetComboResponses(nextCode, comboCommand, false);
    Until(() => fighterA.Connection.Db.MyMoveResult.Iter().SingleOrDefault()?.CommandId == comboCommand);
    Check(!fighterA.Duel!.AutoPassCombos && fighterA.Duel.Revision == 0, "Reintentar una preferencia anterior no revierte la más reciente");
    Rejected(fighterA, () => fighterA.Connection.Reducers.SetComboResponses(nextCode, comboCommand, true), "Un identificador de preferencia no admite otro valor");
    Rejected(outsider, () => outsider.Connection.Reducers.SetComboResponses(nextCode, Guid.NewGuid().ToString("N"), false), "Un tercero no puede cambiar respuestas COMBO ajenas");
    Rejected(fighterA, () => fighterA.Connection.Reducers.SetComboResponses(code, Guid.NewGuid().ToString("N"), false), "Una preferencia atrasada no se aplica a otra sala");
    Rejected(fighterA, () => fighterA.Connection.Reducers.SetComboResponses(nextCode, "inválido", false), "Una preferencia con identificador inválido se rechaza");
    Check(!fighterA.Duel!.AutoPassCombos && !b.Duel!.AutoPassCombos, "Las preferencias rechazadas no modifican a ninguno de los jugadores");
    Check(fighterA.Duel.History == null && b.Duel.History == null,
        "Preferencias privadas y comandos rechazados no añaden entradas al historial público");

    var starter = fighterA.Duel.CurrentPlayer == 0 ? fighterA : b;
    var learn = starter.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Learn);
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), int.MaxValue, []), "Opción inventada rechazada por el servidor");
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, Guid.NewGuid().ToString("N"), learn, [0]), "Descartes adicionales no pueden modificar una jugada normal");
    var learnId = Guid.NewGuid().ToString("N");
    starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn, []);
    Until(() => fighterA.Duel?.Revision == 1 && b.Duel?.Revision == 1);
    Check(starter.Duel!.Players[starter.Duel.MySeat].Skills.Length == 1, "Enseñar una habilidad actualiza ambos tableros");
    VerifyPublicHistory(fighterA.Duel!, b.Duel!);
    Check(starter.Duel.History!.Single().CardId == starter.Duel.Players[starter.Duel.MySeat].Skills[0],
        "La jugada aceptada añade una sola carta anunciada al historial compartido");
    var acceptedHistory = HistoryFingerprint(starter.Duel);
    Check(starter.Duel.Turn == 1 && starter.Duel.Options.All(o => o.Move.Kind != DuelMoveKind.Attack),
        "El servidor permite enseñar pero no ofrece ataques a quien inicia en el turno uno");
    Check(fighterA.Hand.Length == 0 && b.Hand.Length == 0, "Las manos iniciales dejan de usarse cuando el motor de combate toma el control");
    starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn, []);
    Rejected(starter, () => starter.Connection.Reducers.PlayDuelV2(nextCode, 0, learnId, learn + 1, []), "Un identificador reutilizado con otra jugada se rechaza");
    Check(starter.Duel.Revision == 1 && starter.Duel.Players[starter.Duel.MySeat].Skills.Length == 1, "Reenviar una jugada confirmada no la ejecuta dos veces");
    Check(HistoryFingerprint(starter.Duel) == acceptedHistory,
        "Reintentos y rechazos conservan los mismos identificadores y resultados públicos");
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
        Check(turnService.Duel.History?.Count == 2 && turnService.Duel.History[^1].Id == 2,
            "Reconectar durante el envío recupera el historial sin repetir entradas");
        services.Remove(turnService);
    }
    Until(() => fighterA.Duel?.Revision == 2 && b.Duel?.Revision == 2);
    var combatSteps = 0;
    var verifiedImpacts = 0;
    var verifiedSearches = 0;
    var verifiedSwaps = 0;
    var decisions = new HashSet<Phase>();
    while (fighterA.Duel!.Winner == null && combatSteps++ < 600)
    {
        var actor = fighterA.Duel.DecisionPlayer == 0 ? fighterA : b;
        var before = actor.Duel!; decisions.Add(before.Phase);
        var beforeViews = new[] { fighterA.Duel!, b.Duel! };
        var candidates = before.Options.Select((o, i) => (Option: o, Index: i)).Where(p => p.Option.Move.Kind != DuelMoveKind.Concede).ToList();
        if (candidates.Count == 0) throw new Exception("Sin jugadas legales en " + before.Phase);
        var choice = candidates.FirstOrDefault(p => p.Option.RequiredDiscards > 0);
        if (choice.Option == null) choice = candidates[0];
        var errors = actor.Errors.Count;
        var selectionStart = choice.Option.Move.Kind == DuelMoveKind.Recycle
            ? before.Players[before.MySeat].Memory.Length - choice.Option.RequiredDiscards : 0;
        actor.Connection.Reducers.PlayDuelV2(nextCode, before.Revision, Guid.NewGuid().ToString("N"), choice.Index, SelectedCards.For(before, choice.Option, selectionStart).ToList());
        Until(() =>
        {
            if (actor.Errors.Count > errors) throw new Exception(choice.Option.Label + ": " + actor.Errors.Last());
            return fighterA.Duel?.Revision == before.Revision + 1 && b.Duel?.Revision == before.Revision + 1;
        });
        VerifyPublicHistory(fighterA.Duel!, b.Duel!);
        if (IsDeckSearch(before, choice.Option))
        {
            var other = actor == fighterA ? b : fighterA;
            if (actor.Duel!.Players[before.MySeat].Hand.Last() != choice.Option.CardId || other.Duel!.Players[before.MySeat].Hand.Last() != null ||
                before.Options.Where(o => IsDeckSearch(before, o)).Any(o => o.CardId == null || CardCatalog.Get(o.CardId).Kind == CardKind.Skill))
                throw new Exception("La búsqueda remota cambia el objetivo o revela la carta al rival.");
            verifiedSearches++;
        }
        verifiedSwaps += VerifyHandSwap(beforeViews, new[] { fighterA.Duel!, b.Duel! });
        if (before.Phase is Phase.Combat or Phase.AttackRerollChoice or Phase.DefenseChoice)
        // A combat-phase command can also cause effect/exhaustion damage, whose attack die is 0.
        foreach (var hit in fighterA.Duel!.Visuals.Where(v => v.Kind == DuelVisualKind.Hit && v.Player != before.CurrentPlayer && v.AttackDie != 0))
        {
            // Stacked Totems can turn a 1, 2 or 3 into a successful normal attack.
            if (hit.AttackDie is < 1 or > 6) throw new Exception($"El servidor no transporta el resultado final del impacto: {hit.AttackDie}, {before.Phase}.");
            verifiedImpacts++;
        }
        if (combatSteps % 12 == 0)
        {
            var previous = b.Duel!;
            b.Connection.Disconnect(); clients.Remove(b);
            b = Connect(b.Token);
            Until(() => b.Duel?.Revision == previous.Revision);
            if (b.Duel!.Phase != previous.Phase || b.Duel.Die != previous.Die) throw new Exception("La reconexión alteró una decisión o un dado.");
            if (HistoryFingerprint(b.Duel) != HistoryFingerprint(previous)) throw new Exception("La reconexión alteró el historial público.");
        }
        if (combatSteps % 20 == 0) Console.WriteLine($"PROGRESS combate: {combatSteps} jugadas, turno {fighterA.Duel.Turn}, fase {fighterA.Duel.Phase}");
    }
    Check(fighterA.Duel.Winner != null && b.Duel!.Winner == fighterA.Duel.Winner, "Duelo completo resuelto por el servidor con ganador consistente");
    Check(!string.IsNullOrEmpty(fighterA.Duel.EndReason) && fighterA.Duel.EndReason == b.Duel!.EndReason,
        "Ambos jugadores reciben la misma causa pública del resultado");
    Check(fighterA.Duel.History!.Count == DuelHistory.Capacity,
        "El duelo completo mantiene solo las últimas veinte jugadas públicas en ambos clientes");
    Console.WriteLine($"IMPACT_TRANSPORT · {verifiedImpacts} impactos con resultado final verificado.");
    Check(verifiedImpacts > 0, "El duelo remoto transporta dados finales de impactos de ataque");
    Check(fighterA.Duel.Options.Count == 0 && b.Duel!.Options.Count == 0, "La partida terminada no acepta nuevas jugadas");
    Check(decisions.Contains(Phase.AttackRerollChoice) || decisions.Contains(Phase.DieRerollChoice), "La partida remota ejercita decisiones de dado persistidas");
    service.Leave();
    Until(() => service.Room == null && b.Room == null && fighterA.Duel == null);
    Check(service.LastResult?.Winner != null && !string.IsNullOrEmpty(service.LastResult.EndReason),
        "Cerrar la sala conserva el resultado autorizado para terminar de leerlo");
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
    using var bufferedResult = new Relick.Network.LobbyConnection();
    bufferedResult.Connect(server, database, b.Token); services.Add(bufferedResult);
    Until(() => bufferedResult.Ready && bufferedResult.Duel != null); services.Remove(bufferedResult);
    var surrender = b.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede);
    b.Connection.Reducers.PlayDuelV2(surrenderCode, 0, Guid.NewGuid().ToString("N"), surrender, []);
    Until(() => service.Duel?.Winner == 0 && b.Duel?.Winner == 0);
    Check(b.Duel.Options.Count == 0, "Abandonar concede la victoria al rival y termina el duelo");
    service.Leave(); Until(() => service.Room == null && b.Room == null);
    services.Add(bufferedResult);
    Until(() => bufferedResult.Room == null && bufferedResult.LastResult?.Winner == 0);
    Check(bufferedResult.LastResult!.EndReason?.Contains("abandona el duelo") == true,
        "Recibir victoria y borrado en el mismo procesamiento conserva la causa del resultado");
    services.Remove(bufferedResult); bufferedResult.Dispose();
    service.Create("Agotamiento"); Until(() => service.Room != null);
    var recycleCode = service.Room!.Code;
    b.Connection.Reducers.JoinRoom(recycleCode, "Memoria");
    Until(() => b.InitialSkills?.Selected == false && service.InitialSkills?.Selected == false);
    b.Connection.Reducers.ChooseInitialSkill(recycleCode, "magnet-shuffle", Guid.NewGuid().ToString("N")); service.ChooseInitialSkill("magnet-shuffle");
    Until(() => b.Hand.Length == 5 && service.Hand.Length == 5);
    service.Confirm([]); b.Connection.Reducers.ConfirmPreparation(recycleCode, [], Guid.NewGuid().ToString("N"));
    Until(() => fighterA.Duel != null && b.Duel != null);
    var recyclingChoices = 0; var passiveSteps = 0;
    // The larger development deck needs more commands. Respect the production
    // budget shared with the preceding duel instead of increasing server limits.
    Thread.Sleep(TimeSpan.FromSeconds(10));
    while (fighterA.Duel!.Winner == null && passiveSteps++ < CardCatalog.TrainingDeck().Count * 12)
    {
        Thread.Sleep(100);
        var actor = fighterA.Duel.DecisionPlayer == 0 ? fighterA : b;
        var before = actor.Duel!;
        var beforeViews = new[] { fighterA.Duel!, b.Duel! };
        var options = before.Options.Select((o, i) => (Option: o, Index: i)).Where(p => p.Option.Move.Kind != DuelMoveKind.Concede).ToArray();
        var choice = options.FirstOrDefault(o => o.Option.Move.Kind == DuelMoveKind.Recycle);
        var isRecycling = choice.Option != null;
        if (isRecycling && recyclingChoices == 0)
        {
            actor.Connection.Disconnect(); var resumed = Connect(actor.Token);
            if (actor == fighterA) fighterA = resumed; else b = resumed;
            actor = resumed; Until(() => actor.Duel?.Revision == before.Revision);
            Check(actor.Duel!.Phase == Phase.RecycleChoice && actor.Duel.Players[before.MySeat].Memory.SequenceEqual(before.Players[before.MySeat].Memory),
                "Reconectar conserva el dado y la memoria durante la elección de reciclaje");
            Rejected(actor, () => actor.Connection.Reducers.PlayDuelV2(recycleCode, before.Revision, Guid.NewGuid().ToString("N"), choice.Index, []),
                "El servidor exige la cantidad exacta de cartas para reciclar");
        }
        if (!isRecycling)
        {
            choice = before.Phase == Phase.ErrorResponse ? options.First(o => o.Option.Move.Kind == DuelMoveKind.PassError) :
                before.Phase == Phase.DeckSearchChoice ? options.First(o => IsDeckSearch(before, o.Option)) :
                before.Decision?.Kind == DecisionKind.ChooseCard ? options.First(o => o.Option.Move.Kind == DuelMoveKind.Choose) :
                before.Phase == Phase.DieRerollChoice ? options.First(o => o.Option.Move.Kind == DuelMoveKind.EffectDie) :
                options.FirstOrDefault(o => o.Option.Move.Kind == DuelMoveKind.PlayCode &&
                    (before.Players[before.MySeat].Hand[o.Option.Move.Index] == CardCatalog.KonamyCode.Id ||
                     before.Players[before.MySeat].Hand[o.Option.Move.Index] == CardCatalog.MindSwap.Id));
            if (choice.Option == null && (before.Players[before.MySeat].Memory.Length < 6 || before.Players[before.MySeat].Bonus == CardCatalog.KonamyCode.Id))
                choice = options.FirstOrDefault(o => o.Option.Move.Kind == DuelMoveKind.Exchange);
            if (choice.Option == null) choice = options.FirstOrDefault(o => o.Option.Move.Kind == DuelMoveKind.Advance);
            if (choice.Option == null) choice = options[0]; // Complete mandatory discard/target decisions before advancing.
        }
        var selected = SelectedCards.For(before, choice.Option!, isRecycling ?
            before.Players[before.MySeat].Memory.Length - choice.Option!.RequiredDiscards : 0).ToList();
        var errors = actor.Errors.Count;
        actor.Connection.Reducers.PlayDuelV2(recycleCode, before.Revision, Guid.NewGuid().ToString("N"), choice.Index, selected);
        Until(() =>
        {
            if (actor.Errors.Count > errors) throw new Exception(actor.Errors.Last());
            return fighterA.Duel?.Revision == before.Revision + 1 && b.Duel?.Revision == before.Revision + 1;
        });
        if (isRecycling)
        {
            recyclingChoices++;
            var remaining = before.Players[before.MySeat].Memory.Where((_, i) => !selected.Contains(i));
            if (!actor.Duel!.Players[before.MySeat].Memory.SequenceEqual(remaining)) throw new Exception("El servidor recicló cartas que no fueron elegidas.");
            Console.WriteLine($"RECYCLE_TRANSPORT · D6 {before.Die}, {selected.Count} cartas elegidas y memoria restante verificada.");
        }
        if (IsDeckSearch(before, choice.Option!))
        {
            var other = actor == fighterA ? b : fighterA;
            if (actor.Duel!.Players[before.MySeat].Hand.Last() != choice.Option.CardId || other.Duel!.Players[before.MySeat].Hand.Last() != null ||
                other.Duel.Options.Any(o => o.Move.Kind is DuelMoveKind.SearchDeck or DuelMoveKind.Choose || o.CardId != null))
                throw new Exception("La búsqueda revela opciones o la carta añadida a otro jugador.");
            verifiedSearches++;
        }
        verifiedSwaps += VerifyHandSwap(beforeViews, new[] { fighterA.Duel!, b.Duel! });
    }
    Check(recyclingChoices > 0, "El duelo remoto ejercita la selección manual de memoria");
    var resultView = fighterA.Duel!;
    Check(resultView.Winner != null && resultView.Players.Select(p => p.Health).Order().SequenceEqual(new[] { 6, 8 }) &&
        resultView.Players[resultView.Winner.Value].Health == 8 && b.Duel!.Winner == resultView.Winner,
        "El segundo agotamiento resta dos vidas y gana quien conserva más vida");
    service.Leave(); Until(() => service.Room == null && b.Room == null && fighterA.Duel == null);
    Console.WriteLine($"SEARCH_TRANSPORT · {verifiedSearches} búsquedas privadas verificadas.");
    Console.WriteLine($"HAND_SWAP_TRANSPORT · {verifiedSwaps} intercambios con manos, destinatarios y revelaciones verificados.");
    Console.WriteLine("Todas las comprobaciones PvP pasaron.");
    return 0;
}
finally
{
    foreach (var client in clients) if (client.Connection.IsActive) client.Connection.Disconnect();
}

// A deck search answered through the generic card decision, or the earlier search command.
static bool IsDeckSearch(DuelSnapshot snapshot, DuelOption option) => option.Move.Kind == DuelMoveKind.SearchDeck ||
    // Other galleries (Disquete, Resetear la Mente) pick from memories or a hand, not from the deck.
    option.Move.Kind == DuelMoveKind.Choose && snapshot.Decision is { Kind: DecisionKind.ChooseCard } decision && decision.Source == CardCatalog.KonamyCode.Id;

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
