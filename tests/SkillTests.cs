using Relick.Core;

static class SkillTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Magnet: daño normal D6 independiente, reducido redondeado y premio", () =>
        {
            foreach (var hit in new[] { 4, 5 }) foreach (var amount in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(CardCatalog.Magnet.Id);
                var rolls = new List<int>(); g.DieRolled += rolls.Add;
                rng.EnqueueDie(hit); rng.EnqueueDie(amount);
                Check(AttackKeepingRoll(g,0, 0, 0).Success);
                Check(g.State.Players[1].Health == 8 - (hit == 4 ? (amount + 1) / 2 : amount));
                Check(rolls.SequenceEqual(new[] { hit, amount }));
                Check(g.State.Active.Relikia == 500 && g.State.Phase == Phase.Combat);
            }
        }),
        ("Magnet: crítico toma una carta elegida sin revelar permanentemente la mano", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id);
            var rival = g.State.Players[1]; rival.Hand.AddRange(new[] { "attack", "test-recover", "senshuni" });
            rival.RevealHandCard(2);
            rng.EnqueueDie(6); Check(AttackKeepingRoll(g,0, 0, 0).Success);
            Check(g.State.Phase == Phase.MagnetChoice && rival.Health == 3 && g.State.Active.Relikia == 0);
            var before = Fingerprint(g);
            Check(!g.ResolveMagnet(1, false, 0).Success && !g.ResolveMagnet(0, false, 99).Success);
            Check(!g.Advance(0).Success && !g.ResolveMagnet(0, true).Success);
            Check(before == Fingerprint(g));
            Check(g.ResolveMagnet(0, false, 1).Success);
            Check(g.State.Active.Hand.Single() == "test-recover" && rival.Hand.SequenceEqual(new[] { "attack", "senshuni" }));
            Check(rival.RevealedHandCard(1) == "senshuni" && rival.RevealedHandCard(0) == null);
            Check(g.State.Phase == Phase.Combat && g.State.Active.Relikia == 500 && !g.ResolveMagnet(0, false, 0).Success);
        }),
        ("Magnet: Bonus se transfiere y repone; efecto opcional; fallo sin efecto", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id);
            var rival = g.State.Players[1]; rival.Bonus = "senshuni";
            var next = rival.Deck[0]; var deckCount = rival.Deck.Count;
            rng.EnqueueDie(6); Check(AttackKeepingRoll(g,0, 0, 0).Success);
            Check(g.ResolveMagnet(0, true).Success);
            Check(g.State.Active.Hand.Single() == "senshuni" && rival.Bonus == next && rival.Deck.Count == deckCount - 1);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
            rng.EnqueueDie(6); Check(AttackKeepingRoll(g,0, 1, 0).Success);
            var before = rival.Hand.Count;
            Check(g.SkipMagnet(0).Success && g.State.Phase == Phase.Combat && rival.Hand.Count == before);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
            rng.EnqueueDie(1); Check(AttackKeepingRoll(g,0, 1, 0).Success);
            Check(g.State.Phase == Phase.Combat && !g.ResolveMagnet(0, true).Success);
        }),
        ("Destiny: decisión del defensor, coste exacto y cancelación sin daño", () =>
        {
            foreach (var critical in new[] { false, true })
            {
                var (g, rng) = Fixture(CardCatalog.Destiny.Id);
                var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Destiny.Id);
                defender.Hand.AddRange(Enumerable.Repeat("attack", 5));
                rng.EnqueueDie(critical ? 6 : 5); Check(AttackKeepingRoll(g,0, 0, 0).Success);
                Check(g.State.Phase == Phase.DefenseChoice && g.State.DecisionPlayer == 1 && defender.Health == 8);
                var cost = critical ? 4 : 3; var before = Fingerprint(g);
                Check(!g.PassDefense(0).Success);
                Check(!g.ResolveDefense(1, 0, Enumerable.Repeat(0, cost)).Success);
                Check(!g.ResolveDefense(1, 0, Enumerable.Range(0, cost - 1)).Success);
                Check(!g.ResolveDefense(1, 1, Enumerable.Range(0, cost)).Success);
                Check(before == Fingerprint(g));
                var damageEvents = 0; g.DamageReceived += (_, _, _) => damageEvents++;
                Check(g.ResolveDefense(1, 0, Enumerable.Range(0, cost)).Success);
                Check(defender.Health == 8 && damageEvents == 0 && defender.Memory.Count == cost && defender.Hand.Count == 5 - cost);
                Check(defender.CountersOnSkill(0) == 1 && g.State.Phase == Phase.Combat && g.State.PendingAttack == null);
                Check(g.State.Active.Relikia == 600);
            }
        }),
        ("Destiny: dos usos por copia; copias repetidas independientes", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Destiny.Id);
            var defender = g.State.Players[1]; defender.Skills.AddRange(new[] { CardCatalog.Destiny.Id, CardCatalog.Destiny.Id });
            for (var turn = 0; turn < 4; turn++)
            {
                g.State.AttackedThisTurn = false; g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("attack");
                defender.Hand.Clear(); defender.Hand.AddRange(Enumerable.Repeat("attack", 4));
                rng.EnqueueDie(5); Check(AttackKeepingRoll(g,0, 0, 0).Success);
                var slot = turn < 2 ? 0 : 1;
                Check(g.ResolveDefense(1, slot, new[] { 0, 1, 2 }).Success);
            }
            Check(defender.CountersOnSkill(0) == 2 && defender.CountersOnSkill(1) == 2);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack");
            rng.EnqueueDie(5); Check(AttackKeepingRoll(g,0, 0, 0).Success);
            Check(g.State.Phase == Phase.Combat && defender.Health == 5);
        }),
        ("Destiny: no se ofrece sin coste suficiente y se puede recibir daño", () =>
        {
            foreach (var handCount in new[] { 2, 3, 4 })
            {
                var (g, rng) = Fixture(CardCatalog.Destiny.Id);
                var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Destiny.Id);
                defender.Hand.AddRange(Enumerable.Repeat("attack", handCount));
                rng.EnqueueDie(6); Check(AttackKeepingRoll(g,0, 0, 0).Success);
                if (handCount == 4) { Check(g.State.Phase == Phase.DefenseChoice); Check(g.PassDefense(1).Success); }
                Check(g.State.Phase == Phase.Combat && defender.Health == 3 && defender.CountersOnSkill(0) == 0 && defender.Hand.Count == handCount);
            }
        }),
        ("Cancelar daño conserva el crítico y la elección de Magnet", () =>
        {
            var (g, rng) = Fixture(CardCatalog.Magnet.Id);
            var defender = g.State.Players[1]; defender.Skills.Add(CardCatalog.Destiny.Id);
            defender.Hand.AddRange(Enumerable.Repeat("attack", 5));
            rng.EnqueueDie(6); Check(AttackKeepingRoll(g,0, 0, 0).Success);
            Check(g.ResolveDefense(1, 0, new[] { 0, 1, 2, 3 }).Success);
            Check(defender.Health == 8 && g.State.Phase == Phase.MagnetChoice);
            Check(g.ResolveMagnet(0, false, 0).Success && defender.Hand.Count == 0);
        })
    };

    private static CommandResult AttackKeepingRoll(GameEngine game, int actor, int hand, int skill)
    {
        var result = game.Attack(actor, hand, skill);
        if (result.Success && game.State.Phase == Phase.AttackRerollChoice) return game.ResolveAttackReroll(actor, false);
        return result;
    }

    private static (GameEngine, ScriptedRandom) Fixture(string skill)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.SkillCounters.Clear(); p.Memory.Clear(); }
        g.State.Active.Skills.Add(skill); g.State.Active.Hand.Add("attack");
        return (g, rng);
    }
    private static string Fingerprint(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de habilidad no satisfecha"); }
}
