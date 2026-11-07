using Relick.Core;
static class EggComboTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Huevo: Bonus y canje de ambos jugadores; repone Bonus y conserva cartas", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange })
            {
                var (g, _) = Fixture(); var p = g.State.Active; var target = g.State.Players[owner];
                g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200;
                if (zone == FieldZone.Bonus) target.Bonus = "senshuni";
                else target.Exchange.Add("senshuni");
                var total = CardCount(g); var deck = target.Deck.Count;
                var placements = 0; g.BonusPlaced += (who, _) => { Check(who == owner); placements++; };
                Check(g.DestructionTargets().Single().Zone == zone && g.PlayCode(0, 0).Success);
                var before = Snapshot(g);
                Check(!g.ResolveDestruction(0, owner, FieldZone.Exchange, 99).Success && Snapshot(g) == before);
                Check(g.ResolveDestruction(0, owner, zone, 0).Success && target.Memory.Contains("senshuni") && CardCount(g) == total);
                Check(p.Relikia == 0 && p.ActionsLeft == 1);
                if (zone == FieldZone.Bonus) Check(target.Bonus == "attack" && target.Deck.Count == deck - 1 && placements == 1);
                else Check(target.Exchange.Count == 0 && target.Deck.Count == deck && placements == 0);
            }
        }),
        ("Huevo: habilidades en Bonus o canje no son objetivos y no consumen costes", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200;
            p.Bonus = "magnet-shuffle"; p.Exchange.Add("destiny-shuffle");
            var before = Snapshot(g);
            Check(!g.DestructionTargets().Any() && !g.PlayCode(0, 0).Success && before == Snapshot(g));
        }),
        ("Huevo: destruir en canje conserva la otra carta y permite completar un nuevo canje", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.AddRange(new[] { "blue-egg", "attack" }); p.Relikia = 200;
            p.Bonus = "senshuni"; p.Exchange.Add("attack");
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, FieldZone.Exchange, 0).Success);
            Check(g.Exchange(0, 0).Success && p.Exchange.Single() == "attack" && p.Bonus == "senshuni");
        }),
        ("Huevo: coste 200 PR, objetivo no-habilidad, destrucción a memoria y rechazos atómicos", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("blue-egg"); g.State.Phase = Phase.Actions;
            p.Relikia = 200; var before = Snapshot(g); Check(!g.PlayCode(0, 0).Success && before == Snapshot(g));
            g.State.Players[1].Equipment = "lucky-pendant"; p.Relikia = 199; before = Snapshot(g); Check(!g.PlayCode(0, 0).Success && before == Snapshot(g));
            p.Relikia = 200; Check(g.PlayCode(0, 0).Success && p.Relikia == 0 && g.State.ActionsLeft == 1 && g.State.Phase == Phase.DestroyChoice);
            before = Snapshot(g); Check(!g.ResolveDestruction(1, 1, true).Success && !g.ResolveDestruction(0, 2, true).Success && !g.Advance(0).Success && before == Snapshot(g));
            Check(g.ResolveDestruction(0, 1, true).Success && g.State.Players[1].Equipment == null && g.State.Players[1].Memory.Single() == "lucky-pendant");
            g.State.ActionsLeft = 2; p.Hand.Add("blue-egg"); p.Relikia = 200; p.AttachedCards.Add("senshuni"); p.AttachedCards.Add("magnet-shuffle");
            Check(g.DestructionTargets().Count() == 1 && g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, false, 0).Success && p.Memory.Contains("senshuni") && p.AttachedCards.Single() == "magnet-shuffle");
        }),
        ("Huevo: Caja lo cancela devolviendo costes; Relikia exige 600 PR", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200; g.State.Phase = Phase.Actions;
            g.State.Players[1].Equipment = "cardboard-box";
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && g.ResolveCodeResponse(1, true).Success && p.Relikia == 200 && g.State.DestroyActor == null);
            g.State.ActionsLeft = 2; p.Memory.Clear(); p.Memory.Add("blue-egg"); p.Equipment = "lucky-pendant"; p.Relikia = 599;
            var before = Snapshot(g); Check(!g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && before == Snapshot(g));
            p.Relikia = 600; Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.Relikia == 0 && p.CorruptedMemory.Single() == "blue-egg" && g.ResolveDestruction(0, 0, true).Success);
        }),
        ("Re-lanza: gratis sin acciones, relanza 1, roba solo con 1–3, 6 no natural", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.ActionsLeft = 0; p.Hand.Add("reroll"); rng.EnqueueDie(1);
                Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                rng.EnqueueDie(die); Check(g.PlayCode(0, 0).Success);
                if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
                Check(g.State.LastAttackDie == die && p.Hand.Count == (die <= 3 ? 1 : 0) && p.ActionsLeft == 0 && !p.AttackRerollUsedThisTurn && !p.HasNaturalSix);
            }
        }),
        ("Re-lanza: no dado ni dado enemigo no cumplen requisitos", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Hand.Add("reroll"); var before = Snapshot(g);
            Check(!g.PlayCode(0, 1).Success && before == Snapshot(g));
            g.State.Players[1].Hand.Add("reroll"); rng.EnqueueDie(2); Check(g.Attack(0, 0, 0).Success); before = Snapshot(g);
            Check(!g.PlayCode(1, 0).Success && before == Snapshot(g));
        }),
        ("Combo: cupo propio fuera de turno, no refresca al entrar en acciones y se renueva cada turno", () =>
        {
            var catalogue = (Dictionary<string, CardDefinition>)CardCatalog.All;
            catalogue["test-paid-combo"] = CardCatalog.Reroll with { Id = "test-paid-combo", ActionCost = 1 };
            try
            {
                var (g, rng) = Fixture(); g.State.CurrentPlayer = 1; var p = g.State.Players[0];
                p.Hand.Clear(); p.Hand.AddRange(new[] { "test-paid-combo", "test-paid-combo" }); p.Bonus = "senshuni"; p.Deck.Clear(); p.Deck.Add("attack"); p.Memory.AddRange(Enumerable.Repeat("attack", 6));
                var rival = g.State.Players[1]; rival.Hand.Add("attack"); rival.Skills.Add("magnet-shuffle");
                rng.EnqueueDie(6); Check(g.Attack(1, 0, 0).Success && g.ResolveAttackReroll(1, false).Success);
                rng.EnqueueDie(2); Check(g.ResolveMagnet(1, true).Success && g.State.DecisionPlayer == 0);
                rng.EnqueueDie(4); Check(g.PlayCode(0, 0).Success && p.ActionsLeft == 1 && rival.ActionsLeft == 2);
                rng.EnqueueDie(6); Check(g.PlayCode(0, 0).Success && p.ActionsLeft == 0 && g.State.CurrentPlayer == 1);
                Check(g.Advance(1).Success && rival.ActionsLeft == 2 && p.ActionsLeft == 0 && g.Advance(1).Success && p.ActionsLeft == 2 && rival.ActionsLeft == 2);
            }
            finally { catalogue.Remove("test-paid-combo"); }
        }),
        ("Re-lanza: robo que recicla pausa y reanuda el dado original una sola vez", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Hand.Add("reroll"); p.Deck.Clear(); p.Memory.AddRange(Enumerable.Repeat("attack", 5));
            rng.EnqueueDie(1); Check(g.Attack(0, 0, 0).Success); rng.EnqueueDie(2); rng.EnqueueDie(4);
            Check(g.PlayCode(0, 0).Success && g.State.PendingDieRoll?.Purpose == DiePurpose.Recycling && g.State.PendingAttackRoll == null);
            Check(g.ResolveDieReroll(0, false).Success && g.State.PendingAttackRoll?.Die == 2 && p.Hand.Count == 1);
            Check(g.ResolveAttackReroll(0, false).Success && g.State.Phase == Phase.Combat && g.State.PendingDieRoll == null);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add("destiny-shuffle"); return (g, rng);
    }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static int CardCount(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Bonus == null ? 0 : 1) + (p.Equipment == null ? 0 : 1));
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Huevo/Combo no satisfecha"); }
}
