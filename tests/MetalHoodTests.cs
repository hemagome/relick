using Relick.Core;

static class MetalHoodTests
{
    private static readonly string Hood = CardCatalog.MetalHood.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Capirote Metálico: elige quién lo lleva y el jugador que lo pone paga la acción", () =>
        {
            foreach (var onOpponent in new[] { false, true })
            {
                var (game, _) = Fixture();
                var actor = game.State.Players[0];
                var wearer = onOpponent ? game.State.Players[1] : actor;
                actor.Hand.Add(CardCatalog.Pendant.Id);
                Check(!game.Equip(0, 1, onOpponent: true).Success && actor.Hand.Count == 2 && actor.ActionsLeft == 2);
                var options = DuelOptions.For(game, 0).Where(o => o.Move.Kind == DuelMoveKind.Equip).ToArray();
                Check(options.Count(o => o.Move.Index == 0) == 2 && options.Count(o => o.Move.Index == 1) == 1);
                Check(options.Single(o => o.Move.Index == 0 && o.Move.Flag == onOpponent).Move.Apply(game, 0).Success);
                Check(wearer.Equipment == Hood && actor.ActionsLeft == 1 && actor.Memory.Count == 0);
                Check(game.AvailableActions(0) == (onOpponent ? 1 : 0) && game.AvailableActions(1) == (onOpponent ? 1 : 2));
                Check(DuelSnapshot.For(game, 1, 1).Players[onOpponent ? 1 : 0].Actions == (onOpponent ? 1 : 0));
            }
        }),
        ("Capirote Metálico: reemplaza el equipo rival tras reconectar y conserva su titularidad", () =>
        {
            var (game, random) = Fixture();
            var rival = game.State.Players[1];
            rival.Equipment = CardCatalog.Pendant.Id;
            rival.Hand.Add(CardCatalog.LoadError.Id);
            Check(game.Equip(0, 0, onOpponent: true).Success && game.State.Phase == Phase.ErrorResponse);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.PassErrorResponse(1).Success);
            rival = game.State.Players[1];
            Check(rival.Equipment == Hood && rival.Memory.Single() == CardCatalog.Pendant.Id && rival.ForeignFieldCards!.Single() == Hood);
            var actor = game.State.Players[0];
            Check(actor.Memory.Count == 0);
            rival.Hand.Clear();
            game.State.CurrentPlayer = 1;
            RollAttack(game, random, 1);
            Check(rival.Equipment == null && actor.Memory.Single() == Hood && rival.ForeignFieldCards == null &&
                game.AvailableActions(1) == 2);
        }),
        ("Capirote Metálico: Error de Carga, Reverse y Súper Imán pueden impedir el equipamiento rival", () =>
        {
            var (canceled, _) = Fixture();
            canceled.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(canceled.Equip(0, 0, onOpponent: true).Success && canceled.PlayCode(1, 0).Success);
            Check(canceled.State.Players[1].Equipment == null && canceled.State.Active.Memory.Single() == Hood &&
                canceled.AvailableActions(0) == 2);

            var (protectedEquip, _) = Fixture();
            var rival = protectedEquip.State.Players[1];
            rival.Equipment = CardCatalog.Pendant.Id;
            rival.Skills.Add(CardCatalog.Reverse.Id);
            Check(protectedEquip.Equip(0, 0, onOpponent: true).Success && protectedEquip.State.Phase == Phase.DestructionResponse);
            Check(protectedEquip.ResolveDestructionProtection(1, 0).Success && rival.Equipment == CardCatalog.Pendant.Id &&
                protectedEquip.State.Active.Memory.Single() == Hood);

            var (magnet, _) = Fixture();
            rival = magnet.State.Players[1];
            rival.Equipment = CardCatalog.SuperMagnet.Id;
            rival.EquipmentCounters = CardCatalog.SuperMagnetCounterLimit - 1;
            Check(magnet.Equip(0, 0, onOpponent: true).Success && rival.Equipment == null &&
                magnet.State.Active.Memory.Single() == Hood && magnet.State.Phase == Phase.Actions);
        }),
        ("Capirote Metálico: reduce daño de ataque y de efecto al portador", () =>
        {
            var (game, random) = Fixture();
            Check(game.Equip(0, 0, onOpponent: true).Success);
            var wearer = game.State.Players[1];
            var health = wearer.Health;
            var visuals = DuelVisualEvents.Capture(game);
            RollAttack(game, random, 5);
            Check(wearer.Health == health - 1 && visuals.Any(c => c.Kind == DuelVisualKind.Guard &&
                c.GuardCard == Hood && c.Original == 3 && c.Value == 1));
            game.State.Active.Bonus = CardCatalog.Jessica.Id;
            visuals.Clear();
            random.EnqueueDie(5);
            Check(game.UseFieldCode(0, FieldZone.Bonus).Success && wearer.Health == health - 1 &&
                visuals.Any(c => c.Kind == DuelVisualKind.Guard && c.GuardCard == Hood && c.Value == 0));
        }),
        ("Capirote Metálico: el 1 debe ser un resultado final de quien lo lleva", () =>
        {
            var (game, random) = Fixture();
            Check(game.Equip(0, 0, onOpponent: true).Success);
            var wearer = game.State.Players[1];
            RollAttack(game, random, 1);
            Check(wearer.Equipment == Hood);
            game.State.CurrentPlayer = 1;
            wearer.Hand.Add(CardCatalog.Agility.Id);
            RollAttack(game, random, 1);
            Check(game.State.Phase == Phase.AttackRerollChoice && wearer.Equipment == Hood);
            Check(game.PlayCode(1, 0).Success && wearer.Equipment == Hood && game.State.LastAttackDie == 5 &&
                game.AvailableActions(1) == 0);
            game.State.Phase = Phase.Actions;
            wearer.ActionsLeft = 2;
            wearer.Health = 4;
            wearer.Hand.Add(CardCatalog.ExtraLife.Id);
            random.EnqueueDie(1);
            Check(game.PlayCode(1, 0).Success && wearer.Equipment == null && wearer.Health == 5 &&
                game.AvailableActions(1) == 1 && game.State.Players[0].Memory.Contains(Hood));
        }),
        ("Capirote Metálico: el límite se aplica al gastar acciones y cada etapa repetida", () =>
        {
            var (game, _) = Fixture();
            Check(game.Equip(0, 0).Success);
            var p = game.State.Active;
            p.Hand.AddRange([CardCatalog.Pendant.Id, CardCatalog.ExtraLife.Id]);
            Check(!game.Equip(0, 0).Success && !game.PlayCode(0, 1).Success &&
                !DuelOptions.For(game, 0).Any(o => o.Move.Kind is DuelMoveKind.Equip or DuelMoveKind.PlayCode));
            game.State.ExtraActionStages = 1;
            Check(game.Advance(0).Success && game.State.Phase == Phase.Actions && game.AvailableActions(0) == 1);
            p.Relikia = 1000;
            p.Memory.Add(CardCatalog.ExtraLife.Id);
            Check(!game.CanUseRelikiaEffect(0, RelikiaEffect.ReplayCode));
            Check(game.Equip(0, 0).Success && p.Memory.Contains(Hood) && game.AvailableActions(0) == 1);
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[Hood], []], Decks = [[Attack, Attack], [Attack, Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var player in game.State.Players)
        {
            player.Dexterity = 0;
            player.AttackRerollUsedThisTurn = true;
        }
        return (game, random);
    }

    private static void RollAttack(GameEngine game, ScriptedRandom random, int die)
    {
        game.State.Phase = Phase.Combat;
        game.State.AttackedThisTurn = false;
        var player = game.State.Active;
        player.Hand.Add(Attack);
        player.Skills.Add(CardCatalog.Destiny.Id);
        random.EnqueueDie(die);
        Check(game.Attack(game.State.CurrentPlayer, player.Hand.Count - 1, player.Skills.Count - 1).Success);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Regla de Capirote Metálico incumplida.");
    }
}
