using Relick.Core;

static class SuperMagnetTests
{
    public static (string, Action)[] Cases =>
    [
        ("Súper Imán: canje normal, creación opcional y reconexión", () =>
        {
            Check(CardCatalog.SuperMagnet.CodeType == CodeType.Trap && !CardCatalog.SuperMagnet.IsCombo);
            Check(!CardCatalog.TrainingDeck().Contains(CardCatalog.SuperMagnet.Id) && new GameEngine(1).State.Active.SuperMagnetCopies == 1);
            foreach (var actor in new[] { 0, 1 })
            {
                var (game, random) = Fixture(actor);
                Trade(game, actor, CardCatalog.Attack, CardCatalog.Pendant);
                var state = game.State; var owner = state.Active;
                Check(owner.ActionsLeft == 0 && owner.Relikia == 0 && owner.Equipment == null);
                Check(owner.Memory.SequenceEqual([CardCatalog.Attack.Id, CardCatalog.Pendant.Id]) && owner.Exchange.Count == 0);
                Check(owner.Hand.Single() == CardCatalog.Senshuni.Id && owner.Bonus == CardCatalog.Attack.Id);
                Check(state.Phase == Phase.TrapCraftChoice && state.DecisionPlayer == actor && !game.Advance(actor).Success);
                Check(DuelOptions.For(game, 1 - actor).All(o => o.Move.Kind != DuelMoveKind.TrapCraft));
                var saved = game.SaveSnapshot();
                Check(!game.ResolveTrapCraft(1 - actor, true).Success && game.SaveSnapshot() == saved);
                var restored = GameEngine.FromSnapshot(saved, random);
                Check(DuelOptions.For(restored, actor).Single(o => o.Move.Kind == DuelMoveKind.TrapCraft && o.Move.Flag).Move.Apply(restored, actor).Success);
                owner = restored.State.Active;
                Check(owner.Equipment == CardCatalog.SuperMagnet.Id && owner.EquipmentCounters == 0 && owner.SuperMagnetCopies == 0);
                Check(restored.State.Phase == Phase.Actions && owner.ActionsLeft == 0 && owner.Relikia == 0);
                Check(game.ResolveTrapCraft(actor, false).Success && game.State.Active.SuperMagnetCopies == 1);
                Trade(game, actor, CardCatalog.Pendant, CardCatalog.DiamondHelmet);
                Check(game.State.Phase == Phase.TrapCraftChoice);
            }
        }),
        ("Súper Imán: dos no-equipos o un duelo anterior no crean trampa", () =>
        {
            var (game, _) = Fixture();
            Trade(game, 0, CardCatalog.Attack, CardCatalog.GoldenTotem);
            Check(game.State.Phase == Phase.Actions && game.State.Active.SuperMagnetCopies == 1);
            (game, _) = Fixture(copies: null);
            Trade(game, 0, CardCatalog.Pendant, CardCatalog.Attack);
            Check(game.State.Phase == Phase.Actions && game.State.Active.SuperMagnetCopies == 0);
        }),
        ("Súper Imán: el registro de un duelo nuevo reconstruye la creación desde la apertura", () =>
        {
            var opening = new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = 0, Hands = [[CardCatalog.Pendant.Id, CardCatalog.Attack.Id], []],
                Decks = [[CardCatalog.Attack.Id, CardCatalog.Attack.Id], []],
                Bonuses = [CardCatalog.Senshuni.Id, null], SuperMagnetCopies = [1, 1]
            };
            var moves = new DuelMove[]
            {
                new() { Kind = DuelMoveKind.Advance },
                new() { Kind = DuelMoveKind.Exchange, Index = 0 },
                new() { Kind = DuelMoveKind.Exchange, Index = 0 },
                new() { Kind = DuelMoveKind.TrapCraft, Flag = true }
            };
            var (game, _) = DuelReplay.Restore(opening, moves.Select(move => new DuelEntry { Actor = 0, Move = move }));
            Check(game.State.Phase == Phase.Actions && game.State.Active.Equipment == CardCatalog.SuperMagnet.Id);
            Check(game.State.Active.SuperMagnetCopies == 0 && game.State.Players[1].SuperMagnetCopies == 1);
        }),
        ("Súper Imán: espera a que Cartucho termine el canje y ambos robos", () =>
        {
            var (game, random) = Fixture();
            game.State.Active.Hand.AddRange([CardCatalog.ReloadCartridge.Id, CardCatalog.Attack.Id]);
            var rival = game.State.Players[1];
            rival.Hand.Add(CardCatalog.Sniper.Id);
            rival.Exchange.Add(CardCatalog.Attack.Id);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.SniperChoice);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveSniper(1, false).Success);
            Check(game.State.Phase == Phase.TrapCraftChoice && game.State.DecisionPlayer == 1 && game.State.CurrentPlayer == 0);
            Check(game.State.Players[0].Hand.Count == 4 && game.State.Players[1].Hand.Count == 5 && game.State.PendingReload == null);
            Check(game.ResolveTrapCraft(1, true).Success && game.State.Players[1].Equipment == CardCatalog.SuperMagnet.Id);
        }),
        ("Súper Imán: espera al reciclaje del Bonus antes de ofrecer la creación", () =>
        {
            var (game, random) = Fixture();
            var owner = game.State.Active;
            owner.Deck.RemoveRange(1, owner.Deck.Count - 1);
            random.EnqueueDie(1);
            Trade(game, 0, CardCatalog.Pendant, CardCatalog.Attack);
            Check(game.State.Phase == Phase.RecycleChoice && owner.Bonus == CardCatalog.Attack.Id);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.Phase == Phase.TrapCraftChoice);
            Check(game.ResolveTrapCraft(0, true).Success && game.State.Active.Equipment == CardCatalog.SuperMagnet.Id);
        }),
        ("Súper Imán: sustituye efectos de destrucción y de robo del Bonus, y sale al tercer contador", () =>
        {
            var (game, random) = Fixture();
            Trade(game, 0, CardCatalog.Pendant, CardCatalog.Attack);
            Check(game.ResolveTrapCraft(0, true).Success);
            var owner = game.State.Players[0]; var rival = game.State.Players[1];
            owner.Exchange.Add(CardCatalog.Bonfire.Id);
            var bonus = owner.Bonus; var deckCount = owner.Deck.Count;
            game.State.CurrentPlayer = 1; rival.Relikia = 200;
            rival.Hand.AddRange([CardCatalog.BlueEgg.Id, CardCatalog.TimeBomb.Id, CardCatalog.Attack.Id]);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Exchange, 0).Success);
            Check(owner.Exchange.Single() == CardCatalog.Bonfire.Id && owner.EquipmentCounters == 1 && rival.Relikia == 200);
            Check(game.ResolveBlueEggDraw(1, false).Success);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Equipment).Success);
            Check(owner.Equipment == CardCatalog.SuperMagnet.Id && owner.EquipmentCounters == 2);

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            owner = game.State.Players[0]; rival = game.State.Players[1];
            game.State.Phase = Phase.Combat;
            rival.Skills.Add(CardCatalog.Magnet.Id);
            rival.AttackRerollUsedThisTurn = true;
            random.EnqueueDie(6);
            Check(game.Attack(1, 0, 0).Success && game.State.Phase == Phase.MagnetChoice);
            Check(owner.Health == 3 && owner.EquipmentCounters == 2);
            var takeBonus = DuelOptions.For(game, 1).Single(o => o.Move.Kind == DuelMoveKind.Magnet && o.Move.Flag);
            Check(takeBonus.Move.Apply(game, 1).Success && owner.Bonus == bonus && owner.Deck.Count == deckCount && rival.Hand.Count == 0);
            Check(owner.Equipment == null && owner.EquipmentCounters == 0 && !owner.Memory.Contains(CardCatalog.SuperMagnet.Id));
            Check(game.State.Phase == Phase.Combat && rival.Relikia == 200 + CardCatalog.Magnet.Reward);
            var details = DuelHistoryText.Lines(game.PublicHistory!.Entries.Last(), seat => game.State.Players[seat].Name);
            Check(details.Any(line => line.Contains("3/3")) && details.Any(line => line.Contains("fuera del juego")));
            game.State.CurrentPlayer = 0; game.State.Phase = Phase.Actions;
            Trade(game, 0, CardCatalog.Bonfire, CardCatalog.Attack);
            Check(game.State.Phase == Phase.Actions);
        }),
        ("Súper Imán: mano y efectos propios siguen su curso", () =>
        {
            var (game, random) = Fixture();
            Trade(game, 0, CardCatalog.Pendant, CardCatalog.Attack);
            Check(game.ResolveTrapCraft(0, true).Success);
            var owner = game.State.Active;
            game.State.CurrentPlayer = 1;
            game.State.Active.Hand.Add(CardCatalog.TimeBomb.Id);
            random.EnqueueDie(1);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Hand).Success);
            Check(owner.Hand.Count == 0 && owner.EquipmentCounters == 0);
            game.State.CurrentPlayer = 0; owner.ActionsLeft = 1;
            owner.Hand.Add(CardCatalog.Bonfire.Id);
            Check(game.Equip(0, 0).Success && owner.Equipment == CardCatalog.Bonfire.Id);
            Check(!owner.Memory.Contains(CardCatalog.SuperMagnet.Id) && owner.SuperMagnetCopies == 0);
        }),
        ("Súper Imán: la sustitución del equipo y la destrucción al tercer contador respetan Reverse", () =>
        {
            var (game, random) = Fixture();
            var owner = game.State.Active;
            owner.Equipment = CardCatalog.Pc.Id; owner.EquipmentDie = 6; owner.Skills.Add(CardCatalog.Reverse.Id);
            Trade(game, 0, CardCatalog.Pendant, CardCatalog.Attack);
            Check(game.ResolveTrapCraft(0, true).Success && game.State.Phase == Phase.DestructionResponse);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random); owner = game.State.Active;
            Check(game.ResolveDestructionProtection(0).Success && owner.Equipment == CardCatalog.SuperMagnet.Id && owner.EquipmentDie == 0);

            owner.EquipmentCounters = 2; var bonus = owner.Bonus;
            game.State.CurrentPlayer = 1; game.State.Active.Hand.Add(CardCatalog.TimeBomb.Id);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Bonus).Success);
            Check(game.State.Phase == Phase.DestructionResponse && game.State.PendingDestruction?.Target.CardId == CardCatalog.SuperMagnet.Id);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random); owner = game.State.Players[0];
            Check(game.ResolveDestructionProtection(0, 0).Success && owner.Equipment == CardCatalog.SuperMagnet.Id && owner.Bonus == bonus);
            Check(owner.EquipmentCounters == 3 && owner.Skills.Count == 0 && game.State.Phase == Phase.Actions);
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(int actor = 0, int? copies = 1)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = actor, Hands = [[], []],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList()],
            Bonuses = [CardCatalog.Senshuni.Id, CardCatalog.Senshuni.Id],
            SuperMagnetCopies = copies is int count ? [count, count] : null
        }, random);
        game.State.Turn = 2;
        game.State.Phase = Phase.Actions;
        return (game, random);
    }

    private static void Trade(GameEngine game, int actor, CardDefinition first, CardDefinition second)
    {
        var player = game.State.Players[actor];
        player.ActionsLeft = 2;
        var index = player.Hand.Count;
        player.Hand.AddRange([first.Id, second.Id]);
        Check(game.Exchange(actor, index).Success && game.Exchange(actor, index).Success);
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("Resultado inesperado al resolver Súper Imán.");
    }
}
