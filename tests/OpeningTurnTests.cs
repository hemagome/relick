using Relick.Core;

static class OpeningTurnTests
{
    public static (string Name, Action Run)[] Cases =>
    [
        ("Inicio: permite enseñar, bloquea atacar sin gastar cartas ni azar y habilita al rival y al iniciador después", TurnSequence),
        ("Inicio: repetir Combate y reconectar mantienen la prohibición de atacar", RepeatedCombat),
        ("Inicio: la preparación local también bloquea el primer ataque", LocalStart)
    ];

    static GameEngine Opening(int seat, IGameRandom random, bool repeat = false)
    {
        var hands = new List<string>[]
        {
            [CardCatalog.Destiny.Id, CardCatalog.Attack.Id],
            [CardCatalog.Destiny.Id, CardCatalog.Attack.Id]
        };
        if (repeat) hands[seat].AddRange([CardCatalog.TimeBender.Id, CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id]);
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = seat, Hands = hands,
            Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
        }, random);
        foreach (var p in game.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
        if (game.State.Phase == Phase.Draw) Check(game.Advance(seat).Success);
        return game;
    }

    static void Blocked(GameEngine game)
    {
        var actor = game.State.CurrentPlayer;
        var before = game.SaveSnapshot();
        var cues = DuelVisualEvents.Capture(game);
        Check(!game.CanAttack(actor));
        Check(DuelOptions.For(game, actor).All(o => o.Move.Kind != DuelMoveKind.Attack));
        var result = new DuelMove { Kind = DuelMoveKind.Attack, Index = game.State.Active.Hand.IndexOf("attack"), Value = 0 }.Apply(game, actor);
        Check(!result.Success && result.Message.Contains("primer turno"));
        Check(game.SaveSnapshot() == before && cues.Count == 0);
    }

    static void TurnSequence()
    {
        foreach (var seat in new[] { 0, 1 })
        {
            // An unexpected roll during rejection would fail immediately.
            var random = new DuelRandom(); random.Replay([]);
            var game = Opening(seat, random);
            var learn = DuelOptions.For(game, seat).Single(o => o.Move.Kind == DuelMoveKind.Learn);
            Check(game.HasAvailablePlay(seat) && learn.Move.Apply(game, seat).Success);
            Blocked(game);
            Check(!game.HasAvailablePlay(seat));
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Blocked(game);
            random.Finish();
            Check(game.Advance(seat).Success && game.Advance(seat).Success);
            Check(game.State.Turn == 2 && game.State.CurrentPlayer == 1 - seat);
            Check(game.Learn(1 - seat, 0).Success);
            Check(game.CanAttack(1 - seat) && game.HasAvailablePlay(1 - seat));
            var attack = DuelOptions.For(game, 1 - seat).First(o => o.Move.Kind == DuelMoveKind.Attack);
            random.Replay([4]); Check(attack.Move.Apply(game, 1 - seat).Success); random.Finish();
            if (game.State.Phase == Phase.AttackRerollChoice) Check(game.ResolveAttackReroll(1 - seat, false).Success);
            if (game.State.Phase == Phase.DefenseChoice) Check(game.PassDefense(seat).Success);
            Check(game.State.Players[seat].Health == 5);
            Check(game.Advance(1 - seat).Success && game.Advance(1 - seat).Success);
            Check(game.State.Turn == 3 && game.State.CurrentPlayer == seat && game.CanAttack(seat));
            attack = DuelOptions.For(game, seat).First(o => o.Move.Kind == DuelMoveKind.Attack);
            random.Replay([4]); Check(attack.Move.Apply(game, seat).Success); random.Finish();
            if (game.State.Phase == Phase.AttackRerollChoice) Check(game.ResolveAttackReroll(seat, false).Success);
            if (game.State.Phase == Phase.DefenseChoice) Check(game.PassDefense(1 - seat).Success);
            Check(game.State.Players[1 - seat].Health == 5);
        }
    }

    static void RepeatedCombat()
    {
        foreach (var seat in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([]);
            var game = Opening(seat, random, repeat: true);
            Check(game.Learn(seat, 0).Success);
            var p = game.State.Active;
            Check(game.PlayCode(seat, p.Hand.IndexOf(CardCatalog.TimeBender.Id)).Success);
            Check(game.ResolveTurnBuffDiscard(seat, p.Hand.IndexOf(CardCatalog.Senshuni.Id)).Success);
            Check(game.ResolveTurnBuffDiscard(seat, p.Hand.IndexOf(CardCatalog.ExtraLife.Id)).Success);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.Advance(seat).Success && game.State.Phase == Phase.Combat && game.State.Turn == 1);
            Blocked(game);
            random.Finish();
        }
    }

    static void LocalStart()
    {
        var game = TestGame.Create(15);
        Check(game.Start([]).Success && game.State.Turn == 1);
        if (game.State.Phase == Phase.Draw) Check(game.Advance(game.State.CurrentPlayer).Success);
        var p = game.State.Active;
        Check(game.Learn(game.State.CurrentPlayer, p.Hand.IndexOf(CardCatalog.Magnet.Id)).Success);
        if (!p.Hand.Contains("attack")) p.Hand.Add("attack");
        Blocked(game);
    }

    static void Check(bool value) { if (!value) throw new Exception("Regla de ataque del turno inicial incorrecta."); }
}
