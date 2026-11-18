using System.Diagnostics;
using System.Text.Json;
using Relick.Core;
using Relick.Network;
using Relick.Network.Types;
using SpacetimeDB;

// Saved decks against a published module: ownership, drafts, the active deck and dealing it.
static class DeckChecks
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
                    .Subscribe(["SELECT * FROM my_decks", "SELECT * FROM my_deck_styles", "SELECT * FROM my_account", "SELECT * FROM my_room", "SELECT * FROM my_hand", "SELECT * FROM my_initial_skill", "SELECT * FROM my_duel", "SELECT * FROM my_room_relikarios"]))
                .OnConnectError(e => peer.Fatal = e.Message).Build();
            void Reply(ReducerEventContext ctx)
            {
                peer.Replies++; peer.Error = ctx.Event.Status is Status.Failed(var reason) ? reason : null;
            }
            peer.Connection.Reducers.OnSaveDeck += (ctx, _, _, _, _, _, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnSetDisplayName += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnDeleteAccount += ctx => Reply(ctx);
            peer.Connection.Reducers.OnDeleteAccountOf += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnDeleteDeck += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnSetActiveDeck += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnCreateRoom += (ctx, _) => Reply(ctx);
            peer.Connection.Reducers.OnJoinRoom += (ctx, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnLeaveRoom += ctx => Reply(ctx);
            peer.Connection.Reducers.OnChooseInitialSkill += (ctx, _, _, _) => Reply(ctx);
            peer.Connection.Reducers.OnSaveDeckV2 += (ctx, _, _, _, _, _, _, _, _, _) => Reply(ctx);
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

        try
        {
            if (args.Contains("--guest"))
            {
                // Guests only come from the account provider, so the owner marks this player as one while the check waits:
                //   spacetime sql <database> "UPDATE account SET guest = true"
                var visitor = await Connect();
                Console.WriteLine("Esperando a que la cuenta se marque como invitada…");
                await Until(() => visitor.Connection.Db.MyAccount.Iter().SingleOrDefault()?.Guest == true, 60);
                var legal = CardCatalog.TrainingDeck();
                await Call(visitor, r => r.SaveDeck(0, "De invitado", "yuno", legal, [], [], Id()), rejected: true);
                Check(visitor.Error!.Contains("invitado") && visitor.Decks.Count == 0, "Un invitado no puede guardar mazos");
                await Call(visitor, r => r.SetActiveDeck(1), rejected: true);
                Check(visitor.Error!.Contains("invitado"), "Un invitado no puede activar un mazo guardado");
                await Call(visitor, r => r.SetActiveDeck(0));
                await Call(visitor, r => r.CreateRoom("Invitado"));
                await Until(() => visitor.Room != null);
                await Call(visitor, r => r.LeaveRoom());
                Check(true, "Un invitado sí puede abrir salas con el mazo de entrenamiento");
                Console.WriteLine($"{checks} comprobaciones de invitado correctas.");
                return 0;
            }
            var ana = await Connect(); var beto = await Connect();
            // Twelve Attacks plus two copies of fifteen cards: legal, and unlike the training deck it has no Equipment.
            var pairs = CardCatalog.TrainingDeck().Select(CardCatalog.Get)
                .Where(card => card.Kind is CardKind.Skill or CardKind.Code).Select(card => card.Id).Distinct().Take(15).ToList();
            string[] attackArts = [CardCatalog.Attack.Id, CardCatalog.Attack.Id, CardCatalog.AttackYuno.Id,
                CardCatalog.AttackOsamu.Id, CardCatalog.AttackYunoDt.Id, CardCatalog.AttackOsamuDt.Id];
            var custom = attackArts.Concat(attackArts).Concat(pairs).Concat(pairs).ToList();
            List<string> traps = ["super-attack", "super-attack", "super-magnet"];
            Check(DeckRules.Validate("yuno", custom, traps).Count == 0, "El mazo de prueba cumple las reglas");

            var command = Id();
            await Call(ana, r => r.SaveDeck(0, "  Dobles  ", "yuno", custom, traps, [], command));
            await Call(ana, r => r.SaveDeck(0, "Dobles", "yuno", custom, traps, [], command));
            await Until(() => ana.Decks.Count == 1);
            var deck = ana.Decks.Single();
            Check(deck is { Name: "Dobles", Valid: true, Active: false } && deck.Cards.SequenceEqual(custom) && deck.Traps.SequenceEqual(traps),
                "Guardar conserva la mezcla de artes, crea un solo mazo aunque se reintente y recorta el nombre");
            Check(beto.Decks.Count == 0, "Los mazos de otro jugador no son visibles");

            await Call(ana, r => r.SaveDeck(0, "Borrador", "yuno", custom.Skip(1).ToList(), [], [], Id()));
            await Until(() => ana.Decks.Count == 2);
            var draft = ana.Decks.Single(d => d.Name == "Borrador");
            Check(!draft.Valid, "Un mazo que no cumple las reglas se guarda como borrador");
            await Call(ana, r => r.SetActiveDeck(draft.Id), rejected: true);
            Check(ana.Error!.Contains("no cumple las reglas"), "Un borrador no puede ser el mazo activo");

            await Call(ana, r => r.SaveDeck(0, "Falso", "yuno", custom.Skip(1).Append("no-existe").ToList(), [], [], Id()), rejected: true);
            await Call(ana, r => r.SaveDeck(0, "Falso", "otro", custom, [], [], Id()), rejected: true);
            await Call(ana, r => r.SaveDeck(0, " ", "yuno", custom, [], [], Id()), rejected: true);
            await Call(ana, r => r.SaveDeck(0, "Enorme", "yuno", Enumerable.Repeat("attack", 200).ToList(), [], [], Id()), rejected: true);
            await Call(ana, r => r.SaveDeck(0, "Mal id", "yuno", custom, [], [], "x"), rejected: true);
            Check(ana.Decks.Count == 2, "Cartas, Relikarios, nombres, tamaños e identificadores inválidos se rechazan");

            await Call(beto, r => r.SetActiveDeck(deck.Id), rejected: true);
            await Call(beto, r => r.SaveDeck(deck.Id, "Robado", "yuno", custom, [], [], Id()), rejected: true);
            await Call(beto, r => r.DeleteDeck(deck.Id), rejected: true);
            Check(ana.Decks.Count == 2 && ana.Decks.Any(d => d.Name == "Dobles"), "Otro jugador no puede usar, editar ni borrar un mazo ajeno");

            await Call(ana, r => r.SetActiveDeck(deck.Id));
            await Until(() => ana.Decks.Single(d => d.Id == deck.Id).Active);
            Check(!ana.Decks.Single(d => d.Id == draft.Id).Active, "Solo el mazo elegido queda activo");

            // The side deck travels with the deck, counts toward copy limits, and never reaches the hand dealt below.
            var bench = CardCatalog.All.Values.Where(card => !DeckRules.IsAttack(card) && !DeckRules.IsTrap(card) && !pairs.Contains(card.Id) && DeckRules.Compatible(card, "yuno"))
                .Select(card => card.Id).Take(DeckRules.MaxSide).ToList();
            Check(bench.Count == DeckRules.MaxSide && DeckRules.Validate("yuno", custom, traps, bench).Count == 0, "El banquillo de prueba cumple las reglas");
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, [pairs[0]], Id()));
            await Until(() => ana.Decks.Single(d => d.Id == deck.Id) is { Valid: false, Active: false });
            Check(true, "Una tercera copia en el banquillo deja el mazo como borrador y lo desactiva");
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, ["attack"], Id()));
            await Call(ana, r => r.SetActiveDeck(deck.Id), rejected: true);
            Check(ana.Error!.Contains("banquillo no admite cartas de Ataque"), "Un banquillo con Ataques no se puede activar");
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, Enumerable.Repeat(bench[0], 21).ToList(), Id()), rejected: true);
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, ["no-existe"], Id()), rejected: true);
            Check(ana.Decks.Single(d => d.Id == deck.Id).Side.SequenceEqual(["attack"]), "Banquillos desmedidos o con cartas inexistentes se rechazan");
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, bench, Id()));
            await Call(ana, r => r.SetActiveDeck(deck.Id));
            await Until(() => ana.Decks.Single(d => d.Id == deck.Id) is { Valid: true, Active: true } saved && saved.Side.SequenceEqual(bench));
            Check(true, "Guardar conserva un banquillo de diez cartas y el mazo sigue siendo jugable");

            // The deck's sleeve and die: saved with it, kept when an older client saves without them, and validated.
            await Call(ana, r => r.SaveDeckV2(deck.Id, "Dobles", "yuno", custom, traps, bench, "oro", "obsidiana", Id()));
            await Until(() => ana.Decks.Single(d => d.Id == deck.Id) is { Sleeve: "oro", Dice: "obsidiana", Valid: true, Active: true });
            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, bench, Id()));
            Check(ana.Decks.Single(d => d.Id == deck.Id) is { Sleeve: "oro", Dice: "obsidiana" }, "Guardar con un cliente anterior conserva la mica y el dado");
            await Call(ana, r => r.SaveDeckV2(deck.Id, "Dobles", "yuno", custom, traps, bench, "no-existe", "clasico", Id()), rejected: true);
            await Call(ana, r => r.SaveDeckV2(deck.Id, "Dobles", "yuno", custom, traps, bench, "clasica", "no-existe", Id()), rejected: true);
            Check(ana.Decks.Single(d => d.Id == deck.Id) is { Sleeve: "oro", Dice: "obsidiana" }, "Micas y dados inexistentes se rechazan sin tocar el mazo");

            // Ana brings her deck; Beto has none and receives the training deck. A hand shows only five cards,
            // so several deals are needed before a training deck dealt by mistake could not go unnoticed.
            var betoCards = new HashSet<string>();
            for (var deal = 0; deal < 10; deal++)
            {
                await Call(ana, r => r.CreateRoom("Ana"));
                await Until(() => ana.Room != null);
                await Call(beto, r => r.JoinRoom(ana.Room!.Code, "Beto"));
                await Until(() => ana.Room!.Dealt && beto.Room?.Dealt == true);
                var room = ana.Room!.Code;
                // Editing after the deal must not reach the match in progress.
                await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", CardCatalog.TrainingDeck(), [], [], Id()));
                foreach (var peer in new[] { ana, beto })
                    await Call(peer, r => r.ChooseInitialSkill(room, "magnet-shuffle", Id()));
                await Until(() => ana.Hand.Count == 5 && beto.Hand.Count == 5);
                if (!ana.Hand.All(custom.Contains) || ana.Hand[0] != "magnet-shuffle")
                    throw new Exception("La mano inicial no salió del mazo activo: " + string.Join(", ", ana.Hand));
                betoCards.UnionWith(beto.Hand);
                await Call(ana, r => r.LeaveRoom());
                await Until(() => ana.Room == null && beto.Room == null);
                await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, bench, Id()));
            }
            Check(true, "La mano inicial sale del mazo activo aunque se edite después de repartir");
            Check(betoCards.Any(card => !custom.Contains(card)) && betoCards.All(CardCatalog.TrainingDeck().Contains),
                "Sin mazo activo se reparte el mazo de entrenamiento");

            // The room keeps the Relikario and style of the dealt deck through preparation and into the duel.
            {
                var osamu = CardCatalog.TrainingDeck().Where(id => DeckRules.Compatible(CardCatalog.Get(id), "osamu"))
                    .Concat([CardCatalog.TurboSword.Id, CardCatalog.TurboSword.Id, CardCatalog.TurboOverlap.Id]).ToList();
                await Call(ana, r => r.SaveDeckV2(deck.Id, "Dobles", "osamu", osamu, traps, [], "oro", "obsidiana", Id()));
                Check(ana.Decks.Single(d => d.Id == deck.Id) is { Valid: true, Active: true, Relikario: "osamu" },
                    "El servidor acepta y conserva activo un mazo legal de Osamu");
                await Call(ana, r => r.CreateRoom("Ana"));
                await Until(() => ana.Room != null);
                await Call(beto, r => r.JoinRoom(ana.Room!.Code, "Beto"));
                await Until(() => ana.Room!.Dealt && beto.Room?.Dealt == true);
                var room = ana.Room!.Code; var seat = (int)ana.Room.MySeat;
                await Until(() => new[] { ana, beto }.All(p => p.Connection.Db.MyRoomRelikarios.Iter().SingleOrDefault() is
                    { RelikarioOne: "osamu", RelikarioTwo: "yuno" }));
                Check(true, "Osamu y Yuno se ven en la preparación antes de elegir sus habilidades");
                await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom, traps, bench, Id()));
                await Call(ana, r => r.ChooseInitialSkill(room, CardCatalog.TurboOverlap.Id, Id()));
                await Call(beto, r => r.ChooseInitialSkill(room, CardCatalog.Magnet.Id, Id()));
                await Until(() => ana.Hand.Count == 5 && beto.Hand.Count == 5);
                foreach (var peer in new[] { ana, beto }) await Call(peer, r => r.ConfirmPreparation(room, [], Id()));
                await Until(() => ana.Duel != null && beto.Duel != null);
                foreach (var view in new[] { ana.Duel!, beto.Duel! })
                    Check(view.Players[seat] is { RelikarioId: "osamu", Health: 10, Dexterity: 0, Sleeve: "oro", Dice: "obsidiana" } &&
                        view.Players[1 - seat] is { RelikarioId: "yuno", Health: 8, Sleeve: CosmeticCatalog.DefaultSleeve, Dice: CosmeticCatalog.DefaultDice },
                        "Ambos jugadores ven los Relikarios y estilos del reparto aunque se edite un mazo después");
                var duel = ana.Duel!;
                await Call(ana, r => r.PlayDuelV2(room, duel.Revision, Id(), duel.Options.FindIndex(o => o.Move.Kind == DuelMoveKind.Concede), []));
                await Until(() => ana.Duel?.Winner != null || ana.Room == null);
                await Call(ana, r => r.LeaveRoom());
                await Until(() => ana.Room == null && beto.Room == null);
            }

            await Call(ana, r => r.SaveDeck(deck.Id, "Dobles", "yuno", custom.Skip(1).ToList(), traps, [], Id()));
            await Until(() => ana.Decks.All(d => !d.Active));
            Check(!ana.Decks.Single(d => d.Id == deck.Id).Valid, "Editar el mazo activo hasta dejarlo ilegal lo desactiva");

            foreach (var saved in ana.Decks.ToList()) await Call(ana, r => r.DeleteDeck(saved.Id));
            await Until(() => ana.Decks.Count == 0);
            await Call(ana, r => r.DeleteDeck(deck.Id));
            Check(true, "Borrar elimina los mazos y repetir el borrado no falla");

            // Development builds admit anonymous players with no name of their own until they choose one.
            Check(beto.Connection.Db.MyAccount.Iter().Single().Name == "", "Conectar crea la cuenta del jugador");
            await Call(beto, r => r.SetDisplayName("  Beto Real  "));
            await Call(beto, r => r.SetDisplayName(" "), rejected: true);
            await Until(() => beto.Connection.Db.MyAccount.Iter().Single().Name == "Beto Real");
            await Call(beto, r => r.CreateRoom("Impostor"));
            await Until(() => beto.Room != null);
            Check(beto.Room!.PlayerOne == "Beto Real", "La sala muestra el nombre de la cuenta, no el que envía el cliente");
            await Call(beto, r => r.LeaveRoom());
            await Until(() => beto.Room == null);

            for (var i = 0; i < 20; i++) await Call(beto, r => r.SaveDeck(0, "Mazo " + i, "yuno", custom, traps, [], Id()));
            await Call(beto, r => r.SaveDeck(0, "Sobra", "yuno", custom, traps, [], Id()), rejected: true);
            await Until(() => beto.Decks.Count == 20);
            Check(true, "Cada jugador guarda hasta 20 mazos");
            foreach (var saved in beto.Decks.ToList()) await Call(beto, r => r.DeleteDeck(saved.Id));

            // Deleting an account erases its decks and leaves nothing to play with until a new sign-in.
            await Call(ana, r => r.SaveDeck(0, "Se va conmigo", "yuno", custom, traps, [], Id()));
            await Until(() => ana.Decks.Count == 1);
            await Call(ana, r => r.CreateRoom("Ana"));
            await Until(() => ana.Room != null);
            await Call(ana, r => r.DeleteAccount(), rejected: true);
            Check(ana.Error!.Contains("Cierra tu sala") && ana.Decks.Count == 1, "No se puede eliminar la cuenta con una sala abierta");
            await Call(ana, r => r.LeaveRoom());
            await Until(() => ana.Room == null);
            await Call(beto, r => r.DeleteAccountOf(ana.Connection.Identity!.Value), rejected: true);
            Check(ana.Decks.Count == 1, "Un jugador no puede eliminar la cuenta de otro");
            await Call(ana, r => r.DeleteAccount());
            await Until(() => ana.Decks.Count == 0 && !ana.Connection.Db.MyAccount.Iter().Any());
            await Call(ana, r => r.SaveDeck(0, "Fantasma", "yuno", custom, traps, [], Id()), rejected: true);
            await Call(ana, r => r.CreateRoom("Ana"), rejected: true);
            await Call(ana, r => r.DeleteAccount(), rejected: true);
            Check(ana.Error!.Contains("Inicia sesión"), "Eliminar la cuenta borra sus mazos y deja de admitir sus solicitudes");

            // The game's own connection wrapper, as the deck screens use it.
            var service = new Relick.Network.LobbyConnection();
            async Task Served(Func<bool> done)
            {
                var timer = Stopwatch.StartNew();
                while (!done())
                {
                    service.Tick();
                    if (timer.Elapsed.TotalSeconds > 20) throw new Exception("Tiempo agotado en la conexión del juego: " + service.DeckError);
                    await Task.Delay(5);
                }
            }
            try
            {
                service.Connect(server, database, null);
                await Served(() => service.Ready);
                var revision = service.DeckRevision;
                service.SaveDeck(0, " Desde el juego ", "yuno", custom, traps, [], "violeta", "menta");
                Check(service.DeckPending, "El juego marca la solicitud de mazo como pendiente");
                await Served(() => !service.DeckPending && service.Decks.Count == 1);
                Check(service.DeckSaves == 1 && service.DeckError == "" && service.DeckRevision > revision && service.Decks[0] is { Name: "Desde el juego", Sleeve: "violeta", Dice: "menta" },
                    "El juego guarda un mazo con su estilo y lo recibe de vuelta");
                service.SetActiveDeck(service.Decks[0].Id);
                await Served(() => !service.DeckPending && service.Decks[0].Active);
                service.SaveDeck(service.Decks[0].Id, "", "yuno", custom, traps, [], "violeta", "menta");
                await Served(() => !service.DeckPending);
                Check(service.DeckSaves == 1 && service.DeckError.Contains("nombre de mazo"), "El juego muestra el motivo de un guardado rechazado");
                service.DeleteDeck(service.Decks[0].Id);
                await Served(() => !service.DeckPending && service.Decks.Count == 0);
                Check(service.DeckError == "", "El juego activa y borra mazos");
            }
            finally { service.Dispose(); }

            Console.WriteLine($"{checks} comprobaciones de mazos correctas.");
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
        public List<DeckInfo> Decks => LobbyConnection.WithStyles(Connection.Db);
        public RoomInfo? Room => Connection.Db.MyRoom.Iter().SingleOrDefault();
        public List<string> Hand => Connection.Db.MyHand.Iter().OrderBy(card => card.Position).Select(card => card.Definition).ToList();
        public DuelSnapshot? Duel => Connection.Db.MyDuel.Iter().SingleOrDefault() is { } row ? JsonSerializer.Deserialize(row.Json, DuelJson.Default.DuelSnapshot) : null;
    }
}
