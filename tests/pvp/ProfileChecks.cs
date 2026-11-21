using System.Diagnostics;
using System.Text.Json;
using Relick.Core;
using Relick.Network.Types;
using SpacetimeDB;

// Public profiles against a published module: validation, who may see a rival's profile, and recorded results.
static class ProfileChecks
{
    public static async Task<int> Run(string[] args)
    {
        var server = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:3100";
        var database = args.ElementAtOrDefault(2) ?? "relick-dev";
        var peers = new List<Peer>();
        var checks = 0;
        void Check(bool ok, string reason)
        {
            if (!ok) throw new Exception(reason);
            checks++; Console.WriteLine("PASS " + reason);
        }
        async Task Until(Func<bool> done, double seconds = 20)
        {
            var timer = Stopwatch.StartNew();
            while (true)
            {
                foreach (var peer in peers) peer.Connection.FrameTick();
                if (peers.Any(p => p.Fatal != null)) throw new Exception(peers.First(p => p.Fatal != null).Fatal);
                if (done()) return;
                if (timer.Elapsed.TotalSeconds > seconds) throw new Exception("Tiempo agotado esperando al servidor.");
                await Task.Delay(5);
            }
        }
        async Task<Peer> Connect()
        {
            var peer = new Peer(); peers.Add(peer);
            peer.Connection = DbConnection.Builder().WithUri(server).WithDatabaseName(database)
                .OnConnect((connection, _, _) => connection.SubscriptionBuilder().OnApplied(_ => peer.Ready = true)
                    .OnError((_, e) => peer.Fatal = e.Message)
                    .Subscribe(["SELECT * FROM my_account", "SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_duel",
                        "SELECT * FROM my_profile", "SELECT * FROM my_room_profiles"]))
                .OnConnectError(e => peer.Fatal = e.Message).Build();
            void Reply(ReducerEventContext ctx)
            {
                peer.Replies++; peer.Error = ctx.Event.Status is Status.Failed(var reason) ? reason : null;
            }
            peer.Connection.Reducers.OnSetProfile += (ctx, _, _, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnSetDisplayName += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnDeleteAccount += ctx => Reply(ctx);
            peer.Connection.Reducers.OnCreateRoom += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnJoinRoom += (ctx, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnLeaveRoom += ctx => Reply(ctx);
            peer.Connection.Reducers.OnChooseInitialSkill += (ctx, _, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnConfirmPreparation += (ctx, _, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnPlayDuelV2 += (ctx, _, _, _, _, _) => Reply(ctx);
            await Until(() => peer.Ready); return peer;
        }
        async Task Call(Peer peer, Action<RemoteReducers> send, bool rejected = false)
        {
            var before = peer.Replies; send(peer.Connection.Reducers); await Until(() => peer.Replies > before);
            if ((peer.Error != null) != rejected) throw new Exception("Respuesta inesperada: " + (peer.Error ?? "se aceptó una solicitud inválida"));
        }
        static string Id() => Guid.NewGuid().ToString("N");

        // Plays one duel between two peers that ends with the loser conceding.
        async Task Duel(Peer host, Peer guest, Peer loser)
        {
            await Call(host, r => r.CreateRoom("Anfitrión"));
            await Until(() => host.Room != null);
            await Call(guest, r => r.JoinRoom(host.Room!.Code, "Invitado"));
            await Until(() => host.Room!.Dealt && guest.Room?.Dealt == true);
            var room = host.Room!.Code;
            foreach (var peer in new[] { host, guest }) await Call(peer, r => r.ChooseInitialSkill(room, CardCatalog.Magnet.Id, Id()));
            await Until(() => host.Hand.Count == 5 && guest.Hand.Count == 5);
            foreach (var peer in new[] { host, guest }) await Call(peer, r => r.ConfirmPreparation(room, [], Id()));
            await Until(() => loser.Duel != null);
            var duel = loser.Duel!;
            await Call(loser, r => r.PlayDuelV2(room, duel.Revision, Id(), duel.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede), []));
            await Until(() => host.Duel?.Winner != null && guest.Duel?.Winner != null);
            await Call(host, r => r.LeaveRoom());
            await Until(() => host.Room == null && guest.Room == null);
        }

        try
        {
            var ana = await Connect(); var beto = await Connect(); var carla = await Connect();
            await Call(ana, r => r.SetDisplayName("Ana"));
            await Call(beto, r => r.SetDisplayName("Beto"));
            await Until(() => ana.Profile is { Name: "Ana" });
            Check(ana.Profile is { Avatar: "yuno", Title: ProfileCatalog.DefaultTitle, Frame: ProfileCatalog.DefaultFrame,
                Playmat: ProfileCatalog.DefaultPlaymat, Wins: 0, Losses: 0, Draws: 0, Favorite: "" },
                "Una cuenta nueva ve su perfil con los valores predeterminados y sin duelos");

            await Call(ana, r => r.SetProfile("yuno-lv2", ProfileCatalog.DefaultTitle, ProfileCatalog.DefaultFrame, ProfileCatalog.DefaultPlaymat), rejected: true);
            Check(ana.Error!.Contains("retrato"), "Una evolución no es un retrato válido");
            await Call(ana, r => r.SetProfile("osamu", "inventado", ProfileCatalog.DefaultFrame, ProfileCatalog.DefaultPlaymat), rejected: true);
            await Call(ana, r => r.SetProfile("osamu", ProfileCatalog.DefaultTitle, "", ProfileCatalog.DefaultPlaymat), rejected: true);
            await Call(ana, r => r.SetProfile("osamu", ProfileCatalog.DefaultTitle, ProfileCatalog.DefaultFrame, "x"), rejected: true);
            Check(ana.Profile!.Avatar == "yuno", "El servidor rechaza títulos, marcos y tapetes que no existen");
            await Call(ana, r => r.SetProfile("osamu", "estratega", "oro", "aurora"));
            await Until(() => ana.Profile is { Avatar: "osamu", Title: "estratega", Frame: "oro", Playmat: "aurora" });
            Check(true, "El jugador guarda su retrato, título, marco y tapete");

            Check(!beto.RoomProfiles.Any() && !carla.RoomProfiles.Any(), "Fuera de una sala no se ve el perfil de nadie");
            await Call(ana, r => r.CreateRoom("Ana"));
            await Until(() => ana.Room != null && ana.RoomProfiles.Count == 1);
            await Call(beto, r => r.JoinRoom(ana.Room!.Code, "Beto"));
            await Until(() => beto.RoomProfiles.Count == 2 && ana.RoomProfiles.Count == 2);
            var anaSeat = ana.Room!.MySeat;
            Check(beto.RoomProfiles.Single(p => p.Seat == anaSeat) is { Name: "Ana", Avatar: "osamu", Title: "estratega", Frame: "oro", Playmat: "aurora" },
                "El rival ve el perfil de quien comparte su sala");
            Check(beto.RoomProfiles.All(p => p.RoomCode == ana.Room.Code) && !carla.RoomProfiles.Any(),
                "Un tercero que no está en la sala no ve esos perfiles");
            await Call(ana, r => r.LeaveRoom());
            await Until(() => ana.Room == null && beto.Room == null && !beto.RoomProfiles.Any());
            Check(true, "Al cerrar la sala desaparece el perfil del rival");

            await Duel(ana, beto, loser: beto);
            await Until(() => ana.Profile is { Wins: 1 } && beto.Profile is { Losses: 1 });
            Check(ana.Profile is { Wins: 1, Losses: 0, Draws: 0, Favorite: "yuno" } && beto.Profile is { Wins: 0, Losses: 1, Favorite: "yuno" },
                "Al terminar un duelo se registran la victoria y la derrota con su Relikario");
            await Duel(beto, ana, loser: ana);
            await Until(() => ana.Profile is { Losses: 1 } && beto.Profile is { Wins: 1 });
            Check(ana.Profile is { Wins: 1, Losses: 1 } && beto.Profile is { Wins: 1, Losses: 1 },
                "Cada duelo cuenta una sola vez y el abandono cuenta como derrota");

            await Call(ana, r => r.DeleteAccount());
            await Until(() => ana.Profile == null);
            var again = await Connect();
            await Until(() => again.Profile != null);
            Check(again.Profile is { Avatar: "yuno", Wins: 0 }, "Eliminar la cuenta borra el perfil; otra identidad empieza vacía");

            Console.WriteLine($"{checks} comprobaciones de perfil correctas.");
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
        public bool Ready;
        public string? Fatal, Error;
        public int Replies;
        public RoomInfo? Room => Connection.Db.MyRoom.Iter().SingleOrDefault();
        public ProfileInfo? Profile => Connection.Db.MyProfile.Iter().SingleOrDefault();
        public List<ProfileInfo> RoomProfiles => Connection.Db.MyRoomProfiles.Iter().ToList();
        public List<string> Hand => Connection.Db.MyHand.Iter().OrderBy(card => card.Position).Select(card => card.Definition).ToList();
        public DuelSnapshot? Duel => Connection.Db.MyDuel.Iter().SingleOrDefault() is { } row ? JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelSnapshot) : null;
    }
}
