using Relick.Core;

static class EvolutionTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Yuno Lv 2: seis caras de ataque, uno sigue siendo uno y cinco modificado no es seis natural", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Evolved(); g.State.CurrentPlayer = actor;
                var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2;
                p.Hand.Clear(); p.Hand.Add("attack"); p.Skills.Clear(); p.Skills.Add(CardCatalog.Destiny.Id);
                p.AttackRerollUsedThisTurn = true;
                // Keep the target at its initial eight lives even when swapping the actor.
                g.State.Players[1 - actor].Health = 8;
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die);
                Check(g.Attack(actor, 0, 0).Success);
                var expected = die == 1 ? 1 : Math.Min(6, die + 1);
                Check(g.State.LastAttackDie == expected && DuelSnapshot.For(g, actor, 1).Die == expected);
                Check(g.State.Players[1 - actor].Health == 8 - GameEngine.DamageFor(expected, 3, 5));
                Check(p.HasNaturalSix == (die == 6) && cues.Count(c => c.Kind == DuelVisualKind.Die) == 1);
                Check(cues.Where(c => c.Kind == DuelVisualKind.Die).Single().Value == die);
                Check(cues.Count(c => c.Kind == DuelVisualKind.DieAdjustment) == (die is >= 2 and <= 5 ? 1 : 0));
                Check(!g.CanUseYunoOnPendingDie(actor));
            }
        }),
        ("Nivel 2 ajeno a Yuno no hereda su modificador de ataque", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Relikario = new("other-lv2", "Otro Relikario", 8, "Efecto diferente", Level: 2);
            rng.EnqueueDie(3); Check(g.Attack(0, 0, 0).Success && g.State.LastAttackDie == 3);
            Check(g.State.Players[1].Health == 8 && !p.HasNaturalSix);
        }),
        ("Yuno Lv 2: relanzamientos de Yuno, Colgante y Re-lanza aplican el aumento una sola vez", () =>
        {
            foreach (var source in new[] { "yuno", "pendant", "code" })
            {
                var (g, rng) = Evolved(); var p = g.State.Active;
                if (source == "pendant") { p.Equipment = CardCatalog.Pendant.Id; p.AttackRerollUsedThisTurn = true; }
                if (source == "code") { p.Hand.Add(CardCatalog.Reroll.Id); p.AttackRerollUsedThisTurn = true; }
                rng.EnqueueDie(source == "yuno" ? 2 : 1);
                Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                rng.EnqueueDie(4);
                Check((source switch
                {
                    "yuno" => g.ResolveAttackReroll(0, true),
                    "pendant" => g.UsePendant(0, PendantEffect.Reroll),
                    _ => g.PlayCode(0, p.Hand.IndexOf(CardCatalog.Reroll.Id))
                }).Success);
                if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
                Check(g.State.LastAttackDie == 5 && g.State.Players[1].Health == 5 && !p.HasNaturalSix);
            }
        }),
        ("Yuno Lv 2: recuperación conserva el límite ocho, las fichas gastadas y el resultado ya modificado", () =>
        {
            var (g, rng) = Evolved(); var p = g.State.Active;
            // Simulate the old persisted definition and health before the rule correction.
            p.Relikario = RelikarioCatalog.YunoLv2 with { MaxHealth = 10 }; p.Health = 10; p.Dexterity = 0;
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(p.Level == 2 && p.MaxHealth == 8 && p.Health == 8 && p.Dexterity == 0);
            rng.EnqueueDie(4); Check(g.Attack(0, 0, 0).Success && g.State.PendingAttackRoll!.Die == 5);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveAttackReroll(0, false).Success && g.State.LastAttackDie == 5 && g.State.Players[1].Health == 5);
            Check(!g.State.Active.HasNaturalSix);
        }),
        ("Yuno Lv 2 y destreza se combinan sin repetir el modificador ni gastar acciones extra", () =>
        {
            var (g, rng) = Evolved(); var p = g.State.Active; p.Dexterity = 1; p.AttackRerollUsedThisTurn = true;
            rng.EnqueueDie(2); Check(g.Attack(0, 0, 0).Success && g.State.PendingAttackRoll!.Die == 3);
            var actions = p.ActionsLeft;
            Check(g.UseDexterity(0).Success && g.State.LastAttackDie == 4 && p.Dexterity == 0 && p.ActionsLeft == actions);
            Check(g.State.Players[1].Health == 6 && !p.HasNaturalSix);
        }),
        ("Evolución: exige 6 natural y 1800 PR; recupera hasta dos vidas sin superar ocho", () =>
        {
            foreach (var health in new[] { 0, 3, 7, 8 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Health = health; p.Relikia = 1800;
                var before = Snapshot(g); Check(!g.Evolve(0).Success && before == Snapshot(g));
                NaturalSix(g, rng); p.Relikia = 1799; before = Snapshot(g);
                Check(p.HasNaturalSix && !g.Evolve(0).Success && before == Snapshot(g));
                p.Relikia = 2000; var actions = g.State.ActionsLeft;
                Check(g.Evolve(0).Success && p.Level == 2 && p.Health == Math.Min(8, health + 2) && p.MaxHealth == 8 && p.Relikia == 200 && g.State.ActionsLeft == actions);
                before = Snapshot(g); Check(!g.Evolve(0).Success && before == Snapshot(g));
                g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; p.Health = 7; p.Hand.Clear(); p.Hand.Add("test-recover");
                Check(g.PlayCode(0, 0).Success && p.Health == 8);
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
            var initialRandom = new ScriptedRandom(); var initial = TestGame.Create(initialRandom);
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
                var raw = sequence.Last(); var final = raw == 1 ? 1 : Math.Min(6, raw + 1);
                Check(g.State.LastAttackDie == final && g.State.Players[1].Health == 8 - GameEngine.DamageFor(final, 3, 5));
                Check(g.State.PendingAttackRoll == null && g.State.Active.AttackRerollUsedThisTurn);
                var before = Snapshot(g); Check(!g.ResolveAttackReroll(0, true).Success && before == Snapshot(g));
            }
        }),
        ("Yuno Lv 2: dado de daño Magnet se decide antes de aplicar daño; 1 inicial no ofrece", () =>
        {
            foreach (var damageDie in new[] { 1, 2 })
            {
                var (g, rng) = Evolved(CardCatalog.Magnet.Id); rng.EnqueueDie(4); rng.EnqueueDie(damageDie);
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
            var (g, rng) = Evolved(CardCatalog.Magnet.Id); rng.EnqueueDie(2); rng.EnqueueDie(4); rng.EnqueueDie(4);
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
            Check(g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.RecycleChoice && g.RecyclingCount == 4);
            Check(g.ResolveRecycling(0, [0, 1, 2, 3]).Success && p.Hand.Count == 2 && p.Deck.Count == 3 && g.State.Phase == Phase.Actions && Count(g) == count);
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
            Check(second.ResolveDieReroll(0, false).Success && second.State.Phase == Phase.RecycleChoice);
            Check(second.ResolveRecycling(0, [0, 1]).Success && second.State.PendingRelikia == RelikiaChoice.PlaceHandCard && second.State.Phase == Phase.RelikiaChoice);
        }),
        ("Yuno Lv 2: reciclaje del rival puede decidirse fuera de su turno", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id); var rival = g.State.Players[1]; rival.Relikario = RelikarioCatalog.YunoLv2;
            rival.Bonus = "test-recover"; rival.Deck.Clear(); rival.Deck.Add("attack"); rival.Memory.AddRange(Enumerable.Repeat("senshuni", 5));
            rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
            rng.EnqueueDie(2); Check(g.ResolveMagnet(0, true).Success && g.State.Phase == Phase.DieRerollChoice && g.State.DecisionPlayer == 1);
            var before = Snapshot(g); Check(!g.ResolveDieReroll(0, true).Success && before == Snapshot(g));
            rng.EnqueueDie(1); Check(g.ResolveDieReroll(1, true).Success && g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == 1);
            Check(g.ResolveRecycling(1, [0]).Success && rival.Deck.Count == 1 && g.State.Phase == Phase.Combat && g.State.CurrentPlayer == 0);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture(string? skill = null)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id); return (g, rng);
    }
    private static (GameEngine, ScriptedRandom) Evolved(string? skill = null)
    {
        var (g, rng) = Fixture(skill); g.State.Active.Relikario = RelikarioCatalog.YunoLv2; g.State.Active.Health = 8; return (g, rng);
    }
    private static void NaturalSix(GameEngine g, ScriptedRandom rng)
    {
        rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de evolución/Lv 2 no satisfecha"); }
}
