using Relick.Core;

static class SuperAttackTests
{
    public static (string, Action)[] Cases =>
    [
        ("Súper Ataque: dos Ataques permiten crear una copia en mano y la elección sobrevive a la reconexión", () =>
        {
            var (game, random) = Fixture();
            Check(!CardCatalog.TrainingDeck().Contains(CardCatalog.SuperAttack.Id));
            Trade(game, CardCatalog.Attack, CardCatalog.Pendant);
            Check(game.State.Phase == Phase.Actions);
            Trade(game, CardCatalog.Attack, CardCatalog.Attack);
            Check(game.State.Phase == Phase.TrapCraftChoice && DuelSnapshot.For(game, 0, 1).TrapCardId == CardCatalog.SuperAttack.Id);
            Check(DuelOptions.For(game, 1).All(option => option.Move.Kind != DuelMoveKind.TrapCraft));
            Check(game.ResolveTrapCraft(0, false).Success && game.State.Active.SuperAttackAvailable);
            Trade(game, CardCatalog.Attack, CardCatalog.Attack);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            var create = DuelOptions.For(game, 0).Single(option => option.Move.Kind == DuelMoveKind.TrapCraft && option.Move.Flag);
            Check(create.Move.Apply(game, 0).Success && game.State.Phase == Phase.Actions);
            var player = game.State.Active;
            Check(player.Hand.Last() == CardCatalog.SuperAttack.Id && !player.SuperAttackAvailable && player.ActionsLeft == 0);
            Check(DuelSnapshot.For(game, 1, 2).Players[0].Hand.All(id => id == null));
            Trade(game, CardCatalog.Attack, CardCatalog.Attack);
            Check(game.State.Phase == Phase.Actions && player.Hand.Count(id => id == CardCatalog.SuperAttack.Id) == 1);

            (game, _) = Fixture(includeSuperAttack: false);
            Trade(game, CardCatalog.Attack, CardCatalog.Attack);
            Check(game.State.Phase == Phase.Actions && !game.State.Active.SuperAttackAvailable);
        }),
        ("Súper Ataque: aplica el tope al D6, conserva el crítico y añade daño a los aciertos", () =>
        {
            int[] dice = [3, 4, 5, 5, 5, 6];
            int[] damage = [0, 4, 5, 5, 5, 7];
            for (var face = 1; face <= 6; face++)
            {
                var (game, random) = Fixture();
                PrepareAttack(game, CardCatalog.Destiny);
                random.EnqueueDie(face);
                var option = DuelOptions.For(game, 0).Single(candidate => candidate.Move.Kind == DuelMoveKind.Attack);
                Check(option.Move.Apply(game, 0).Success);
                var player = game.State.Active;
                Check(game.State.LastAttackDie == dice[face - 1] && game.State.Players[1].Health == 8 - damage[face - 1]);
                Check(player.HasNaturalSix == (face == 6) && player.Relikia == (face == 1 ? 0 : CardCatalog.Destiny.Reward));
                Check(player.ActionsLeft == 0 && player.Hand.Count == 0 && !player.Memory.Contains(CardCatalog.SuperAttack.Id));
                Check(!game.CanAttack(0) && game.PublicHistory!.Entries.Last().Text.Contains("Súper Ataque"));
            }
        }),
        ("Súper Ataque: el relanzamiento conserva el bono y el dado secundario de Magnet recibe solo el bono de daño", () =>
        {
            var (game, random) = Fixture();
            PrepareAttack(game, CardCatalog.Magnet);
            game.State.Active.AttackRerollUsedThisTurn = false;
            game.State.Active.Dexterity = 1;
            random.EnqueueDie(2);
            Check(game.Attack(0, 0, 0).Success && game.State.Phase == Phase.AttackRerollChoice && game.State.LastAttackDie == 4);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            random.EnqueueDie(3);
            random.EnqueueDie(2);
            Check(game.ResolveAttackReroll(0, true).Success && game.State.LastAttackDie == 5);
            Check(game.State.Phase == Phase.DieRerollChoice && game.State.PendingDieRoll is { Purpose: DiePurpose.MagnetDamage, Die: 2 });
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveDieReroll(0, false).Success && game.State.Players[1].Health == 4);
        }),
        ("Súper Ataque: se puede cancelar su daño con una defensa normal", () =>
        {
            var (game, random) = Fixture();
            PrepareAttack(game, CardCatalog.Destiny);
            var rival = game.State.Players[1];
            rival.Equipment = CardCatalog.CardboardBox.Id;
            random.EnqueueDie(3);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingAttack?.Damage == 5);
            Check(game.CancelAttackWithBox(1).Success && rival.Health == 8);
            Check(game.State.Active.Relikia == CardCatalog.Destiny.Reward && !game.State.Active.Memory.Contains(CardCatalog.SuperAttack.Id));
        }),
        ("Súper Ataque: cuenta como Ataque en otro canje y queda fuera al salir", () =>
        {
            var (game, _) = Fixture();
            Trade(game, CardCatalog.SuperAttack, CardCatalog.Attack);
            Check(game.State.Phase == Phase.TrapCraftChoice && !game.State.Active.Memory.Contains(CardCatalog.SuperAttack.Id));
            Check(game.ResolveTrapCraft(0, true).Success);
            var player = game.State.Active;
            player.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, GameEngine.MaxHandSize));
            Check(game.Advance(0).Success && game.State.Phase == Phase.Final);
            Check(game.Discard(0, player.Hand.IndexOf(CardCatalog.SuperAttack.Id)).Success);
            Check(!player.Memory.Contains(CardCatalog.SuperAttack.Id) && !player.Hand.Contains(CardCatalog.SuperAttack.Id));
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(bool includeSuperAttack = true)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[], []],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
            Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id], IncludeSuperAttack = includeSuperAttack
        }, random);
        game.State.Turn = 2;
        game.State.Phase = Phase.Actions;
        return (game, random);
    }

    private static void Trade(GameEngine game, CardDefinition first, CardDefinition second)
    {
        var player = game.State.Active;
        var index = player.Hand.Count;
        player.Hand.AddRange([first.Id, second.Id]);
        player.ActionsLeft = 2;
        Check(game.Exchange(0, index).Success && game.Exchange(0, index).Success);
    }

    private static void PrepareAttack(GameEngine game, CardDefinition skill)
    {
        Trade(game, CardCatalog.Attack, CardCatalog.Attack);
        Check(game.ResolveTrapCraft(0, true).Success);
        var player = game.State.Active;
        player.Hand.RemoveAt(0); // Remove the ordinary Bonus to test a hand with only the trap.
        player.Skills.Add(skill.Id);
        player.AttackRerollUsedThisTurn = true;
        player.Dexterity = 0;
        game.State.Phase = Phase.Combat;
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Resultado inesperado al resolver Súper Ataque.");
    }
}
