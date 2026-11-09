using Relick.Core;

static class YunoTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Yuno: ambos empiezan con 8 vidas; curar no supera el máximo", () =>
        {
            var (g, _) = Fixture();
            Check(g.State.Players.All(p => p.Health == 8 && p.MaxHealth == 8 && p.Relikario.Id == "yuno"));
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            g.State.Active.Health = 7; g.State.Active.Hand.Add("test-recover");
            Check(g.PlayCode(0, 1).Success && g.State.Active.Health == 8);
            var fresh = TestGame.Create(42); Check(fresh.State.Players.All(p => p.Health == 8 && !p.AttackRerollUsedThisTurn));
        }),
        ("Yuno: 1 no permite relanzar; 2 a 6 ofrecen una elección antes del daño", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var rolls = new List<int>(); g.DieRolled += rolls.Add;
                rng.EnqueueDie(die); Check(g.Attack(0, 0, 0).Success);
                if (die == 1)
                {
                    var before = Snapshot(g);
                    Check(g.State.Phase == Phase.Combat && g.State.Players[1].Health == 8);
                    Check(!g.ResolveAttackReroll(0, true).Success && before == Snapshot(g));
                }
                else
                {
                    Check(g.State.Phase == Phase.AttackRerollChoice && g.State.DecisionPlayer == 0);
                    Check(g.State.Players[1].Health == 8 && g.State.Active.Relikia == 0);
                    Check(g.ResolveAttackReroll(0, false).Success);
                    Check(g.State.LastAttackDie == die && !g.State.Active.AttackRerollUsedThisTurn);
                }
                Check(rolls.SequenceEqual(new[] { die }));
            }
        }),
        ("Yuno: relanzar sustituye al dado anterior, incluso si empeora; 6 se convierte en 5", () =>
        {
            foreach (var reroll in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var rolls = new List<int>(); var attacks = 0; var damageEvents = 0;
                g.DieRolled += rolls.Add; g.AttackPerformed += _ => attacks++; g.DamageReceived += (_, _, _) => damageEvents++;
                rng.EnqueueDie(6); rng.EnqueueDie(reroll);
                Check(g.Attack(0, 0, 0).Success); Check(attacks == 0);
                Check(g.ResolveAttackReroll(0, true).Success);
                var effective = reroll == 6 ? 5 : reroll;
                Check(g.State.LastAttackDie == effective && g.State.LastDie == reroll);
                Check(g.State.Players[1].Health == 8 - GameEngine.DamageFor(effective, 3, 5));
                Check(g.State.Active.Relikia == (effective >= 4 ? 600 : 0));
                Check(g.State.Active.AttackRerollUsedThisTurn && g.State.PendingAttackRoll == null);
                Check(rolls.SequenceEqual(new[] { 6, reroll }) && attacks == (effective >= 4 ? 1 : 0) && damageEvents == (effective >= 4 ? 1 : 0));
                Check(g.State.Active.Hand.Count == 0 && g.State.Active.Memory.Count == 1);
                var before = Snapshot(g);
                Check(!g.ResolveAttackReroll(0, true).Success && before == Snapshot(g));
            }
        }),
        ("Yuno: elección valida jugador y fase, y bloquea otras jugadas", () =>
        {
            var (g, rng) = Fixture(); rng.EnqueueDie(3); Check(g.Attack(0, 0, 0).Success);
            var before = Snapshot(g);
            Check(!g.ResolveAttackReroll(1, true).Success && !g.ResolveAttackReroll(-1, true).Success);
            Check(!g.Advance(0).Success && !g.Learn(0, 0).Success && !g.Attack(0, 0, 0).Success);
            Check(before == Snapshot(g) && g.HasAvailablePlay(0) && !g.HasAvailablePlay(1));
            Check(g.ResolveAttackReroll(0, false).Success && !g.ResolveAttackReroll(0, false).Success);
        }),
        ("Yuno: una vez por turno y se recupera al comenzar el siguiente", () =>
        {
            var (g, rng) = Fixture(); rng.EnqueueDie(2); rng.EnqueueDie(5);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, true).Success);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack"); rng.EnqueueDie(3);
            Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.Combat);
            Check(!g.ResolveAttackReroll(0, true).Success);
            Check(g.Advance(0).Success && g.Advance(0).Success);
            Check(g.State.Players.All(p => !p.AttackRerollUsedThisTurn));
            if (g.State.Phase == Phase.Draw) Check(g.Advance(1).Success);
            g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(CardCatalog.Destiny.Id); rng.EnqueueDie(2);
            Check(g.Attack(1, g.State.Active.Hand.Count - 1, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
        }),
        ("Yuno y Magnet: 6 relanzado no roba carta; dado de daño independiente sin conversión", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id); g.State.Players[1].Hand.Add("test-recover");
            rng.EnqueueDie(3); rng.EnqueueDie(6); rng.EnqueueDie(6);
            var rolls = new List<int>(); g.DieRolled += rolls.Add;
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, true).Success);
            Check(g.State.LastAttackDie == 5 && g.State.Players[1].Health == 2 && g.State.Phase == Phase.Combat);
            Check(!g.ResolveMagnet(0, false, 0).Success && rolls.SequenceEqual(new[] { 3, 6, 6 }));
            // Keeping an original 6 still produces a critical and Magnet's optional effect.
            var (critical, random) = Fixture(CardCatalog.Magnet.Id); critical.State.Players[1].Hand.Add("test-recover");
            random.EnqueueDie(6); Check(critical.Attack(0, 0, 0).Success && critical.ResolveAttackReroll(0, false).Success);
            Check(critical.State.Phase == Phase.MagnetChoice && critical.State.Players[1].Health == 3);
        }),
        ("Yuno y Destiny: defensa usa el resultado final; un 6 convertido cuesta 3 descartes", () =>
        {
            var (g, rng) = Fixture(); var defender = g.State.Players[1];
            defender.Skills.Add(CardCatalog.Destiny.Id); defender.Hand.AddRange(Enumerable.Repeat("attack", 3));
            rng.EnqueueDie(6); rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success);
            Check(g.ResolveAttackReroll(0, true).Success && g.State.Phase == Phase.DefenseChoice && g.DefenseDiscardCost == 3);
            Check(g.ResolveDefense(1, 0, new[] { 0, 1, 2 }).Success && defender.Health == 8);
        }),
        ("Yuno: no se activa al sortear inicio, reciclar ni tirar por Relikia", () =>
        {
            var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
            rng.EnqueueDie(6); rng.EnqueueDie(1); Check(g.Start(Array.Empty<int>()).Success);
            Check(g.State.Phase == Phase.Combat && g.State.PendingAttackRoll == null);
            g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("senshuni"); p.Deck.Clear(); p.Deck.Add("attack");
            p.Memory.AddRange(new[] { "attack", "test-recover", "senshuni" }); rng.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && g.State.Phase != Phase.AttackRerollChoice && !p.AttackRerollUsedThisTurn);
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; p.Relikia = 1000; p.Bonus = "attack";
            p.Deck.AddRange(new[] { "attack", "test-recover" }); rng.EnqueueDie(5);
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success && g.State.Phase == Phase.RelikiaChoice && !p.AttackRerollUsedThisTurn);
        })
    };

    private static (GameEngine, ScriptedRandom) Fixture(string? skill = null)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Memory.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id);
        return (g, rng);
    }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Yuno no satisfecha"); }
}
