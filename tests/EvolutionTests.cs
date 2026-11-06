using Relick.Core;

static class EvolutionTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Evolución: exige 6 natural y 1800 PR; suma 2 vidas y eleva el máximo a 10", () =>
        {
            foreach (var health in new[] { 0, 3, 8 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Health = health; p.Relikia = 1800;
                var before = Snapshot(g); Check(!g.Evolve(0).Success && before == Snapshot(g));
                NaturalSix(g, rng); p.Relikia = 1799; before = Snapshot(g);
                Check(p.HasNaturalSix && !g.Evolve(0).Success && before == Snapshot(g));
                p.Relikia = 2000; var actions = g.State.ActionsLeft;
                Check(g.Evolve(0).Success && p.Level == 2 && p.Health == health + 2 && p.MaxHealth == 10 && p.Relikia == 200 && g.State.ActionsLeft == actions);
                before = Snapshot(g); Check(!g.Evolve(0).Success && before == Snapshot(g));
                g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; p.Health = 9; p.Hand.Clear(); p.Hand.Add("test-recover");
                Check(g.PlayCode(0, 0).Success && p.Health == 10);
            }
        }),
        ("Evolución: 6 natural persistente, no sorteo ni relanzamiento; rechazos atómicos", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikia = 3000;
            rng.EnqueueDie(3); rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, true).Success);
            Check(!p.HasNaturalSix && !g.Evolve(0).Success);
            g.State.AttackedThisTurn = false; p.AttackRerollUsedThisTurn = false; p.Hand.Add("attack");
            rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success); var before = Snapshot(g);
            Check(!g.Evolve(0).Success && before == Snapshot(g));
            Check(g.ResolveAttackReroll(0, false).Success && p.HasNaturalSix);
            before = Snapshot(g); Check(!g.Evolve(1).Success && !g.Evolve(-1).Success && before == Snapshot(g));
            Check(g.Advance(0).Success && g.Advance(0).Success && p.HasNaturalSix);
            var initialRandom = new ScriptedRandom(); var initial = new GameEngine(initialRandom);
            initialRandom.EnqueueDie(6); initialRandom.EnqueueDie(1); Check(initial.Start(Array.Empty<int>()).Success);
            Check(initial.State.Players.All(player => !player.HasNaturalSix));
        }),
        ("Yuno Lv 2: dos relanzamientos, 6 conserva crítico, 1 detiene y puede parar antes", () =>
        {
            foreach (var sequence in new[] { new[] { 4, 6 }, new[] { 1 }, new[] { 5 } })
            {
                var (g, rng) = Evolved(); rng.EnqueueDie(2);
                Check(g.Attack(0, 0, 0).Success);
                foreach (var value in sequence) { rng.EnqueueDie(value); Check(g.ResolveAttackReroll(0, true).Success); }
                if (sequence.SequenceEqual(new[] { 5 })) Check(g.ResolveAttackReroll(0, false).Success);
                var final = sequence.Last();
                Check(g.State.LastAttackDie == final && g.State.Players[1].Health == 8 - GameEngine.DamageFor(final, 3, 5));
                Check(g.State.PendingAttackRoll == null && g.State.Active.AttackRerollUsedThisTurn);
                var before = Snapshot(g); Check(!g.ResolveAttackReroll(0, true).Success && before == Snapshot(g));
            }
        }),
        ("Yuno Lv 2: dado de daño Magnet se decide antes de aplicar daño; 1 inicial no ofrece", () =>
        {
            foreach (var damageDie in new[] { 1, 2 })
            {
                var (g, rng) = Evolved(CardCatalog.Magnet.Id); rng.EnqueueDie(5); rng.EnqueueDie(damageDie);
                Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
                if (damageDie == 1) Check(g.State.Players[1].Health == 7 && g.State.Phase == Phase.Combat);
                else
                {
                    Check(g.State.Phase == Phase.DieRerollChoice && g.State.PendingDieRoll!.Purpose == DiePurpose.MagnetDamage && g.State.Players[1].Health == 8);
                    rng.EnqueueDie(3); Check(g.ResolveDieReroll(0, true).Success && g.State.Phase == Phase.DieRerollChoice);
                    rng.EnqueueDie(6); Check(g.ResolveDieReroll(0, true).Success && g.State.Players[1].Health == 2 && g.State.Phase == Phase.Combat);
                }
            }
        }),
        ("Yuno Lv 2: una sola cadena por turno; evolucionar no refresca un uso gastado", () =>
        {
            var (g, rng) = Evolved(CardCatalog.Magnet.Id); rng.EnqueueDie(2); rng.EnqueueDie(5); rng.EnqueueDie(4);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, true).Success && g.ResolveAttackReroll(0, false).Success);
            Check(g.State.Phase == Phase.Combat && g.State.Players[1].Health == 4 && g.State.Active.AttackRerollUsedThisTurn);
            var (baseGame, random) = Fixture(); NaturalSix(baseGame, random); var p = baseGame.State.Active;
            p.AttackRerollUsedThisTurn = true; p.Relikia = 1800; Check(baseGame.Evolve(0).Success && p.AttackRerollUsedThisTurn);
            Check(baseGame.Advance(0).Success && baseGame.Advance(0).Success && !p.AttackRerollUsedThisTurn);
        }),
        ("Yuno Lv 2: reciclaje pausa robos múltiples y los continúa una sola vez", () =>
        {
            var (g, rng) = Evolved(); var p = g.State.Active; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            p.Hand.Clear(); p.Hand.Add("senshuni"); p.Deck.Clear(); p.Deck.Add("attack");
            p.Memory.Clear(); p.Memory.AddRange(Enumerable.Repeat("test-recover", 6));
            var count = Count(g); rng.EnqueueDie(2); Check(g.PlayCode(0, 0).Success);
            Check(g.State.Phase == Phase.DieRerollChoice && p.Hand.Count == 1 && p.Deck.Count == 0);
            var before = Snapshot(g); Check(!g.ResolveDieReroll(1, true).Success && !g.Advance(0).Success && before == Snapshot(g));
            rng.EnqueueDie(4); Check(g.ResolveDieReroll(0, true).Success && p.Hand.Count == 1);
            Check(g.ResolveDieReroll(0, false).Success && p.Hand.Count == 2 && p.Deck.Count == 3 && g.State.Phase == Phase.Actions && Count(g) == count);
        }),
        ("Yuno Lv 2: Relikia usa resultado final y encadena reciclaje sin perder elecciones", () =>
        {
            var (g, rng) = Evolved(); var p = g.State.Active; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; p.Relikia = 1000; p.Bonus = "test-recover";
            rng.EnqueueDie(2); Check(g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success && g.State.Phase == Phase.DieRerollChoice);
            rng.EnqueueDie(6); Check(g.ResolveDieReroll(0, true).Success && g.ResolveDieReroll(0, false).Success);
            Check(g.State.Phase == Phase.RelikiaChoice && g.State.PendingRelikia == RelikiaChoice.BonusReward && p.Hand.Contains("test-recover"));
            var (second, random) = Evolved(); p = second.State.Active; second.State.Phase = Phase.Actions; second.State.ActionsLeft = 2; p.Relikia = 1000;
            p.Deck.Clear(); p.Deck.Add("attack"); p.Memory.AddRange(Enumerable.Repeat("test-recover", 5)); random.EnqueueDie(2);
            Check(second.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success && second.State.Phase == Phase.DieRerollChoice);
            Check(second.ResolveDieReroll(0, false).Success && second.State.PendingRelikia == RelikiaChoice.PlaceHandCard && second.State.Phase == Phase.RelikiaChoice);
        }),
        ("Yuno Lv 2: reciclaje del rival puede decidirse fuera de su turno", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id); var rival = g.State.Players[1]; rival.Relikario = RelikarioCatalog.YunoLv2;
            rival.Bonus = "test-recover"; rival.Deck.Clear(); rival.Deck.Add("attack"); rival.Memory.AddRange(Enumerable.Repeat("senshuni", 5));
            rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
            rng.EnqueueDie(2); Check(g.ResolveMagnet(0, true).Success && g.State.Phase == Phase.DieRerollChoice && g.State.DecisionPlayer == 1);
            var before = Snapshot(g); Check(!g.ResolveDieReroll(0, true).Success && before == Snapshot(g));
            rng.EnqueueDie(1); Check(g.ResolveDieReroll(1, true).Success && rival.Deck.Count == 1 && g.State.Phase == Phase.Combat && g.State.CurrentPlayer == 0);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture(string? skill = null)
    {
        var rng = new ScriptedRandom(); var g = new GameEngine(rng); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id); return (g, rng);
    }
    private static (GameEngine, ScriptedRandom) Evolved(string? skill = null)
    {
        var (g, rng) = Fixture(skill); g.State.Active.Relikario = RelikarioCatalog.YunoLv2; g.State.Active.Health = 10; return (g, rng);
    }
    private static void NaturalSix(GameEngine g, ScriptedRandom rng)
    {
        rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de evolución/Lv 2 no satisfecha"); }
}
