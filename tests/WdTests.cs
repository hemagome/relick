using Relick.Core;

static class WdTests
{
    private static readonly string Attack = CardCatalog.AttackWd.Id, Bala = CardCatalog.Bullet.Id, Bonfire = CardCatalog.Bonfire.Id;
    private static readonly string Wild = CardCatalog.WildDrive.Id, Multi = CardCatalog.MultiDrive.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("WD: evoluciona con cuatro descartes y 1600 PR, conserva las elecciones y cura hasta 11 vidas", () =>
        {
            var (game, random) = Fixture([Bala, Bonfire, Bonfire, Bonfire, Attack]);
            var player = game.State.Active;
            Check(player.Relikario.IsVillain && player.Health == 11 && player.Dexterity == 0);
            player.Health = 9;
            player.Relikia = 1599;
            Check(!game.Evolve(0).Success);
            player.Relikia = 1600;
            Check(game.Evolve(0).Success && Pick(game, 0).Success && Pick(game, -1).Success);
            Check(player.Hand.Count == 5 && player.Relikia == 1600 && player.Memory.Count == 0);
            Check(game.Evolve(0).Success && Pick(game, 0).Success && !Pick(game, 0).Success && Pick(game, 2).Success);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            var rival = DuelSnapshot.For(game, 1, 0);
            Check(rival.Options.All(option => option.Move.Kind != DuelMoveKind.Choose) && rival.Players[0].Hand.All(id => id == null));
            Check(Pick(game, 1).Success && Pick(game, 3).Success && game.State.Phase == Phase.Combat);
            player = game.State.Active;
            Check(player.Relikario == RelikarioCatalog.WdLv2 && player.Health == 11 && player.Relikia == 0 && !player.HasNaturalSix);
            Check(player.Hand.SequenceEqual([Attack, Bonfire]) && player.Memory.Count == 4 && player.Memory.Contains(Bala));
        }),
        ("WD Lv 1: descarta solo Bala o Ataque, se detiene en 5 y Wild Drive puede enviar Bala a canje", () =>
        {
            var (game, random) = Fixture([Attack, Bala, Bonfire, Attack], skill: Wild);
            random.EnqueueDie(4);
            Check(game.Attack(0, 0, 0).Success && game.UseWdBoost(0).Success);
            Check(game.DecisionChoices(0).Select(choice => choice.Value).SequenceEqual([0, 2, -1]));
            Check(Pick(game, 0).Success && game.State.PendingDecision?.Source == Wild);
            Check(DuelSnapshot.For(game, 1, 0).Stage == Phase.Combat);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(Pick(game, 0).Success && game.State.Active.Exchange.SequenceEqual([Bala]));
            Check(game.State.Active.Hand.SequenceEqual([Bonfire, Attack, Bonfire]) && game.State.LastAttackDie == 5);
            Check(!game.UseWdBoost(0).Success && game.State.Active.Relikia == 400 && game.State.Players[1].Health == 5);
            game.State.DiscardsRemaining = 1;
            game.State.Phase = Phase.Discard;
            Check(game.Discard(0, 0).Success && game.State.PendingDecision == null);
            Check(game.State.Active.Memory.Contains(Bonfire) && game.State.Active.Exchange.SequenceEqual([Bala]));
        }),
        ("WD Lv 2: descarta cualquier carta para aumentar un dado de efecto y suma +1 automático al ataque", () =>
        {
            var (game, random) = Fixture([CardCatalog.ExtraLife.Id, Bonfire, Attack], skill: Wild, levelTwo: true);
            game.State.Phase = Phase.Actions;
            game.State.Active.Health = 5;
            random.EnqueueDie(2);
            Check(game.PlayCode(0, 0).Success && game.UseWdBoost(0).Success);
            Check(Pick(game, 0).Success && Pick(game, -1).Success);
            Check(game.State.Phase == Phase.DieRerollChoice && game.State.LastDie == 3);
            Check(game.ResolveDieReroll(0, false).Success && game.State.Active.Health == 8);
            game.State.Phase = Phase.Combat;
            random.EnqueueDie(4);
            Check(game.Attack(0, 0, 0).Success && game.State.LastAttackDie == 5 && game.State.Players[1].Health == 5);
        }),
        ("Multi Drive: paga cada relanzamiento, cuenta la mano al acertar y redondea el daño normal", () =>
        {
            var hand = new[] { Attack }.Concat(Enumerable.Repeat(Bonfire, 7)).ToArray();
            var (game, random) = Fixture(hand, skill: Multi);
            random.EnqueueDie(2);
            random.EnqueueDie(3);
            random.EnqueueDie(5);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingDecision?.Source == Multi);
            Check(Pick(game, 0).Success && game.State.PendingDecision?.Source == Multi);
            Check(Pick(game, 0).Success && game.State.Phase == Phase.Combat && game.State.LastAttackDie == 5);
            Check(game.State.Active.Hand.Count == 5 && game.State.Active.Relikia == 500 && game.State.Players[1].Health == 5);
            Check(game.State.Active.Memory.SequenceEqual([Attack, Bonfire, Bonfire]));
        }),
        ("WD: el robo de Bala puede abrir reciclaje sin perder el ataque ni el aumento pendiente", () =>
        {
            var (game, random) = Fixture([Attack, Bala], skill: Wild, deckSize: 1);
            random.EnqueueDie(4);
            random.EnqueueDie(1);
            Check(game.Attack(0, 0, 0).Success && game.UseWdBoost(0).Success && Pick(game, 0).Success);
            Check(Pick(game, -1).Success && game.State.Phase == Phase.RecycleChoice);
            Check(DuelSnapshot.For(game, 0, 0).Stage == Phase.Combat);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.Phase == Phase.Combat && game.State.LastAttackDie == 5);
            Check(game.State.Active.Hand.SequenceEqual([Bonfire]) && game.State.Active.Memory.SequenceEqual([Bala]));
        }),
        ("Wild Drive: Habitación Secreta se resuelve al entrar en canje durante el pago de WD", () =>
        {
            var room = CardCatalog.SecretRoom.Id;
            var (game, random) = Fixture([Attack, room], skill: Wild, levelTwo: true);
            game.State.Active.Memory.Add(Bonfire);
            random.EnqueueDie(3);
            Check(game.Attack(0, 0, 0).Success && game.UseWdBoost(0).Success && Pick(game, 0).Success);
            Check(Pick(game, 0).Success && game.State.PendingDecision?.Source == room);
            Check(Pick(game, 0).Success && game.State.Phase == Phase.AttackRerollChoice);
            Check(game.State.LastAttackDie == 5 && game.State.Active.Hand.SequenceEqual([Bonfire]));
        }),
        ("WD: Multi Drive y Wild Drive calculan sus críticos y premios con las cartas de cada zona", () =>
        {
            foreach (var skill in new[] { Wild, Multi })
            {
                var hand = new[] { Attack }.Concat(Enumerable.Repeat(Bonfire, 5)).ToArray();
                var (game, random) = Fixture(hand, skill: skill);
                game.State.Active.Memory.AddRange([Attack, Attack, CardCatalog.AttackWdNs.Id, CardCatalog.AttackWdDt.Id]);
                game.State.Active.CorruptedMemory.Add(Attack);
                game.State.Players[1].Memory.Add(Attack);
                random.EnqueueDie(6);
                Check(game.Attack(0, 0, 0).Success && game.State.Players[1].Health == 3);
                Check(game.State.Active.Relikia == (skill == Wild ? 400 : 500));
            }
        }),
        ("Bala: el descarte de cada jugador roba una carta, incluso sin WD", () =>
        {
            var (game, _) = Fixture([CardCatalog.ReloadCartridge.Id, Bala, Bala], deckSize: 8);
            game.State.Active.Relikario = RelikarioCatalog.Yuno;
            game.State.Players[1].Hand.Add(Bala);
            game.State.Phase = Phase.Actions;
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.Actions);
            Check(game.State.Players[0].Hand.Count == 6 && game.State.Players[1].Hand.Count == 5);
            Check(game.State.Players[0].Memory.Count(id => id == Bala) == 2 && game.State.Players[1].Memory.SequenceEqual([Bala]));
        }),
        ("Wild Drive: dos copias enseñadas pueden enviar dos cartas de un descarte simultáneo a canje", () =>
        {
            var (game, _) = Fixture([CardCatalog.ReloadCartridge.Id, Bala, Bonfire], skill: Wild);
            game.State.Active.Skills.Add(Wild);
            game.State.Players[1].Hand.Add(Attack);
            game.State.Phase = Phase.Actions;
            Check(game.PlayCode(0, 0).Success && game.State.PendingDecision?.Source == Wild);
            Check(Pick(game, 0).Success && game.DecisionChoices(0).First().CardId == Bonfire);
            Check(Pick(game, 0).Success && game.State.Phase == Phase.Actions);
            Check(game.State.Active.Exchange.SequenceEqual([Bala, Bonfire]) && game.State.Active.Hand.Count == 5);
        }),
        ("Bala: se puede canjear sin robar y la activación desde la mano está deshabilitada", () =>
        {
            var (game, _) = Fixture([Bala]);
            game.State.Phase = Phase.Actions;
            game.State.Active.Bonus = Bonfire;
            Check(!game.PlayCode(0, 0).Success && game.Exchange(0, 0).Success);
            Check(game.State.Active.Hand.Count == 0 && game.State.Active.Exchange.SequenceEqual([Bala]) && game.State.Active.Deck.Count == 8);
        }),
    ];

    private static CommandResult Pick(GameEngine game, int value) => game.ResolveDecision(0,
        game.DecisionChoices(0).ToList().FindIndex(choice => choice.Value == value));

    private static (GameEngine Game, ScriptedRandom Random) Fixture(string[] hand, string? skill = null, bool levelTwo = false, int deckSize = 8)
    {
        var random = new ScriptedRandom();
        var deck = Enumerable.Repeat(Bonfire, deckSize).ToArray();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[.. hand], []], Decks = [[.. deck], [.. deck]],
            Relikarios = [RelikarioCatalog.Wd.Id, RelikarioCatalog.Yuno.Id],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Turn = 2;
        game.State.Phase = Phase.Combat;
        if (levelTwo) game.State.Active.Relikario = RelikarioCatalog.WdLv2;
        if (skill != null) game.State.Active.Skills.Add(skill);
        return (game, random);
    }

    private static void Check(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!condition) throw new Exception($"Regla de WD no satisfecha en la línea {line}.");
    }
}
