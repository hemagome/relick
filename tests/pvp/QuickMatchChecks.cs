using System.Diagnostics;
using Relick.Network;
using Relick.Network.Types;
using SpacetimeDB;

static class QuickMatchChecks
{
    public static async Task<int> Run(string[] args)
    {
        var server = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:3100";
        var database = args.ElementAtOrDefault(2) ?? "relick-dev";
        var services = new List<LobbyConnection>();
        var tokens = new Dictionary<LobbyConnection, string>();
        var peers = new List<Peer>();
        var checks = 0;
        void Check(bool ok, string reason)
        {
            if (!ok) throw new Exception(reason);
            checks++; Console.WriteLine("PASS " + reason);
        }
        async Task Until(Func<bool> done, double seconds = 25)
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                foreach (var service in services) service.Tick();
                foreach (var peer in peers) peer.Connection.FrameTick();
                if (peers.Any(p => p.Fatal != null)) throw new Exception(peers.First(p => p.Fatal != null).Fatal);
                if (done()) return;
                if (timer.Elapsed.TotalSeconds > seconds) throw new Exception("Tiempo agotado: " + string.Join(" / ", services.Select(s => s.Message)));
                await Task.Delay(5);
            }
        }
        async Task Wait(double seconds)
        {
            var timer = Stopwatch.StartNew(); await Until(() => timer.Elapsed.TotalSeconds >= seconds, seconds + 5);
        }
        async Task<LobbyConnection> Service()
        {
            var service = new LobbyConnection(); services.Add(service);
            service.TokenReceived += token => tokens[service] = token;
            service.Connect(server, database, null); await Until(() => service.Ready && service.QuickMatchAvailable); return service;
        }
        async Task<Peer> Raw(string? token = null)
        {
            var peer = new Peer(); peers.Add(peer);
            var builder = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
                .OnConnect((connection, _, _) => connection.SubscriptionBuilder().OnApplied(_ => peer.Ready = true)
                    .OnError((_, e) => peer.Fatal = e.Message).Subscribe(["SELECT * FROM my_room", "SELECT * FROM my_quick_search"]))
                .OnConnectError(e => peer.Fatal = e.Message);
            if (token != null) builder = builder.WithToken(token);
            peer.Connection = builder.Build();
            void Reply(ReducerEventContext ctx)
            {
                peer.Replies++; peer.Error = ctx.Event.Status is Status.Failed(var reason) ? reason : null;
            }
            peer.Connection.Reducers.OnFindQuickMatch += (ctx, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnCancelQuickMatch += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnCreateRoom += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnJoinRoom += (ctx, _, _) => Reply(ctx);
            await Until(() => peer.Ready); return peer;
        }
        async Task Call(Peer peer, Action send, bool rejected = false)
        {
            var before = peer.Replies; send(); await Until(() => peer.Replies > before);
            if ((peer.Error != null) != rejected) throw new Exception("Respuesta inesperada: " + peer.Error);
        }
        try
        {
            var a = await Service(); var b = await Service();
            var rawA = await Raw(tokens[a]); var rawB = await Raw(tokens[b]);
            await Call(rawA, () => rawA.Connection.Reducers.FindQuickMatch(" ", Guid.NewGuid().ToString("N")), true);
            await Call(rawA, () => rawA.Connection.Reducers.FindQuickMatch("Ana", "invalid"), true);
            Check(!a.Searching && a.Room == null, "Nombres y solicitudes inválidas no entran a la cola.");
            a.FindQuickMatch("Ana"); await Until(() => a.Searching && a.PendingRoomRequest == RoomRequest.None);
            var firstId = a.QuickSearchId!;
            await Call(rawA, () => rawA.Connection.Reducers.FindQuickMatch("Duplicada", firstId));
            await Call(rawA, () => rawA.Connection.Reducers.FindQuickMatch("Otra pestaña", Guid.NewGuid().ToString("N")));
            Check(a.QuickSearchId == firstId && a.Room == null && !b.Searching, "Una identidad tiene una sola búsqueda y no se empareja consigo misma.");
            Check(rawB.Search == null && rawA.Search == firstId, "La vista de cola expone únicamente la búsqueda propia.");
            var denied = false;
            rawB.Connection.SubscriptionBuilder().OnError((_, _) => denied = true).Subscribe(["SELECT * FROM quick_search"]);
            await Until(() => denied); Check(denied, "La tabla de cola privada rechaza consultas ajenas.");
            await Call(rawB, () => rawB.Connection.Reducers.CancelQuickMatch(firstId));
            Check(a.Searching, "Otro jugador no puede cancelar una búsqueda ajena.");
            await Call(rawA, () => rawA.Connection.Reducers.CreateRoom("Ana"), true);
            var host = await Service(); host.Create("Privada"); await Until(() => host.Room != null);
            await Call(rawA, () => rawA.Connection.Reducers.JoinRoom(host.Room!.Code, "Ana"), true);
            Check(a.Searching && a.Room == null, "Buscar y crear/entrar a una sala privada se excluyen también en el servidor.");
            host.Leave(); await Until(() => host.Room == null);
            await Wait(17);
            Check(a.Searching && a.Ready, "La búsqueda activa renueva su lease y resiste temporizadores antiguos.");
            a.CancelQuickMatch(); await Until(() => !a.Searching && a.PendingRoomRequest == RoomRequest.None);
            Check(a.Room == null, "Cancelar antes del emparejamiento retira la búsqueda sin crear sala.");
            a.FindQuickMatch("Ana"); await Until(() => a.Searching && a.PendingRoomRequest == RoomRequest.None);
            var secondId = a.QuickSearchId!;
            await Call(rawA, () => rawA.Connection.Reducers.CancelQuickMatch(firstId));
            Check(a.QuickSearchId == secondId, "Una cancelación antigua no afecta una búsqueda nueva.");
            b.FindQuickMatch("Beto"); await Until(() => a.Room is { Dealt: true } && b.Room is { Dealt: true });
            var code = a.Room!.Code;
            Check(b.Room!.Code == code && a.Room.MySeat == 0 && b.Room.MySeat == 1 && !a.Searching && !b.Searching,
                "Dos jugadores reciben la misma sala completa y salen de la cola atómicamente.");
            await Call(rawA, () => rawA.Connection.Reducers.CancelQuickMatch(secondId));
            await Call(rawA, () => rawA.Connection.Reducers.FindQuickMatch("Reintento", secondId));
            Check(a.Room?.Code == code && b.Room?.Code == code, "Cancelar o repetir la solicitud después de encontrar rival conserva el duelo.");
            await Until(() => a.InitialSkills != null && b.InitialSkills != null);
            a.ChooseInitialSkill(a.InitialSkills!.Options[0]); b.ChooseInitialSkill(b.InitialSkills!.Options[0]);
            await Until(() => a.Hand.Length == 5 && b.Hand.Length == 5 && !a.InitialSkillPending && !b.InitialSkillPending);
            a.Confirm([]); b.Confirm([]); await Until(() => a.Duel != null && b.Duel != null);
            a.Connect(server, database, tokens[a]); await Until(() => a.Ready && a.Duel != null);
            Check(a.Room?.Code == code && b.Room?.Code == code, "La pareja completa habilidad, mulligan e inicio y recupera el duelo al reconectar.");
            a.Leave(); await Until(() => a.Room == null && b.Room == null);
            a.FindQuickMatch("Ana"); await Until(() => a.Searching && rawA.Search != null);
            a.Dispose(); await Until(() => rawA.Search == null);
            a.Connect(server, database, tokens[a]); await Until(() => a.Ready);
            Check(!a.Searching && a.Room == null, "Desconectar retira la búsqueda; reconectar no la reinicia sin consentimiento.");
            var idle = await Raw(); var idleId = Guid.NewGuid().ToString("N");
            await Call(idle, () => idle.Connection.Reducers.FindQuickMatch("Inactiva", idleId));
            await Until(() => idle.Search != null); await Until(() => idle.Search == null, 18);
            Check(idle.Search == null, "Una búsqueda sin renovaciones caduca aunque el socket siga abierto.");
            var wave = new[] { await Service(), await Service(), await Service(), await Service() };
            foreach (var (client, index) in wave.Select((c, i) => (c, i))) client.FindQuickMatch("Jugador " + index);
            await Until(() => wave.All(c => c.Room?.Dealt == true));
            var pairs = wave.GroupBy(c => c.Room!.Code).ToArray();
            Check(pairs.Length == 2 && pairs.All(p => p.Count() == 2 && p.Select(c => c.Room!.MySeat).Distinct().Count() == 2),
                "Cuatro solicitudes concurrentes forman dos parejas sin duplicados.");
            foreach (var pair in pairs) pair.First().Leave(); await Until(() => wave.All(c => c.Room == null));
            Console.WriteLine($"QUICK_MATCH_PASS · {checks} comprobaciones."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            foreach (var service in services) { service.CancelQuickMatch(); service.Leave(); }
            try { await Wait(0.3); } catch (Exception) { }
            foreach (var service in services) service.Dispose();
            foreach (var peer in peers) peer.Connection.Disconnect();
        }
    }

    private sealed class Peer
    {
        public DbConnection Connection = null!;
        public bool Ready;
        public string? Fatal, Error;
        public int Replies;
        public string? Search => Connection.Db.MyQuickSearch.Iter().SingleOrDefault()?.SearchId;
    }
}
