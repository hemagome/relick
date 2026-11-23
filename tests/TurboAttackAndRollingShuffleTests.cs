using Relick.Core;

static class TurboAttackAndRollingShuffleTests
{
    private static readonly string Turbo = CardCatalog.TurboAttack.Id;
    private static readonly string Rolling = CardCatalog.RollingShuffle.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Bonfire = CardCatalog.Bonfire.Id;
    private static readonly string ExtraLife = CardCatalog.ExtraLife.Id;
    private static readonly string Konamy = CardCatalog.KonamyCode.Id;
    private static readonly string Bomb = CardCatalog.TimeBomb.Id;
    private static readonly string Error = CardCatalog.LoadError.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Turbo Attack: una tirada aparte decide par o impar, el crítico es fijo y el premio es 400 PR", () =>
        {
            foreach (var (attack, damageDie, damage) in new[] { (4, 3, 2), (5, 4, 2), (5, 3, 3), (6, 0, 5), (2, 0, 0) })
            {
                var (game, random) = Fixture(Turbo, [Attack]);
                var visuals = DuelVisualEvents.Capture(game);
                random.EnqueueDie(attack);
                if (damageDie > 0) random.EnqueueDie(damageDie);
                Check(game.Attack(0, 0, 0).Success && game.State.Phase == Phase.Combat);
                Check(game.State.Players[1].Health == 8 - damage && game.State.Active.Relikia == (damage == 0 ? 0 : 400));
                Check(game.State.LastAttackDie == attack && visuals.Count(cue => cue.Kind == DuelVisualKind.Die) == (damageDie == 0 ? 1 : 2));
            }
        }),
        ("Turbo Attack: equipa la copia buscada con Osamu Lv 2 aunque se haya gastado la última acción", () =>
        {
            var (game, random) = Fixture(Turbo, [Bonfire], [Bonfire, ExtraLife], phase: Phase.Actions);
            game.State.Active.Relikario = RelikarioCatalog.OsamuLv2;
            game.State.ActionsLeft = 1;
            Check(game.UseOsamuSearch(0).Success && Pick(game, 0).Success);
            Check(game.State.PendingDecision?.Source == Turbo && game.State.ActionsLeft == 0);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(DuelSnapshot.For(game, 1, 0).Options.All(option => option.Move.Kind != DuelMoveKind.Choose));
            Check(game.DecisionChoices(0).Where(choice => choice.CardId != null).Single().Value == 1);
            Check(Pick(game, 1).Success && game.State.Active.Equipment == Bonfire && game.State.Active.Hand.SequenceEqual([Bonfire]));
            Check(game.State.ActionsLeft == 0 && game.State.Active.SearchActivationUsed && game.State.SearchActivation == null);
            game.State.Active.Hand.Add(Konamy);
            game.State.ActionsLeft = 1;
            random.EnqueueDie(1);
            Check(game.PlayCode(0, 1).Success && game.State.Phase == Phase.RecycleChoice);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.PendingDecision == null && game.State.Active.Hand.Contains(ExtraLife));
        }),
        ("Turbo Attack: si Error de Carga anula la carta encontrada, reanuda el ataque de Osamu sin devolver una acción", () =>
        {
            var (game, random) = Fixture(Turbo, [Attack], [Bomb, ExtraLife]);
            game.State.Players[1].Hand.Add(Error);
            game.State.ActionsLeft = 0;
            random.EnqueueDie(5);
            random.EnqueueDie(4);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingDecision?.Source == RelikarioCatalog.Osamu.Id);
            Check(Pick(game, 0).Success && game.State.PendingDecision?.Source == Turbo);
            Check(Pick(game, 0).Success && game.State.Phase == Phase.ErrorResponse && game.State.ActivationChain.Single().ActionsPaid == 0);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.PlayCode(1, 0).Success && game.State.Phase == Phase.Combat && game.State.SearchActivation == null);
            Check(game.State.Active.Memory.SequenceEqual([Attack, Bomb]) && game.State.Active.Relikia == 400 &&
                game.State.ActionsLeft == 0 && game.State.Players[1].Health == 6);
        }),
        ("Turbo Attack: Ellie ofrece los dos resultados y pasar conserva el uso para una búsqueda posterior", () =>
        {
            var (game, _) = Fixture(Turbo, [CardCatalog.Ellie.Id, Konamy], [ExtraLife, Bonfire, ExtraLife, CardCatalog.DiamondHelmet.Id, Attack],
                phase: Phase.Actions);
            Check(game.PlayCode(0, 0).Success && Pick(game, 0).Success && Pick(game, 0).Success);
            Check(game.State.PendingDecision?.Source == Turbo && game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual([ExtraLife, Bonfire, null]));
            Check(Pick(game, int.MinValue).Success && !game.State.Active.SearchActivationUsed);
            Check(game.PlayCode(0, 0).Success && Pick(game, 1).Success && game.State.PendingDecision?.Source == Turbo);
            Check(Pick(game, 2).Success && game.State.Active.Equipment == CardCatalog.DiamondHelmet.Id && game.State.ActionsLeft == 0);
            Check(game.State.Active.Hand.SequenceEqual([ExtraLife, Bonfire]));
        }),
        ("Turbo Attack: un robo normal no ofrece activar las cartas robadas", () =>
        {
            var sniper = CardCatalog.Sniper.Id;
            var (game, random) = Fixture(Turbo, [Konamy], [CardCatalog.QuickLoad.Id, sniper, Bonfire, ExtraLife, Bomb], phase: Phase.Actions);
            random.EnqueueDie(5);
            Check(game.PlayCode(0, 0).Success && Pick(game, 0).Success && Pick(game, 0).Success);
            Check(game.State.PendingDecision == null && game.State.Active.Hand.SequenceEqual([sniper, Bonfire, ExtraLife]));
            Check(game.State.ActionsLeft == 1);
        }),
        ("Rolling Shuffle: los resultados 1 a 5 de la acción aplican una sola vez sus efectos", () =>
        {
            for (var die = 1; die <= 5; die++)
            {
                var (game, random) = Fixture(Rolling, [], phase: Phase.Actions);
                var rival = game.State.Players[1];
                rival.Health = 7;
                rival.Memory.AddRange([Attack, ExtraLife, Bonfire, Attack, Attack, Attack]);
                game.State.ActionsLeft = 1;
                var move = DuelOptions.For(game, 0).Single(option => option.Move.Kind == DuelMoveKind.SkillAction).Move;
                random.EnqueueDie(die);
                Check(!move.Apply(game, 1).Success && move.Apply(game, 0).Success && game.State.ActionsLeft == 0);
                Check(game.State.Phase == Phase.Actions && !move.Apply(game, 0).Success);
                Check(game.State.Active.Health == (die == 1 ? 7 : 8) && game.State.Active.Hand.Count == (die == 2 ? 2 : 0));
                Check(rival.Health == (die == 3 ? 8 : 7) && game.State.Active.Relikia == (die == 4 ? 400 : 0));
                Check(rival.Hand.SequenceEqual(die == 5 ? [ExtraLife] : Array.Empty<string>()) && rival.Memory.Count == (die == 5 ? 5 : 6));
            }
            var (shortMemory, shortRandom) = Fixture(Rolling, [], phase: Phase.Actions);
            shortRandom.EnqueueDie(5);
            shortMemory.State.Players[1].Memory.Add(Attack);
            Check(shortMemory.UseSkillAction(0, 0).Success && shortMemory.State.Players[1].Memory.SequenceEqual([Attack]));
        }),
        ("Rolling Shuffle: el 6 limita la elección privada a las primeras seis y conserva el orden del resto", () =>
        {
            string[] deck = [Bonfire, Attack, Attack, Attack, Attack, ExtraLife, Konamy, Bomb];
            var (game, random) = Fixture(Rolling, [], deck, phase: Phase.Actions);
            random.EnqueueDie(6);
            Check(game.UseSkillAction(0, 0).Success && game.DecisionChoices(0).Count == 6 && !Pick(game, 6).Success);
            Check(Pick(game, 5).Success && game.State.Active.Hand.Count == 0);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(DuelSnapshot.For(game, 1, 0).Options.All(option => option.Move.Kind != DuelMoveKind.Choose));
            Check(!Pick(game, 5).Success && Pick(game, 0).Success);
            Check(game.State.Active.Hand.SequenceEqual([ExtraLife, Bonfire]) && game.State.Active.Deck.SequenceEqual([Attack, Attack, Attack, Attack, Konamy, Bomb]));
        }),
        ("Rolling Shuffle: crítico y premio usan D6 separados, los fallos no tiran premio", () =>
        {
            foreach (var (attack, critical, prize, expectedDamage) in new[] { (4, 0, 1, 1), (5, 0, 4, 2), (6, 2, 5, 3), (2, 0, 0, 0) })
            {
                var (game, random) = Fixture(Rolling, [Attack]);
                var visuals = DuelVisualEvents.Capture(game);
                random.EnqueueDie(attack);
                if (critical > 0) random.EnqueueDie(critical);
                if (prize > 0) random.EnqueueDie(prize);
                Check(game.Attack(0, 0, 0).Success);
                if (game.State.Phase == Phase.AttackRerollChoice) Check(game.ResolveAttackReroll(0, false).Success);
                Check(game.State.Phase == Phase.Combat && game.State.PendingAttack == null);
                Check(game.State.Players[1].Health == 8 - expectedDamage && game.State.LastAttackDie == attack);
                Check(game.State.Active.Relikia == (prize > 0 ? (prize + 1) * 100 : 0));
                Check(visuals.Count(cue => cue.Kind == DuelVisualKind.Die) == 1 + (critical > 0 ? 1 : 0) + (prize > 0 ? 1 : 0));
            }
        }),
    ];

    private static CommandResult Pick(GameEngine game, int value) => game.ResolveDecision(0,
        game.DecisionChoices(0).ToList().FindIndex(choice => choice.Value == value));

    private static (GameEngine Game, ScriptedRandom Random) Fixture(string skill, string[] hand, string[]? deck = null, Phase phase = Phase.Combat)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[.. hand], []], Decks = [[.. deck ?? [Bonfire, Bonfire, Bonfire]], [Attack, Attack]],
            Relikarios = [skill == Turbo ? RelikarioCatalog.Osamu.Id : RelikarioCatalog.Yuno.Id, RelikarioCatalog.Yuno.Id],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = phase;
        game.State.Turn = 2;
        game.State.Active.Dexterity = 0; game.State.Players[1].Dexterity = 0;
        game.State.Active.Skills.Add(skill);
        return (game, random);
    }

    private static void Check(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!condition) throw new Exception($"Regla de Turbo Attack o Rolling Shuffle no satisfecha en la línea {line}.");
    }
}
