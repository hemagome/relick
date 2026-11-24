using System.Diagnostics;
using System.Text.Json;
using Relick.Core;
using Relick.Network;
using T = Relick.TestNetwork.Types;

static class ScalabilityChecks
{
    public static async Task<int> Run(string[] args)
    {
        var mode = args[0]; var server = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:3100";
        var database = args.ElementAtOrDefault(2) ?? "relick-dev";
        try
        {
            if (mode == "--load") await Load(server, database, args.ElementAtOrDefault(3) ?? "artifacts/scalability-load.json");
            else if (mode == "--expiry-test") await Expiry(server, database);
            else if (mode == "--rate-test") await Rate(server, database);
            else if (mode == "--restart-prepare") await Restart(server, database, args.ElementAtOrDefault(3) ?? "/tmp/relick-restart-check.json", false);
            else if (mode == "--restart-verify") await Restart(server, database, args.ElementAtOrDefault(3) ?? "/tmp/relick-restart-check.json", true);
            else throw new Exception("Modo desconocido: " + mode);
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    sealed class Harness : IDisposable
    {
        public readonly List<LobbyConnection> Services = [];
        public readonly List<RawClient> Raw = [];
        public async Task Wait(Func<bool> condition, int seconds = 30)
        {
            var deadline = Stopwatch.StartNew();
            while (true)
            {
                foreach (var client in Services) client.Tick();
                foreach (var client in Raw) { client.Connection.FrameTick(); if (client.Fatal != null) throw new Exception(client.Fatal); }
                if (condition()) return;
                if (deadline.Elapsed.TotalSeconds > seconds) throw new Exception("Tiempo agotado: " + string.Join(" / ", Services.Take(4).Select(c => c.Message)));
                await Task.Delay(1);
            }
        }
        public LobbyConnection Service(string server, string database, string? token = null)
        {
            var client = new LobbyConnection(); Services.Add(client); client.Connect(server, database, token); return client;
        }
        public RawClient Client(string server, string database, bool stats = false)
        {
            var client = new RawClient();
            client.Connection = T.DbConnection.Builder().WithUri(server).WithDatabaseName(database)
                .OnConnect((connection, _, _) => connection.SubscriptionBuilder().OnApplied(_ => client.Ready = true)
                    .OnError((_, error) => client.Fatal = error.Message)
                    .Subscribe(stats ? ["SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_initial_skill", "SELECT * FROM my_duel", "SELECT * FROM my_move_result", "SELECT * FROM my_fixture_stats"] :
                        ["SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_initial_skill", "SELECT * FROM my_duel", "SELECT * FROM my_move_result"]))
                .OnConnectError(error => client.Fatal = error.Message).Build();
            client.Connection.OnUnhandledReducerError += (_, error) => client.Errors.Add(error.Message);
            Raw.Add(client); return client;
        }
        public void Dispose()
        {
            foreach (var service in Services) service.Dispose();
            foreach (var client in Raw) if (client.Connection.IsActive) client.Connection.Disconnect();
        }
    }
    sealed class RawClient
    {
        public T.DbConnection Connection = null!;
        public bool Ready;
        public string? Fatal;
        public List<string> Errors = [];
        public T.RoomInfo? Room => Connection.Db.MyRoom.Iter().SingleOrDefault();
        public T.MoveResult? Result => Connection.Db.MyMoveResult.Iter().SingleOrDefault();
        public DuelSnapshot? Duel => Connection.Db.MyDuel.Iter().SingleOrDefault() is { } row ? JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelSnapshot) : null;
    }
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    static async Task<string> ReadyPair(Harness h, RawClient a, RawClient b)
    {
        await h.Wait(() => a.Ready && b.Ready);
        a.Connection.Reducers.CreateRoom("A"); await h.Wait(() => a.Room != null);
        var code = a.Room!.Code; b.Connection.Reducers.JoinRoom(code, "B");
        await h.Wait(() => a.Room?.Dealt == true && b.Room?.Dealt == true);
        a.Connection.Reducers.ChooseInitialSkill(code, "magnet-shuffle", Guid.NewGuid().ToString("N"));
        b.Connection.Reducers.ChooseInitialSkill(code, "magnet-shuffle", Guid.NewGuid().ToString("N"));
        await h.Wait(() => a.Connection.Db.MyHand.Iter().Count() == 5 && b.Connection.Db.MyHand.Iter().Count() == 5);
        a.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N"));
        b.Connection.Reducers.ConfirmPreparation(code, [], Guid.NewGuid().ToString("N"));
        await h.Wait(() => a.Duel != null && b.Duel != null); return code;
    }
    static async Task Expiry(string server, string database)
    {
        using var h = new Harness(); var a = h.Client(server, database); var b = h.Client(server, database);
        await h.Wait(() => a.Ready && b.Ready);
        a.Connection.Reducers.CreateRoom("Expiración"); await h.Wait(() => a.Room != null);
        var code = a.Room!.Code; var errors = a.Errors.Count;
        a.Connection.Reducers.AttemptExpiryForTest();
        await h.Wait(() => a.Errors.Count > errors);
        Check(a.Room != null, "Un cliente no puede invocar la expiración programada");
        await Task.Delay(1100);
        b.Connection.Reducers.JoinRoom(code, "B"); await h.Wait(() => b.Room != null);
        await Task.Delay(1100); await h.Wait(() => true);
        Check(a.Room != null && b.Room != null, "La actividad renueva el vencimiento sin que lo borre el temporizador anterior");
        await h.Wait(() => a.Room == null && b.Room == null, 8);
        Check(!a.Connection.Db.MyHand.Iter().Any() && !b.Connection.Db.MyHand.Iter().Any(), "El planificador limpia sala, membresías y cartas al vencer");
    }
    static async Task Rate(string server, string database)
    {
        using var h = new Harness(); var a = h.Client(server, database); var b = h.Client(server, database);
        var code = await ReadyPair(h, a, b); var last = "";
        for (var i = 0; i < 150; i++) { last = Guid.NewGuid().ToString("N"); a.Connection.Reducers.PlayDuelV2(code, 0, last, int.MaxValue, []); }
        await h.Wait(() => a.Result?.CommandId == last);
        Check(a.Result!.Error.Contains("Demasiadas") && !a.Result.Accepted && a.Duel!.Revision == 0,
            "Las jugadas inválidas consumen el límite y no modifican la partida");
        var nonce = Guid.NewGuid().ToString("N"); var concede = a.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede);
        a.Connection.Reducers.PlayDuelV2(code, 0, nonce, concede, []); await h.Wait(() => a.Result?.CommandId == nonce);
        Check(!a.Result!.Accepted && a.Result.Error.Contains("Demasiadas"), "El límite se conserva entre transacciones rechazadas");
        a.Connection.Reducers.LeaveRoom(); await h.Wait(() => a.Room == null && b.Room == null);
    }
    sealed class RestartData
    {
        public string TokenA { get; set; } = "";
        public string TokenB { get; set; } = "";
        public string StateA { get; set; } = "";
        public string StateB { get; set; } = "";
    }
    static async Task Restart(string server, string database, string path, bool verify)
    {
        using var h = new Harness();
        var saved = verify ? JsonSerializer.Deserialize<RestartData>(File.ReadAllText(path))! : new RestartData();
        var a = h.Service(server, database, verify ? saved.TokenA : null);
        var b = h.Service(server, database, verify ? saved.TokenB : null);
        a.TokenReceived += token => saved.TokenA = token; b.TokenReceived += token => saved.TokenB = token;
        await h.Wait(() => a.Ready && b.Ready);
        if (verify)
        {
            await h.Wait(() => a.Duel != null && b.Duel != null);
            Check(JsonSerializer.Serialize(a.Duel!, DuelJson.Default.DuelSnapshot) == saved.StateA &&
                JsonSerializer.Serialize(b.Duel!, DuelJson.Default.DuelSnapshot) == saved.StateB,
                "Reiniciar el servidor conserva exactamente ambos tableros y la decisión pendiente");
        }
        else
        {
            a.Create("Reinicio A"); await h.Wait(() => a.Room != null);
            b.Join(a.Room!.Code, "Reinicio B");
            await h.Wait(() => a.InitialSkills?.Selected == false && b.InitialSkills?.Selected == false);
            a.ChooseInitialSkill("magnet-shuffle"); b.ChooseInitialSkill("magnet-shuffle");
            await h.Wait(() => a.Hand.Length == 5 && b.Hand.Length == 5);
            a.Confirm([]); b.Confirm([]); await h.Wait(() => a.Duel != null && b.Duel != null);
        }
        for (var i = 0; i < (verify ? 1 : 50); i++)
        {
            if (!verify && a.Duel!.Phase is Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.ErrorResponse) break;
            var actor = a.Duel!.DecisionPlayer == a.Duel.MySeat ? a : b;
            var before = actor.Duel!;
            var choice = before.Options.Select((o,index)=>(o,index)).First(p => p.o.Move.Kind != DuelMoveKind.Concede);
            actor.Play(choice.index, SelectedCards.For(before, choice.o));
            await h.Wait(() => !actor.PlayPending && a.Duel?.Revision == before.Revision + 1 && b.Duel?.Revision == before.Revision + 1);
            Check(actor.LastPlayAccepted, verify ? "El efecto pendiente continúa correctamente después del reinicio" : "Jugada de preparación del reinicio aceptada");
        }
        if (verify)
        {
            if (a.Duel!.Winner == null)
            {
                a.Play(a.Duel.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede));
                await h.Wait(() => !a.PlayPending && a.Duel?.Winner != null);
            }
            a.Leave(); await h.Wait(() => a.Room == null && b.Room == null);
            File.Delete(path);
        }
        else
        {
            Check(a.Duel!.Phase is Phase.AttackRerollChoice or Phase.DieRerollChoice or Phase.ErrorResponse, "La partida se pausa en una decisión real para verificar el reinicio");
            saved.StateA = JsonSerializer.Serialize(a.Duel!, DuelJson.Default.DuelSnapshot); saved.StateB = JsonSerializer.Serialize(b.Duel!, DuelJson.Default.DuelSnapshot);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var writer = new StreamWriter(new FileStream(path, options)); writer.Write(JsonSerializer.Serialize(saved));
            Console.WriteLine("RESTART_READY · archivo privado creado; los tokens no se muestran.");
        }
    }

    sealed class LoadPair
    {
        public LobbyConnection A = null!, B = null!;
        public int Steps;
        public ulong PendingRevision;
        public LobbyConnection? Pending;
    }
    sealed record LoadResult(int Pairs, int Clients, int Actions, double Seconds, double ActionsPerSecond, double P50Ms, double P95Ms, double P99Ms, long JsonBytes, long FullStateEquivalentBytes);
    static double Percentile(List<double> values, double p) => values.Order().ElementAt(Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * p) - 1));
    static async Task Load(string server, string database, string output)
    {
        var results = new List<LoadResult>();
        foreach (var count in new[] { 1, 10, 25, 50 })
        {
            using var h = new Harness(); var pairs = new List<LoadPair>();
            for (var i = 0; i < count; i++) pairs.Add(new LoadPair { A = h.Service(server, database), B = h.Service(server, database) });
            await h.Wait(() => h.Services.All(c => c.Ready), 90);
            foreach (var pair in pairs) pair.A.Create("Carga A");
            await h.Wait(() => pairs.All(p => p.A.Room != null), 90);
            foreach (var pair in pairs) pair.B.Join(pair.A.Room!.Code, "Carga B");
            await h.Wait(() => pairs.All(p => p.A.InitialSkills?.Selected == false && p.B.InitialSkills?.Selected == false), 90);
            foreach (var pair in pairs) { pair.A.ChooseInitialSkill("magnet-shuffle"); pair.B.ChooseInitialSkill("magnet-shuffle"); }
            await h.Wait(() => pairs.All(p => p.A.Hand.Length == 5 && p.B.Hand.Length == 5), 90);
            foreach (var pair in pairs) { pair.A.Confirm([]); pair.B.Confirm([]); }
            await h.Wait(() => pairs.All(p => p.A.Duel != null && p.B.Duel != null && !p.A.ConfirmationPending && !p.B.ConfirmationPending), 90);
            var initialBytes = h.Services.Sum(c => c.GamePayloadBytes);
            var latencies = new List<double>(); long equivalent = 0;
            var clock = Stopwatch.StartNew();
            await h.Wait(() =>
            {
                foreach (var pair in pairs)
                {
                    if (pair.Pending != null)
                    {
                        if (!pair.Pending.PlayPending && !pair.Pending.LastPlayAccepted) throw new Exception(pair.Pending.Message);
                        if (pair.Pending.PlayPending || pair.A.Duel?.Revision != pair.PendingRevision || pair.B.Duel?.Revision != pair.PendingRevision) continue;
                        latencies.Add(pair.Pending.LastPlayLatencyMs); pair.Steps++;
                        equivalent += System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(pair.A.Duel, DuelJson.Default.DuelSnapshot));
                        equivalent += System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(pair.B.Duel, DuelJson.Default.DuelSnapshot));
                        pair.Pending = null;
                    }
                    if (pair.Steps >= 50 || pair.A.Duel!.Winner != null) continue;
                    var actor = pair.A.Duel.DecisionPlayer == 0 ? pair.A : pair.B;
                    var snapshot = actor.Duel!;
                    var choice = snapshot.Options.Select((o,i)=>(o,i)).First(p => p.o.Move.Kind != DuelMoveKind.Concede);
                    actor.Play(choice.i, SelectedCards.For(snapshot, choice.o), snapshot.Revision);
                    pair.Pending = actor; pair.PendingRevision = snapshot.Revision + 1;
                }
                return pairs.All(p => p.Pending == null && (p.Steps >= 50 || p.A.Duel!.Winner != null));
            }, 180);
            clock.Stop();
            var actions = pairs.Sum(p => p.Steps);
            results.Add(new LoadResult(count, count * 2, actions, clock.Elapsed.TotalSeconds, actions / clock.Elapsed.TotalSeconds,
                Percentile(latencies, .5), Percentile(latencies, .95), Percentile(latencies, .99), h.Services.Sum(c => c.GamePayloadBytes) - initialBytes, equivalent));
            foreach (var pair in pairs.Where(p => p.A.Duel!.Winner == null)) pair.A.Play(pair.A.Duel!.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede));
            await h.Wait(() => pairs.All(p => p.A.Duel!.Winner != null && !p.A.PlayPending), 60);
            foreach (var pair in pairs) pair.A.Leave();
            await h.Wait(() => pairs.All(p => p.A.Room == null && p.B.Room == null), 60);
            Console.WriteLine($"LOAD {count} partidas / {count * 2} clientes: {actions} jugadas, {results[^1].ActionsPerSecond:F1}/s, p95 {results[^1].P95Ms:F1} ms");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new { TimestampUtc = DateTimeOffset.UtcNow, Server = server, Database = database,
            ProcessorCount = Environment.ProcessorCount, Runtime = Environment.Version.ToString(), Results = results,
            Notes = "Oleadas de partidas con hasta 50 jugadas cada una. Latencia de confirmación observada por el cliente. JsonBytes cuenta contenido de filas recibidas, no bytes de transporte. No equivale a capacidad máxima ni a capacidad de Maincloud." }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("LOAD_REPORT " + Path.GetFullPath(output));
    }
}
