using Relick.Core;

static class HandLimitTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Mano: de cero a siete cartas pasan el turno sin descarte y solo se limita al jugador activo", () =>
        {
            foreach (var seat in new[] { 0, 1 })
            foreach (var size in new[] { 0, 1, 6, 7 })
            {
                var (g, _) = Fixture(seat, size); var p = g.State.Active;
                var rival = g.State.Players[1 - seat]; rival.Hand.AddRange(Enumerable.Repeat("attack", 10));
                Check(g.Advance(seat).Success && g.State.CurrentPlayer == 1 - seat && g.State.Phase == Phase.Combat);
                Check(p.Hand.Count == size && p.Memory.Count == 0 && rival.Hand.Count == 11);
            }
        }),
        ("Mano: opciones privadas, descarte elegido, sin coste y recuperación en etapa final", () =>
        {
            foreach (var seat in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(seat, 9); var p = g.State.Active; p.Hand[3] = "senshuni";
                p.ActionsLeft = 0; p.Relikia = 123;
                Check(g.Advance(seat).Success);
                var view = DuelSnapshot.For(g, seat, 1); var rival = DuelSnapshot.For(g, 1 - seat, 1);
                Check(view.Phase == Phase.Final && view.Stage == Phase.Final && view.DecisionPlayer == seat);
                Check(view.Options.Count(o => o.Move.Kind == DuelMoveKind.Discard) == 9);
                Check(view.Options.All(o => o.Move.Kind is DuelMoveKind.Discard or DuelMoveKind.Concede));
                Check(rival.Options.All(o => o.Move.Kind == DuelMoveKind.Concede) && rival.Players[seat].Hand.All(id => id == null));
                var discard = view.Options.Single(o => o.Move.Kind == DuelMoveKind.Discard && o.Move.Index == 3);
                Check(discard.Move.Apply(g, seat).Success && p.Hand.Count == 8 && p.Memory.Single() == "senshuni");
                Check(p.ActionsLeft == 0 && p.Relikia == 123 && g.State.CurrentPlayer == seat);
                var saved = g.SaveSnapshot(); g = GameEngine.FromSnapshot(saved, rng);
                Check(g.SaveSnapshot() == saved && DuelSnapshot.For(g, seat, 2).Stage == Phase.Final);
                Check(g.Discard(seat, 0).Success && g.State.Players[seat].Hand.Count == 7 && g.State.CurrentPlayer == 1 - seat);
            }
        }),
        ("Mano: último descarte resuelve Sniper y respuestas antes de empezar el siguiente turno", () =>
        {
            foreach (var counter in new[] { false, true })
            {
                var (g, rng) = Fixture(0, 8); var p = g.State.Active; p.Hand[0] = CardCatalog.Sniper.Id;
                p.ActionsLeft = 0; p.Equipment = CardCatalog.Pc.Id; p.EquipmentDie = 5;
                if (counter) g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                Check(g.Advance(0).Success && g.Discard(0, 0).Success);
                Check(g.State.Phase == Phase.SniperChoice && g.State.CurrentPlayer == 0 && p.Hand.Count == 7);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.ResolveSniper(0, true).Success);
                if (counter)
                {
                    Check(g.State.Phase == Phase.ErrorResponse && g.State.CurrentPlayer == 0);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                    Check(g.PlayCode(1, g.State.Players[1].Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
                }
                Check(g.State.CurrentPlayer == 1 && g.State.Phase == Phase.Combat && p.Hand.Count == 7);
                Check(p.Equipment == (counter ? CardCatalog.Pc.Id : CardCatalog.Sniper.Id));
                Check(g.State.PendingSniper == null && g.State.Players[1].Hand.Count == 3);
            }
        }),
        ("Mano: Bonus de un canje por Sniper exige otro descarte incluso después de reciclar", () =>
        {
            foreach (var recycle in new[] { false, true })
            {
                var (g, rng) = Fixture(0, 8); var p = g.State.Active; p.Hand[0] = CardCatalog.Sniper.Id;
                p.ActionsLeft = 0; p.Exchange.Add("attack"); p.Bonus = "senshuni";
                if (recycle) { p.Deck.Clear(); p.Deck.Add("attack"); rng.EnqueueDie(2); }
                Check(g.Advance(0).Success && g.Discard(0, 0).Success && g.ResolveSniper(0, false).Success);
                Check(g.State.CurrentPlayer == 0 && p.Hand.Count == 8 && p.Hand.Contains("senshuni"));
                if (recycle)
                {
                    Check(g.State.Phase == Phase.RecycleChoice);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                    Check(g.ResolveRecycling(0, [0, 1]).Success);
                }
                Check(g.State.Phase == Phase.TrapCraftChoice && g.ResolveTrapCraft(0, false).Success);
                Check(g.State.Phase == Phase.Final && p.ActionsLeft == 0);
                Check(g.Discard(0, p.Hand.IndexOf("senshuni")).Success && p.Hand.Count == 7 && g.State.CurrentPlayer == 1);
            }
        }),
        ("Mano: un descarte por efecto con Sniper no bloquea el cierre posterior del turno", () =>
        {
            var (g, _) = Fixture(0, 1); var rival = g.State.Players[1];
            rival.Hand.AddRange([CardCatalog.Sniper.Id, "attack"]);
            g.State.Phase = Phase.Discard; g.State.DiscardPlayer = 1;
            g.State.DiscardsRemaining = 2; g.State.DiscardResumePhase = Phase.Actions;
            Check(g.Discard(1, 0).Success && g.ResolveSniper(1, true).Success && g.Discard(1, 0).Success);
            Check(g.State.Phase == Phase.Actions && g.Advance(0).Success);
            Check(g.State.CurrentPlayer == 1 && g.State.Phase == Phase.Combat && rival.Hand.Count == 3);
        }),
        ("Mano: un COMBO en etapa final respeta el límite después de todas sus elecciones", () =>
        {
            foreach (var noTarget in new[] { false, true })
            {
                var (g, rng) = Fixture(0, 8); var p = g.State.Active;
                if (noTarget) { p.Hand.Clear(); p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Destiny.Id, 8)); }
                p.Hand[0] = CardCatalog.MindReset.Id; p.Memory.AddRange(["attack", "attack"]);
                Check(g.Advance(0).Success && g.PlayCode(0, 0).Success);
                Check(g.State.CurrentPlayer == 0 && g.State.PendingDecision?.Source == CardCatalog.MindReset.Id && p.Hand.Count == 7);
                Check(g.ChooseMindResetTarget(0, 0).Success);
                if (noTarget) Check(g.ResolveMindReset(0, -1).Success);
                else
                {
                    Check(g.ChooseMindResetMemory(0, 0, 0).Success && g.ChooseMindResetMemory(0, 0, 1).Success);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                    Check(g.ResolveMindReset(0, 0).Success);
                }
                Check(p.Hand.Count == 7 && g.State.CurrentPlayer == 1 && g.State.Phase == Phase.Combat);
            }
        })
    };

    private static (GameEngine, ScriptedRandom) Fixture(int seat, int size)
    {
        var rng = new ScriptedRandom(); var g = new GameEngine(rng);
        g.State.CurrentPlayer = seat; g.State.Phase = Phase.Actions; g.State.Turn = 1;
        foreach (var p in g.State.Players)
        {
            p.Dexterity = 0;
            p.Hand.Clear(); p.Memory.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 12));
        }
        g.State.Active.Hand.AddRange(Enumerable.Repeat("attack", size));
        return (g, rng);
    }
    private static void Check(bool ok) { if (!ok) throw new Exception("Límite de mano no satisfecho."); }
}
