using Relick.Core;

static class SecretRoomTests
{
    private static readonly string Room = CardCatalog.SecretRoom.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Dodge = CardCatalog.Dodge.Id;
    private static readonly string Life = CardCatalog.ExtraLife.Id;
    private static readonly string Bonfire = CardCatalog.Bonfire.Id;
    private static readonly string SuperAttack = CardCatalog.SuperAttack.Id;
    private static readonly string SuperMagnet = CardCatalog.SuperMagnet.Id;
    private static readonly string Zahira = CardCatalog.ZahiraFlair.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Habitación: entrar al canje recupera una carta de la memoria propia", () =>
        {
            var (game, random) = Fixture([Room]);
            var player = game.State.Active;
            player.Memory.AddRange([Attack, Dodge]);
            player.CorruptedMemory.Add(Life);
            game.State.Players[1].Memory.Add(Bonfire);
            Check(!game.PlayCode(0, 0).Success && game.Exchange(0, 0).Success && player.ActionsLeft == 1 &&
                player.Exchange.SequenceEqual([Room]) && game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual([Attack, Dodge]),
                "La carta se activa por su llegada y solo ofrece la memoria propia sin corromper.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(!game.ResolveDecision(1, 0).Success && Pick(game, Dodge) && game.State.Active.Hand.SequenceEqual([Dodge]) &&
                game.State.Active.Memory.SequenceEqual([Attack]) && game.State.Active.Bonus == Life && game.State.Phase == Phase.Actions,
                "La elección persiste y recupera exactamente una carta.");

            (game, _) = Fixture([Room]);
            Check(game.Exchange(0, 0).Success && game.State.Phase == Phase.Actions && game.State.Active.Hand.Count == 0,
                "Con la memoria vacía el efecto termina sin bloquear el turno.");
        }),
        ("Habitación: dos copias resuelven el canje completo, luego memoria y nuevo Bonus", () =>
        {
            var (game, random) = Fixture([Dodge, Room], [Room, Attack], [SuperAttack, SuperMagnet]);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success &&
                game.State.Active.Bonus == Room && game.State.Active.Exchange.Count == 0 && game.State.Active.Hand.SequenceEqual([Life]),
                "El Bonus anterior se entrega antes de recuperar y la nueva Habitación queda en Bonus.");
            Check(game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual([Dodge, Room]), "La recuperación incluye el canje recién completado.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(Pick(game, Dodge) && game.State.Active.Relikia == 3000 &&
                game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual([SuperMagnet, SuperAttack]) &&
                DuelOptions.For(game, 1).All(option => option.Move.Kind != DuelMoveKind.Choose), "La recompensa se conserva al retomar la cola y mantiene privado el mazo de Trampas.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(Pick(game, SuperAttack) && game.State.Active.Hand.SequenceEqual([Life, Dodge, SuperAttack]) &&
                game.State.Active.Relikia == 3000 && game.State.Active.SuperMagnetCopies == 1 && game.State.Phase == Phase.Actions,
                "Resuelve una sola recompensa y gasta la Trampa elegida.");
        }),
        ("Habitación: Robotizar dispara la llegada al canje desde el mazo", () =>
        {
            var (game, _) = Fixture([], [Room, Attack]);
            game.State.Phase = Phase.Combat;
            game.State.Active.AttachedCards.Add(CardCatalog.Robotize.Id);
            game.State.Active.Memory.Add(Dodge);
            Check(game.Advance(0).Success && game.State.PendingDecision?.Source == CardCatalog.Robotize.Id,
                "Robotizar ofrece llevar Habitación al canje.");
            var send = game.DecisionChoices(0).ToList().FindIndex(choice => choice.Value == 0);
            Check(game.ResolveDecision(0, send).Success && game.State.Active.Exchange.SequenceEqual([Room]) &&
                game.State.Active.Hand.SequenceEqual([Dodge]) && game.State.Active.ActionsLeft == 2 && game.State.Phase == Phase.Actions,
                "La llegada desde el mazo también recupera la carta sin pagar una acción adicional.");
        }),
        ("Habitación: los PR se ganan al reponer por un canje, incluso sin Trampas", () =>
        {
            var (game, _) = Fixture([Dodge, Dodge], [Room, Attack]);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Active.Bonus == Room &&
                game.State.Active.Relikia == 3000 && game.State.Phase == Phase.Actions, "El mazo de Trampas vacío no impide ganar PR.");

            (game, _) = Fixture([Dodge, Dodge], [Attack, Attack], bonus: Room);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Active.Hand.SequenceEqual([Room]) &&
                game.State.Active.Relikia == 0, "Reclamar una Habitación que ya estaba en el Bonus no dispara la llegada.");

            (game, _) = Fixture([], [Room, Attack]);
            game.State.Active.Relikia = 100;
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ChangeBonus).Success && game.State.Active.Bonus == Room &&
                game.State.Active.Relikia == 0, "Una reposición ordinaria por Relikia no es un canje de dos cartas.");
        }),
        ("Habitación: usa los destinos impresos de Súper Imán y Zahira Flair sin exigir receta", () =>
        {
            foreach (var trap in new[] { SuperMagnet, Zahira })
            {
                var (game, _) = Fixture([Dodge, Dodge], [Room, Attack], [trap]);
                Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Active.Relikia == 3000,
                    "Dos cartas sin receta permiten sacar la Trampa por efecto.");
                if (trap == SuperMagnet)
                    Check(game.State.Active is { Equipment: var equipment, SuperMagnetCopies: 0 } && equipment == SuperMagnet,
                        "Súper Imán se coloca en la zona de equipos.");
                else
                {
                    var host = game.DecisionChoices(0).ToList().FindIndex(choice => choice.Field is { Zone: FieldZone.Relikario, Owner: 0 });
                    Check(game.ResolveDecision(0, host).Success && game.State.Active.Zahira(0) is { Zone: FieldZone.Relikario, Owner: 0 },
                        "Zahira Flair permite escoger la carta que cubrirá.");
                }
                Check(game.State.Phase == Phase.Actions, "El turno retoma sus acciones al terminar la colocación.");
            }
        }),
        ("Habitación: la colocación directa y una receta pueden gastar dos copias distintas", () =>
        {
            var (game, _) = Fixture([Attack, Attack], [Room, Attack], [SuperAttack, SuperAttack]);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Phase == Phase.TrapCraftChoice &&
                game.State.Active.Hand.SequenceEqual([Life, SuperAttack]) && game.ResolveTrapCraft(0, create: true).Success &&
                game.State.Active.Hand.SequenceEqual([Life, SuperAttack, SuperAttack]) && !game.State.Active.SuperAttackAvailable,
                "La receta sigue disponible después de la colocación por efecto si queda una copia.");
        }),
        ("Habitación: reanuda la reposición del Bonus después de guardar durante el reciclaje", () =>
        {
            var (game, random) = Fixture([Dodge, Dodge], [Room]);
            random.EnqueueDie(1);
            Check(game.Exchange(0, 0).Success && game.Exchange(0, 0).Success && game.State.Phase == Phase.RecycleChoice,
                "El último naipe como Bonus inicia el agotamiento.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.Active.Bonus == Room && game.State.Active.Relikia == 3000 &&
                game.State.Phase == Phase.Actions, "El contexto del canje se conserva y concede el premio una sola vez.");
        })
    ];

    private static bool Pick(GameEngine game, string id) => game.ResolveDecision(0,
        game.DecisionChoices(0).ToList().FindIndex(choice => choice.CardId == id)).Success;

    private static (GameEngine Game, ScriptedRandom Random) Fixture(List<string> hand, List<string>? deck = null,
        List<string>? traps = null, string? bonus = null)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [hand, []], Decks = [deck ?? [Attack, Attack], [Attack, Attack]],
            Bonuses = [bonus ?? Life, null], Traps = [traps ?? [], []]
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Active.Dexterity = 0;
        return (game, random);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
