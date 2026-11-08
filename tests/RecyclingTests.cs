using Relick.Core;

static class RecyclingTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Agotamiento: D6 1–6 exige elegir exactamente ese número de la memoria y conserva las otras cartas", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            for (var die = 1; die <= 6; die++)
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active; var count = Count(g); rng.EnqueueDie(die);
                Check(g.PlayCode(actor, 0).Success && p.Recycled && g.State.Phase == Phase.RecycleChoice);
                Check(p.Hand.Count == 1 && p.Deck.Count == 0 && g.RecyclingCount == die);
                var choice = DuelOptions.For(g, actor).Single(o => o.Move.Kind == DuelMoveKind.Recycle);
                Check(choice.RequiredDiscards == die && DuelOptions.For(g, 1 - actor).All(o => o.Move.Kind != DuelMoveKind.Recycle));
                var memory = p.Memory.ToArray(); var selected = Enumerable.Range(memory.Length - die, die).ToArray();
                choice.Move.Discards = selected;
                Check(choice.Move.Apply(g, actor).Success && Count(g) == count);
                Check(p.Memory.SequenceEqual(memory.Take(memory.Length - die)));
                var returned = p.Hand.Skip(1).Concat(p.Deck).Order();
                Check(returned.SequenceEqual(selected.Select(i => memory[i]).Order()));
                Check(die == 1 ? g.State.IsOver : g.State.Phase == Phase.Actions);
            }
        }),
        ("Agotamiento: rechaza jugador, índices, duplicados y cantidades inválidas sin mutación ni azar", () =>
        {
            var (g, rng) = Fixture(); rng.EnqueueDie(3); Check(g.PlayCode(0, 0).Success);
            var random = new DuelRandom(); g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            random.Record(_ => throw new Exception("Una selección inválida consume azar.")); var before = g.SaveSnapshot();
            Check(!g.ResolveRecycling(1, [0, 1, 2]).Success && !g.ResolveRecycling(-1, [0, 1, 2]).Success);
            foreach (var invalid in new[] { Array.Empty<int>(), new[] { 0, 1 }, new[] { 0, 1, 2, 3 }, new[] { 0, 0, 1 }, new[] { -1, 1, 2 }, new[] { 0, 1, 99 } })
                Check(!g.ResolveRecycling(0, invalid).Success && before == g.SaveSnapshot());
            Check(random.Finish().Count == 0 && !g.Advance(0).Success && !g.ResolveDieReroll(0, false).Success);
        }),
        ("Agotamiento: elegir copias distintas, barajar solo el mazo y reanudar tras recuperar el estado", () =>
        {
            var (g, rng) = Fixture(); rng.EnqueueDie(3); Check(g.PlayCode(0, 0).Success);
            var pending = g.SaveSnapshot(); var random = new DuelRandom(); g = GameEngine.FromSnapshot(pending, random);
            var shuffles = 0; g.CardsReturnedToDeck += _ => shuffles++;
            random.Record(_ => 0); Check(g.ResolveRecycling(0, [4, 1, 3]).Success); var values = random.Finish();
            Check(values.Count == 2 && shuffles == 1 && g.State.Phase == Phase.Actions && g.State.Active.Hand.Count == 2);
            var replayRandom = new DuelRandom(); replayRandom.Replay(values);
            var restored = GameEngine.FromSnapshot(pending, replayRandom);
            Check(restored.ResolveRecycling(0, [3, 4, 1]).Success); replayRandom.Finish();
            Check(g.SaveSnapshot() == restored.SaveSnapshot());
            var before = restored.SaveSnapshot(); Check(!restored.ResolveRecycling(0, [0, 1, 2]).Success && before == restored.SaveSnapshot());
        }),
        ("Agotamiento: devuelve todas si la memoria tiene menos cartas y excluye las corruptas", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Memory.Clear();
            p.Memory.Add(CardCatalog.Attack.Id); p.CorruptedMemory.Add(CardCatalog.ExtraLife.Id); rng.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && g.RecyclingCount == 2);
            Check(g.ResolveRecycling(0, [1, 0]).Success && p.Memory.Count == 0 && p.Deck.Count == 1 && p.Hand.Count == 2);
            Check(p.CorruptedMemory.Single() == CardCatalog.ExtraLife.Id && !g.State.IsOver);
        }),
        ("Agotamiento: aun sin memoria se lanza D6 y el siguiente robo vacío resuelve el segundo agotamiento", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1]; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
            p.Hand.Clear(); p.Hand.Add(CardCatalog.Attack.Id); p.Memory.Clear(); var rolls = new List<int>(); g.DieRolled += rolls.Add;
            g.State.Active.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 10));
            rng.EnqueueDie(6); Check(g.Advance(0).Success);
            Check(p.Recycled && !g.State.IsOver && rolls.SequenceEqual(new[] { 6 }) && p.Deck.Count == 0);
            Check(g.Advance(1).Success && g.Advance(1).Success && g.Advance(0).Success && g.Advance(0).Success);
            Check(g.State.IsOver && p.Health == 6 && rolls.Count == 1);
        }),
        ("Agotamiento: reponer Bonus rival cede la elección al dueño y retoma el turno original", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1]; p.Bonus = CardCatalog.Attack.Id;
            p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id); p.Memory.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 6));
            g.State.Active.Hand.Clear(); g.State.Active.Hand.Add(CardCatalog.TimeBomb.Id);
            Check(g.PlayCode(0, 0).Success); rng.EnqueueDie(6);
            Check(g.ResolveDestruction(0, 1, FieldZone.Bonus).Success && g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(DuelSnapshot.For(g, 1, 1).Stage == Phase.Actions && g.ResolveRecycling(1, [0, 1, 2, 3, 4, 5]).Success);
            Check(g.State.Phase == Phase.Actions && g.State.CurrentPlayer == 0 && g.State.Players[1].Deck.Count == 6);
        }),
        ("Agotamiento: el segundo resta dos vidas inevitables y compara incluso si ambas quedan en cero", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var (own, rival, winner) in new[] { (8, 5, 0), (8, 6, -1), (8, 8, 1), (0, 0, -1), (1, 0, -1), (0, 1, 1), (3, 0, 0) })
            {
                var (g, _) = Fixture(actor); var p = g.State.Active; p.Recycled = true; p.Health = own; p.HasReachedZero = true;
                p.Equipment = CardCatalog.DiamondHelmet.Id; g.State.Players[1 - actor].Health = rival;
                var dice = 0; g.DieRolled += _ => dice++; var cues = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(actor, 0).Success && g.State.IsOver && p.Health == Math.Max(0, own - 2));
                Check(g.State.Winner == (winner < 0 ? -1 : winner == 0 ? actor : 1 - actor) && dice == 0);
                Check(cues.Single(c => c.Kind == DuelVisualKind.Hit) is { Value: 2, AttackDie: 0 });
            }
        }),
        ("Relikia: consumir su última carta con un COMBO durante reciclaje no deja una elección sin cartas", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2;
            p.Hand.Clear(); p.Deck.Clear(); p.Deck.Add(CardCatalog.Reroll.Id); p.Relikia = 300;
            rng.EnqueueDie(4); Check(g.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success && g.State.Phase == Phase.DieRerollChoice);
            rng.EnqueueDie(4); Check(g.PlayCode(0, 0).Success && p.Hand.Count == 0);
            Check(g.ResolveDieReroll(0, false).Success && g.ResolveRecycling(0, [0, 1, 2, 3]).Success);
            Check(g.State.Phase == Phase.Actions && g.State.PendingRelikia == null && g.State.Active.Hand.Count == 0);
        }),
        ("Agotamiento: el robo de Re-lanza espera a reciclar antes de intentar robar del mazo vacío", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Add(CardCatalog.Reroll.Id);
            rng.EnqueueDie(2); Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            rng.EnqueueDie(3); Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.RecycleChoice && !g.State.IsOver);
            Check(p.Hand.Count == 1 && g.RecyclingCount == 3);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveRecycling(0, [0, 1, 2]).Success && g.State.Phase == Phase.Actions);
            Check(g.State.Active.Hand.Count == 3 && g.State.Active.Deck.Count == 1 && g.State.Active.Health == 8);
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = new GameEngine(rng); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = actor;
        foreach (var player in g.State.Players) player.Dexterity = 0;
        var p = g.State.Active; p.Hand.Add(CardCatalog.Senshuni.Id); p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
        p.Memory.AddRange(new[] { CardCatalog.Magnet.Id, CardCatalog.Attack.Id, CardCatalog.ExtraLife.Id,
            CardCatalog.Attack.Id, CardCatalog.Attack.Id, CardCatalog.DiamondHelmet.Id });
        return (g, rng);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Deck.Count + p.Hand.Count + p.Memory.Count + p.CorruptedMemory.Count);
    static void Check(bool condition) { if (!condition) throw new Exception("Regla de agotamiento no satisfecha."); }
}
