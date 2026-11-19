using Relick.Core;

static class DestinyPickTests
{
    private static readonly string Pick = CardCatalog.DestinyPick.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Púa del Destino: gastar una acción y elegir exclusivamente robar", () =>
        {
            var (game, _) = Fixture();
            var player = game.State.Active;
            Check(game.PlayCode(0, 0).Success && player.ActionsLeft == 1 && player.Memory.Single() == Pick);
            Check(game.DecisionChoices(0).Count == 3 && game.DecisionChoices(1).Count == 0);
            Check(Choose(game, 0, "Robar 1 carta") && player.Hand.Single() == Attack &&
                game.State.Players[1].Health == 8 && player.CorruptedMemory.Count == 0 && game.State.Phase == Phase.Actions);
        }),
        ("Púa del Destino: desde memoria cuesta una acción sin PR y el coste se conserva al reconectar", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            player.Hand.Clear();
            player.Memory.AddRange([Attack, Pick, Pick]);
            game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            game.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            player.ActionsLeft = 1;
            Check(!game.UseDestinyPickFromMemory(1, 0).Success && !game.UseDestinyPickFromMemory(0, 0).Success);
            var options = DuelOptions.For(game, 0).Where(o => o.Move.Kind == DuelMoveKind.DestinyPickMemory).ToArray();
            Check(game.HasAvailablePlay(0) && options.Select(o => o.Move.Index).SequenceEqual([1, 2]));
            Check(options[1].Move.Apply(game, 0).Success && player.ActionsLeft == 0 && player.Relikia == 0 &&
                player.CorruptedMemory.Single() == Pick && player.Memory.SequenceEqual([Attack, Pick]));
            Check(game.State.Phase == Phase.Decision && !game.PlayCode(1, 0).Success);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(Choose(game, 0, "Robar 1 carta") && game.State.Active.Hand.Single() == Attack &&
                !game.UseDestinyPickFromMemory(0, 1).Success);
        }),
        ("Púa del Destino: corrompe distintas clases de carta de ambas memorias", () =>
        {
            var (game, random) = Fixture();
            var player = game.State.Active;
            var rival = game.State.Players[1];
            player.Memory.Add(CardCatalog.Bonfire.Id);
            rival.Memory.AddRange([Attack, CardCatalog.Destiny.Id]);
            Check(game.PlayCode(0, 0).Success && Choose(game, 0, "Corromper 1 carta"));
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.DecisionChoices(0).Select(c => (c.Owner, c.CardId)).SequenceEqual(
                new (int?, string?)[] { (0, CardCatalog.Bonfire.Id), (0, Pick), (1, Attack), (1, CardCatalog.Destiny.Id) }));
            var target = DuelOptions.For(game, 0).Single(o => o.Owner == 1 && o.CardId == CardCatalog.Destiny.Id);
            Check(target.Move.Apply(game, 0).Success);
            rival = game.State.Players[1];
            Check(rival.CorruptedMemory.Single() == CardCatalog.Destiny.Id && rival.Memory.Single() == Attack &&
                game.State.Active.Memory.SequenceEqual([CardCatalog.Bonfire.Id, Pick]));
        }),
        ("Púa del Destino: el daño puede producir el primer cero y nunca da el golpe definitivo", () =>
        {
            foreach (var (life, hadReachedZero, expected) in new[] { (2, false, 1), (1, false, 0), (1, true, 1), (0, true, 0) })
            {
                var (game, _) = Fixture();
                var rival = game.State.Players[1];
                rival.Health = life;
                rival.HasReachedZero = hadReachedZero;
                Check(game.PlayCode(0, 0).Success && Choose(game, 0, "Hacer 1 daño no letal") &&
                    rival.Health == expected && !game.State.IsOver && rival.HasReachedZero == (hadReachedZero || expected == 0));
            }
            var (blocked, _) = Fixture();
            blocked.State.Players[1].Equipment = CardCatalog.DiamondHelmet.Id;
            Check(blocked.PlayCode(0, 0).Success && Choose(blocked, 0, "Hacer 1 daño no letal") &&
                blocked.State.Players[1].Health == 8);

            var (modified, _) = Fixture();
            modified.State.Active.StarEffects = 1;
            var defender = modified.State.Players[1];
            defender.Health = 2;
            defender.HasReachedZero = true;
            defender.AttachedCards.Add(CardCatalog.Robotize.Id);
            Check(modified.PlayCode(0, 0).Success && Choose(modified, 0, "Hacer 1 daño no letal") &&
                defender.Health == 1 && !modified.State.IsOver);
        }),
        ("Púa del Destino: el COMBO de mano se puede usar al defender y luego continúa el ataque", () =>
        {
            var (game, random) = Fixture();
            var attacker = game.State.Active;
            attacker.Hand.Clear();
            attacker.Hand.Add(Attack);
            attacker.Skills.Add(CardCatalog.Destiny.Id);
            game.State.Players[1].Hand.Add(Pick);
            game.State.Phase = Phase.Combat;
            random.EnqueueDie(5);
            Check(game.Attack(0, 0, 0).Success && game.State.Phase == Phase.DefenseChoice);
            Check(game.PlayCode(1, 0).Success && Choose(game, 1, "Hacer 1 daño no letal") &&
                attacker.Health == 7 && game.State.Phase == Phase.DefenseChoice && game.State.PendingAttack != null);
            Check(game.PassDefense(1).Success && game.State.Players[1].Health == 5 &&
                attacker.Relikia == CardCatalog.Destiny.Reward && game.State.Phase == Phase.Combat);
        }),
        ("Púa del Destino: Error de Carga y Caja de Cartón pueden anular el Código desde la mano", () =>
        {
            foreach (var box in new[] { false, true })
            {
                var (game, _) = Fixture();
                var rival = game.State.Players[1];
                if (box) rival.Equipment = CardCatalog.CardboardBox.Id;
                else rival.Hand.Add(CardCatalog.LoadError.Id);
                Check(game.PlayCode(0, 0).Success && game.State.Phase == (box ? Phase.CodeResponse : Phase.ErrorResponse));
                Check((box ? game.ResolveCodeResponse(1, true) : game.PlayCode(1, 0)).Success &&
                    game.State.Active.ActionsLeft == 2 && game.State.Active.Memory.Single() == Pick &&
                    game.State.PendingDecision == null && game.State.Phase == Phase.Actions);
            }
        })
    ];

    private static bool Choose(GameEngine game, int actor, string label) =>
        DuelOptions.For(game, actor).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Label == label).Move.Apply(game, actor).Success;

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[Pick], []], Decks = [[Attack, Attack], [Attack, Attack]],
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

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Regla de Púa del Destino incumplida.");
    }
}
