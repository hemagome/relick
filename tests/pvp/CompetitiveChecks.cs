using System.Diagnostics;
using System.Text.Json;
using Relick.Core;
using Relick.Network.Types;
using SpacetimeDB;

// The rated modes against a local test module. Administration goes through the fixture reducers with the CLI
// (RELICK_SPACETIME_CLI, RELICK_SPACETIME_ROOT), because the published module only obeys the database owner.
static class CompetitiveChecks
{
    public static async Task<int> Run(string[] args)
    {
        // --competitive uses tests/server/dev; --competitive-time uses tests/server/competitive, with six-second limits.
        var timed = args[0] == "--competitive-time";
        var server = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:3100";
        var database = args.ElementAtOrDefault(2) ?? "relick-dev";
        if (!new Uri(server).IsLoopback)
        {
            Console.Error.WriteLine("Esta batería solo corre contra un servidor local.");
            return 1;
        }
        var peers = new List<Peer>();
        var checks = 0;
        void Check(bool ok, string reason)
        {
            if (!ok) throw new Exception(reason);
            checks++; Console.WriteLine("PASS " + reason);
        }
        void Pump()
        {
            foreach (var peer in peers.Where(p => p.Connection.IsActive)) peer.Connection.FrameTick();
        }
        async Task Until(Func<bool> done, string waiting, double seconds = 25)
        {
            var timer = Stopwatch.StartNew();
            var renewed = 0.0;
            while (true)
            {
                Pump();
                if (peers.Any(p => p.Fatal != null)) throw new Exception(peers.First(p => p.Fatal != null).Fatal);
                if (done()) return;
                if (timer.Elapsed.TotalSeconds > seconds) throw new Exception("Tiempo agotado esperando " + waiting);
                if (timer.Elapsed.TotalSeconds - renewed >= 4)
                {
                    renewed = timer.Elapsed.TotalSeconds;
                    foreach (var peer in peers.Where(p => p.Connection.IsActive && p.Search != null))
                        peer.Connection.Reducers.KeepRankedMatchAlive(peer.Search!.SearchId);
                }
                await Task.Delay(5);
            }
        }
        async Task Pause(double seconds)
        {
            var timer = Stopwatch.StartNew();
            await Until(() => timer.Elapsed.TotalSeconds >= seconds, "una pausa", seconds + 5);
        }
        async Task Admin(string reducer, params string[] values)
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("RELICK_SPACETIME_CLI") ?? "spacetime")
                { RedirectStandardError = true, RedirectStandardOutput = true };
            if (Environment.GetEnvironmentVariable("RELICK_SPACETIME_ROOT") is { } root) { start.ArgumentList.Add("--root-dir"); start.ArgumentList.Add(root); }
            foreach (var value in new[] { "call", "--server", server, "--no-config", "--yes", database, reducer }.Concat(values)) start.ArgumentList.Add(value);
            using var process = Process.Start(start)!;
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEndAsync();
            while (!process.HasExited) { Pump(); await Task.Delay(20); }
            if (process.ExitCode != 0) throw new Exception($"{reducer}: {await error} {await output}");
        }
        async Task<Peer> Connect(string name)
        {
            var peer = new Peer(); peers.Add(peer);
            peer.Connection = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
                .OnConnect((connection, identity, _) =>
                {
                    peer.Identity = identity;
                    connection.SubscriptionBuilder().OnApplied(_ => peer.Ready = true).OnError((_, e) => peer.Fatal = e.Message)
                        .Subscribe(["SELECT * FROM my_account", "SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_duel",
                            "SELECT * FROM my_profile", "SELECT * FROM my_decks", "SELECT * FROM my_quick_search", "SELECT * FROM current_season",
                            "SELECT * FROM my_standings", "SELECT * FROM my_rated_result", "SELECT * FROM my_ranked_search", "SELECT * FROM my_room_mode",
                            "SELECT * FROM my_unlocks", "SELECT * FROM my_emblems", "SELECT * FROM my_room_standings", "SELECT * FROM current_tournament",
                            "SELECT * FROM tournament_entrants", "SELECT * FROM tournament_bracket", "SELECT * FROM my_tournament"]);
                })
                .OnConnectError(e => peer.Fatal = e.Message).Build();
            void Reply(ReducerEventContext ctx)
            {
                // Scheduler and admin calls never answer this client, so every reply is to its own request.
                peer.Replies++; peer.Error = ctx.Event.Status is Status.Failed(var reason) ? reason : null;
            }
            var reducers = peer.Connection.Reducers;
            reducers.OnSetDisplayName += (ctx, _) => Reply(ctx);
            reducers.OnDeleteAccount += ctx => Reply(ctx);
            reducers.OnCreateRoom += (ctx, _) => Reply(ctx);
            reducers.OnJoinRoom += (ctx, _, _) => Reply(ctx);
            reducers.OnLeaveRoom += ctx => Reply(ctx);
            reducers.OnChooseInitialSkill += (ctx, _, _, _) => Reply(ctx);
            reducers.OnConfirmPreparation += (ctx, _, _, _) => Reply(ctx);
            reducers.OnPlayDuelV2 += (ctx, _, _, _, _, _) => Reply(ctx);
            reducers.OnSetProfile += (ctx, _, _, _, _) => Reply(ctx);
            reducers.OnSaveDeckV2 += (ctx, _, _, _, _, _, _, _, _, _) => Reply(ctx);
            reducers.OnFindQuickMatch += (ctx, _, _) => Reply(ctx);
            reducers.OnCancelQuickMatch += (ctx, _) => Reply(ctx);
            reducers.OnFindRankedMatch += (ctx, _, _) => Reply(ctx);
            reducers.OnCancelRankedMatch += (ctx, _) => Reply(ctx);
            reducers.OnOpenSeason += (ctx, _, _) => Reply(ctx);
            reducers.OnCloseSeason += ctx => Reply(ctx);
            reducers.OnOpenTournament += (ctx, _, _, _) => Reply(ctx);
            reducers.OnStartTournament += ctx => Reply(ctx);
            reducers.OnCancelTournament += ctx => Reply(ctx);
            reducers.OnJoinTournament += (ctx, _) => Reply(ctx);
            reducers.OnWithdrawFromTournament += ctx => Reply(ctx);
            reducers.OnSetTournamentReady += (ctx, _) => Reply(ctx);
            await Until(() => peer.Ready, "la conexión de " + name);
            await Call(peer, r => r.SetDisplayName(name));
            return peer;
        }
        async Task Call(Peer peer, Action<RemoteReducers> send, bool rejected = false)
        {
            var before = peer.Replies; send(peer.Connection.Reducers);
            await Until(() => peer.Replies > before, "una respuesta");
            if ((peer.Error != null) != rejected) throw new Exception("Respuesta inesperada: " + (peer.Error ?? "se aceptó una solicitud inválida"));
        }
        static string Id() => Guid.NewGuid().ToString("N");
        async Task Pair(Peer first, Peer second)
        {
            await Call(first, r => r.FindRankedMatch("-", Id()));
            await Until(() => first.Search != null, "la búsqueda");
            await Call(second, r => r.FindRankedMatch("-", Id()));
            await Until(() => first.Room is { Dealt: true } && second.Room is { Dealt: true } && first.Search == null && second.Search == null, "el emparejamiento");
        }
        async Task Begin(Peer a, Peer b)
        {
            await Until(() => a.Room is { Dealt: true } && b.Room is { Dealt: true }, "la sala repartida");
            var room = a.Room!.Code;
            foreach (var peer in new[] { a, b }) await Call(peer, r => r.ChooseInitialSkill(room, CardCatalog.Magnet.Id, Id()));
            await Until(() => a.Hand.Count == 5 && b.Hand.Count == 5, "el reparto");
            foreach (var peer in new[] { a, b }) await Call(peer, r => r.ConfirmPreparation(room, [], Id()));
            await Until(() => a.Duel != null && b.Duel != null, "el duelo");
        }
        async Task Concede(Peer loser, Peer winner)
        {
            var duel = loser.Duel!;
            await Call(loser, r => r.PlayDuelV2(loser.Room!.Code, duel.Revision, Id(), duel.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede), []));
            await Until(() => winner.Duel?.Winner != null && loser.Duel?.Winner != null, "el final");
        }
        async Task Close(Peer closer, Peer other)
        {
            await Call(closer, r => r.LeaveRoom());
            await Until(() => closer.Room == null && other.Room == null, "el cierre");
        }
        Peer Named(string name) => peers.Last(p => p.Connection.IsActive && p.Account?.Name == name);

        async Task Time()
        {
            await Admin("open_season_for_test", "\"Relámpago\"", "7");
            var ana = await Connect("Ana"); var beto = await Connect("Beto");
            await Until(() => ana.Season is { Open: true } && beto.Season is { Open: true }, "la temporada");
            await Pair(ana, beto);
            await Call(ana, r => r.ChooseInitialSkill(ana.Room!.Code, CardCatalog.Magnet.Id, Id()));
            await Until(() => ana.Room == null && beto.Room == null && ana.Result != null && beto.Result != null, "el vencimiento de la preparación", 30);
            Check(ana.Result!.After == 1024 && beto.Result!.After == 976 && beto.Result.Note.Contains("a tiempo"),
                "Quien no prepara su mano a tiempo pierde y la sala se cierra");

            await Pair(ana, beto);
            await Until(() => ana.Room == null && beto.Room == null && ana.Result!.Note.StartsWith("Nadie"), "la doble ausencia", 30);
            Check(ana.Standing is { Value: 1024, Played: 1 } && beto.Standing is { Value: 976, Played: 1 }, "Si nadie prepara su mano, no cambia la puntuación");

            await Pair(ana, beto);
            await Begin(ana, beto);
            var slow = ana.Duel!.DecisionPlayer;
            var waiting = slow == ana.Room!.MySeat ? beto : ana;
            var idle = waiting == ana ? beto : ana;
            await Until(() => ana.Duel?.Winner != null && beto.Duel?.Winner != null, "el reloj de decisión", 35);
            Check(ana.Duel!.Winner == 1 - slow && ana.Duel.EndReason!.Contains("agotó el tiempo"), "Agotar el tiempo termina el duelo contra quien debía decidir");
            await Until(() => waiting.Standing?.Played == 2 && idle.Standing?.Played == 2, "la puntuación");
            var scores = (waiting.Standing!.Value, idle.Standing!.Value);
            await Pause(8);
            await Close(waiting, idle);
            await Pause(0.5);
            Check(scores == (waiting.Standing!.Value, idle.Standing!.Value) && waiting.Standing.Played == 2,
                "Esperar o cerrar la sala no vuelve a mover la puntuación");

            await Admin("open_tournament_for_test", "\"Relámpago\"", "4", "5");
            foreach (var peer in new[] { ana, beto }) await Call(peer, r => r.JoinTournament("-"));
            await Admin("start_tournament_for_test");
            await Until(() => ana.Mine is { Match: > 0 }, "el cuadro");
            await Call(ana, r => r.SetTournamentReady(true));
            await Until(() => ana.Tournament?.State == 2, "el vencimiento de presencia", 30);
            Check(ana.Tournament!.Champion == "Ana" && ana.Unlocked(RewardCatalog.ChampionDice), "Quien se presenta avanza cuando su rival no llega");

            await Admin("open_tournament_for_test", "\"Vacío\"", "4", "5");
            await Until(() => ana.Tournament is { Name: "Vacío", State: 0 }, "el segundo torneo");
            foreach (var peer in new[] { ana, beto }) await Call(peer, r => r.JoinTournament("-"));
            await Admin("start_tournament_for_test");
            await Until(() => ana.Tournament is { Name: "Vacío", State: 2 }, "la doble ausencia del torneo", 30);
            Check(ana.Tournament!.Champion == "", "Un torneo sin nadie presente termina sin campeón");
        }

        async Task Modes()
        {
            var ana = await Connect("Ana"); var beto = await Connect("Beto"); var carla = await Connect("Carla"); var dani = await Connect("Dani");
            Check(ana.Season == null && ana.Tournament == null, "Sin abrir nada, los dos modos siguen planeados");
            await Call(ana, r => r.FindRankedMatch("-", Id()), rejected: true);
            await Call(ana, r => r.JoinTournament("-"), rejected: true);
            await Call(ana, r => r.SetTournamentReady(true), rejected: true);
            Check(true, "Planeado significa que el servidor también los rechaza");
            await Call(ana, r => r.OpenSeason("Mía", 7), rejected: true);
            await Call(ana, r => r.CloseSeason(), rejected: true);
            await Call(ana, r => r.OpenTournament("Mío", 4, 5), rejected: true);
            await Call(ana, r => r.StartTournament(), rejected: true);
            await Call(ana, r => r.CancelTournament(), rejected: true);
            Check(ana.Season == null && ana.Tournament == null, "Un jugador no puede administrar temporadas ni torneos");

            await Call(ana, r => r.SetProfile("yuno", "campeon", ProfileCatalog.DefaultFrame, ProfileCatalog.DefaultPlaymat), rejected: true);
            await Call(ana, r => r.SetProfile("yuno", ProfileCatalog.DefaultTitle, "liga", ProfileCatalog.DefaultPlaymat), rejected: true);
            await Call(ana, r => r.SaveDeckV2(0, "Trampa", "yuno", CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps(), [], "temporada", "clasico", Id()), rejected: true);
            await Call(ana, r => r.SaveDeckV2(0, "Trampa", "yuno", CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps(), [], "clasica", "campeon", Id()), rejected: true);
            Check(true, "El servidor rechaza cosméticos que no se han ganado");

            await Admin("open_season_for_test", "\"Primera\"", "30");
            await Until(() => peers.All(p => p.Season is { Open: true }), "la temporada");
            Check(ana.Season is { Name: "Primera" } season && season.EndsAt > season.StartsAt, "La temporada abierta llega a todos");

            await Call(ana, r => r.CreateRoom("-"));
            await Until(() => ana.Room != null, "la sala");
            await Call(beto, r => r.JoinRoom(ana.Room!.Code, "-"));
            await Begin(ana, beto); await Concede(beto, ana); await Close(ana, beto);
            await Until(() => ana.Profile?.Wins == 1, "el resultado casual");
            Check(ana.Standing == null && beto.Standing == null && ana.Result == null, "Una sala privada cuenta en el perfil pero nunca mueve el ELO");

            await Call(ana, r => r.FindQuickMatch("-", Id()));
            await Call(beto, r => r.FindQuickMatch("-", Id()));
            await Until(() => ana.Room != null && beto.Room != null && ana.Mode?.Mode == "quick", "la partida rápida");
            await Begin(ana, beto); await Concede(beto, ana); await Close(ana, beto);
            await Pause(0.4);
            Check(ana.Standing == null && beto.Standing == null, "La partida rápida tampoco mueve el ELO");

            await Call(carla, r => r.FindRankedMatch("-", Id()));
            await Until(() => carla.Search != null, "la búsqueda");
            await Call(carla, r => r.CreateRoom("-"), rejected: true);
            await Call(carla, r => r.FindQuickMatch("-", Id()), rejected: true);
            Check(ana.Search == null && dani.Search == null, "La búsqueda clasificatoria es privada y excluye a las demás");
            await Call(carla, r => r.CancelRankedMatch(carla.Search!.SearchId));
            await Until(() => carla.Search == null, "la cancelación");

            await Pair(ana, beto);
            await Until(() => ana.Mode is { Mode: "ranked" } && beto.Mode is { Mode: "ranked" } &&
                ana.Connection.Db.MyRoomStandings.Iter().Count() == 2, "el modo de la sala");
            Check(ana.Mode!.DecisionLimit == CompetitiveRules.DecisionLimit && !carla.Connection.Db.MyRoomStandings.Iter().Any() && carla.Mode == null,
                "La sala dice su modo y solo sus dos jugadores ven esas puntuaciones");
            await Begin(ana, beto); await Concede(beto, ana);
            await Until(() => ana.Standing?.Played == 1 && beto.Standing?.Played == 1, "el ELO");
            Check(ana.Standing is { Value: 1024, Wins: 1 } && beto.Standing is { Value: 976, Losses: 1 } && ana.Result is { Before: 1000, After: 1024 },
                "Un duelo clasificado mueve a ambos según la fórmula");
            await Close(beto, ana);
            await Pause(0.4);
            Check(ana.Standing is { Value: 1024, Played: 1 } && beto.Standing is { Value: 976, Played: 1 }, "Cerrar un duelo terminado no puntúa otra vez");

            await Pair(ana, beto);
            await Call(beto, r => r.LeaveRoom());
            await Until(() => ana.Room == null && ana.Standing?.Played == 2, "la salida");
            Check(beto.Standing is { Played: 2, Losses: 2 } && ana.Result!.Note.Contains("salió"), "Salir antes de empezar es una derrota clasificada");

            await Pair(ana, beto);
            await Begin(ana, beto);
            await Call(beto, r => r.LeaveRoom());
            await Until(() => ana.Room == null && ana.Standing?.Played == 3, "la salida tras la iniciativa");
            Check(beto.Standing is { Losses: 3 }, "Ver la iniciativa y cerrar la sala también cuenta como derrota");

            for (var played = 3; played < CompetitiveRules.PairDuelsPerDay; played++)
            {
                await Pair(ana, beto);
                await Begin(ana, beto); await Concede(beto, ana); await Close(ana, beto);
            }
            await Until(() => ana.Standing?.Played == 5, "los cinco duelos");
            await Call(ana, r => r.FindRankedMatch("-", Id()));
            await Call(beto, r => r.FindRankedMatch("-", Id()));
            await Pause(2);
            Check(ana.Room == null && beto.Room == null && ana.Search != null && beto.Search != null, "La misma pareja no se empareja más de cinco veces al día");
            await Call(beto, r => r.CancelRankedMatch(beto.Search!.SearchId));
            await Until(() => beto.Search == null, "la cancelación");
            await Call(carla, r => r.FindRankedMatch("-", Id()));
            await Until(() => ana.Room is { Dealt: true } && carla.Room is { Dealt: true }, "un rival distinto", 35);
            Check(true, "La búsqueda se amplía hasta un rival de otra puntuación");
            await Begin(ana, carla); await Concede(carla, ana); await Close(ana, carla);
            await Until(() => ana.Standing?.Played == 6, "el sexto duelo");
            var final = ana.Standing!.Value;
            Check(final >= 1100 && carla.Standing is { Played: 1 }, "Seis victorias seguidas llegan a Oro");

            await Call(dani, r => r.FindRankedMatch("-", Id()));
            await Until(() => dani.Search != null, "la búsqueda al cierre");
            await Admin("close_season_for_test");
            await Until(() => ana.Season is { Open: false } && dani.Search == null && ana.Emblems.Count == 1, "el cierre de temporada");
            Check(ana.Standing is { Place: 1 } && ana.Emblems[0] is { Kind: "season", Title: "Primera" } emblem && emblem.Detail.StartsWith("Oro"),
                "Cerrar la temporada da puesto y emblema, y vacía la cola");
            Check(ana.Unlocked(RewardCatalog.SeasonTitle) && ana.Unlocked(RewardCatalog.LeagueFrame) && !ana.Unlocked(RewardCatalog.ChampionTitle) &&
                carla.Emblems.Count == 0 && !carla.Unlocked(RewardCatalog.SeasonTitle), "Los premios exigen los cinco duelos y la liga");
            await Call(ana, r => r.SetProfile("yuno", "clasificado", "liga", ProfileCatalog.DefaultPlaymat));
            await Call(carla, r => r.SetProfile("yuno", "clasificado", ProfileCatalog.DefaultFrame, ProfileCatalog.DefaultPlaymat), rejected: true);
            await Call(ana, r => r.FindRankedMatch("-", Id()), rejected: true);
            Check(true, "Lo ganado se puede usar y una temporada cerrada no empareja");

            await Admin("open_season_for_test", "\"Segunda\"", "30");
            await Until(() => ana.Season is { Open: true, Name: "Segunda" }, "la segunda temporada");
            await Call(ana, r => r.FindRankedMatch("-", Id()));
            await Until(() => ana.Standing != null && ana.Search != null, "el reinicio");
            Check(ana.Standing is { Played: 0 } fresh && fresh.Value == CompetitiveRules.SoftReset(final) && ana.Search!.Value == fresh.Value,
                "La temporada nueva parte del reinicio suave");
            await Call(ana, r => r.CancelRankedMatch(ana.Search!.SearchId));
            await Until(() => ana.Search == null, "la cancelación");

            await Admin("open_tournament_for_test", "\"Copa Relick\"", "3", "30");
            await Until(() => peers.All(p => p.Tournament is { State: 0 }), "la inscripción");
            foreach (var peer in new[] { ana, beto, carla }) await Call(peer, r => r.JoinTournament("-"));
            await Call(dani, r => r.JoinTournament("-"), rejected: true);
            await Until(() => dani.Tournament?.Entered == 3 && dani.Entrants.Count == 3, "los inscritos");
            Check(dani.Error!.Contains("lleno") && dani.Mine == null, "El cupo se respeta");
            await Call(carla, r => r.WithdrawFromTournament());
            await Call(carla, r => r.JoinTournament("-"));
            await Call(carla, r => r.JoinTournament("-"));
            await Until(() => ana.Entrants.Count == 3 && carla.Mine != null, "la reinscripción");
            Check(ana.Tournament!.Entered == 3, "Retirarse libera el lugar e inscribirse dos veces no lo duplica");

            await Admin("start_tournament_for_test");
            await Until(() => new[] { ana, beto, carla }.All(p => p.Tournament is { State: 1, Rounds: 2 } && p.Bracket.Count == 2), "el cuadro");
            var bye = ana.Bracket.Single(m => m.EntryB == 0 || m.EntryA == 0);
            var rested = Named(ana.Entrants.Single(e => e.Id == bye.EntryA + bye.EntryB).Name);
            var pair = new[] { ana, beto, carla }.Where(p => p != rested).ToArray();
            await Until(() => pair.All(p => p.Mine is { Match: > 0 }), "el duelo pendiente");
            Check(rested.Entrants.Single(e => e.Name == rested.Account!.Name).Seed == 1 && rested.Mine is { Match: 0 },
                "Con tres inscritos descansa la primera semilla");
            await Call(rested, r => r.SetTournamentReady(true), rejected: true);
            await Call(pair[0], r => r.SetTournamentReady(true));
            await Until(() => pair[1].Mine!.RivalReady, "la presencia");
            await Call(pair[0], r => r.CreateRoom("-"), rejected: true);
            await Call(pair[0], r => r.FindRankedMatch("-", Id()), rejected: true);
            Check(pair[0].Room == null, "Esperar el duelo de torneo excluye otras partidas");
            await Call(pair[1], r => r.SetTournamentReady(true));
            await Until(() => pair.All(p => p.Room is { Dealt: true } && p.Mode is { Mode: "tournament" }), "la sala del torneo");
            await Begin(pair[0], pair[1]); await Concede(pair[0], pair[1]); await Close(pair[1], pair[0]);
            await Until(() => ana.Tournament?.Round == 2 && pair[1].Mine is { Match: > 0 } && rested.Mine is { Match: > 0 } && pair[0].Mine is { OutRound: 1 }, "la final");
            Check(pair[0].Standing is not { Played: > 0 }, "El ganador avanza, el perdedor cae y el torneo no mueve el ELO");

            await Call(rested, r => r.SetTournamentReady(true));
            await Call(pair[1], r => r.SetTournamentReady(true));
            await Begin(rested, pair[1]); await Concede(pair[1], rested); await Close(rested, pair[1]);
            await Until(() => rested.Tournament is { State: 2 } && rested.Unlocked(RewardCatalog.ChampionTitle) && rested.Mine is { OutRound: 3 }, "el campeón");
            Check(rested.Tournament!.Champion == rested.Account!.Name && rested.Emblems.Any(e => e.Kind == "tournament") &&
                rested.Unlocked(RewardCatalog.ChampionDice) && rested.Unlocked(RewardCatalog.ChampionFrame), "El campeón recibe emblema, título, marco y dado");
            await Call(rested, r => r.SaveDeckV2(0, "Campeón", "yuno", CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps(), [], "clasica", "campeon", Id()));
            Check(true, "El dado de campeón ya se puede guardar en un mazo");

            await Admin("open_tournament_for_test", "\"Segunda copa\"", "4", "30");
            await Until(() => ana.Tournament is { State: 0, Name: "Segunda copa" }, "la segunda copa");
            foreach (var peer in new[] { beto, dani }) await Call(peer, r => r.JoinTournament("-"));
            await Admin("start_tournament_for_test");
            await Until(() => dani.Mine is { Match: > 0 }, "la segunda final");
            await Call(beto, r => r.DeleteAccount());
            await Until(() => dani.Tournament is { State: 2 }, "el abandono por borrado");
            Check(dani.Tournament!.Champion == "Dani" && dani.Entrants.Any(e => e.Name == "Cuenta eliminada") && dani.Entrants.All(e => e.Name != "Beto"),
                "Borrar la cuenta quita su nombre del cuadro y deja avanzar al rival");

            await Admin("open_tournament_for_test", "\"Cancelada\"", "4", "30");
            await Until(() => dani.Tournament is { State: 0, Name: "Cancelada" }, "la tercera copa");
            await Call(dani, r => r.JoinTournament("-"));
            await Admin("cancel_tournament_for_test");
            await Until(() => dani.Tournament is { State: 3 }, "la cancelación del torneo");
            await Call(dani, r => r.JoinTournament("-"), rejected: true);
            Check(true, "Un torneo cancelado no admite inscripciones");
        }

        try
        {
            if (timed) await Time(); else await Modes();
            Console.WriteLine($"{checks} comprobaciones competitivas correctas.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL " + error.Message);
            return 1;
        }
        finally
        {
            foreach (var peer in peers.Where(p => p.Connection.IsActive)) peer.Connection.Disconnect();
        }
    }

    private sealed class Peer
    {
        public DbConnection Connection = null!;
        public Identity? Identity;
        public bool Ready;
        public string? Fatal, Error;
        public int Replies;
        public AccountInfo? Account => Connection.Db.MyAccount.Iter().SingleOrDefault();
        public RoomInfo? Room => Connection.Db.MyRoom.Iter().SingleOrDefault();
        public RoomModeInfo? Mode => Connection.Db.MyRoomMode.Iter().SingleOrDefault();
        public ProfileInfo? Profile => Connection.Db.MyProfile.Iter().SingleOrDefault();
        public SeasonInfo? Season => Connection.Db.CurrentSeason.Iter().SingleOrDefault();
        public StandingInfo? Standing => Season is { } season ? Connection.Db.MyStandings.Season.Find(season.Id) : null;
        public RatedResult? Result => Connection.Db.MyRatedResult.Iter().SingleOrDefault();
        public RankedSearchInfo? Search => Connection.Db.MyRankedSearch.Iter().SingleOrDefault();
        public TournamentInfo? Tournament => Connection.Db.CurrentTournament.Iter().SingleOrDefault();
        public MyTournamentInfo? Mine => Connection.Db.MyTournament.Iter().SingleOrDefault();
        public List<EntrantInfo> Entrants => Connection.Db.TournamentEntrants.Iter().ToList();
        public List<BracketInfo> Bracket => Connection.Db.TournamentBracket.Iter().ToList();
        public List<EmblemInfo> Emblems => Connection.Db.MyEmblems.Iter().ToList();
        public bool Unlocked(Reward reward) => Connection.Db.MyUnlocks.Key.Find(reward.Key) != null;
        public List<string> Hand => Connection.Db.MyHand.Iter().OrderBy(card => card.Position).Select(card => card.Definition).ToList();
        public DuelSnapshot? Duel => Connection.Db.MyDuel.Iter().SingleOrDefault() is { } row ? JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelSnapshot) : null;
    }
}
