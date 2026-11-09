using Relick.Core;
static class SniperTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Sniper: bonifica ataques reducidos, normales y críticos, nunca fallos", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                p.Equipment = CardCatalog.Sniper.Id; rng.EnqueueDie(die);
                Check(g.Attack(0, 0, 0).Success);
                if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
                var expected = die < 4 ? 0 : GameEngine.DamageFor(die, 3, 5) + 1;
                Check(g.State.Players[1].Health == 8 - expected);
            }
        }),
        ("Sniper: contador por daño recibido, persiste y se destruye al tercero", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; target.Equipment = CardCatalog.Sniper.Id;
            for (var n = 1; n <= 3; n++)
            {
                if (n > 1) { g.State.Active.Hand.Add("attack"); g.State.AttackedThisTurn = false; g.State.Phase = Phase.Combat; }
                rng.EnqueueDie(4); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
                if (n < 3)
                {
                    Check(g.State.Players[1].EquipmentCounters == n);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                    Check(DuelSnapshot.For(g, -1, 1).Players[1].EquipmentCounters == n);
                }
            }
            target = g.State.Players[1]; Check(target.Equipment == null && target.EquipmentCounters == 0 && target.Memory.Contains(CardCatalog.Sniper.Id));
        }),
        ("Sniper: descarte permite equipar gratis o canje; destrucción no activa descarte", () =>
        {
            foreach (var equip in new[] { false, true })
            {
                var (g, rng) = Fixture(); var p = g.State.Players[1]; p.Hand.Add(CardCatalog.Sniper.Id);
                p.Equipment = "pc-8bits"; p.EquipmentDie = 5;
                g.State.Phase = Phase.Discard; g.State.DiscardPlayer = 1; g.State.DiscardsRemaining = 1; g.State.DiscardResumePhase = Phase.Actions;
                Check(g.Discard(1, 0).Success && g.State.Phase == Phase.SniperChoice);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Players[1];
                Check(!g.ResolveSniper(0, equip).Success && g.ResolveSniper(1, equip).Success && p.ActionsLeft == 2);
                Check(g.State.Phase == Phase.Actions && g.State.PendingSniper == null);
                if (equip) Check(p.Equipment == CardCatalog.Sniper.Id && p.EquipmentDie == 0 && p.EquipmentCounters == 0 && p.Memory.Contains("pc-8bits"));
                else Check(p.Exchange.Single() == CardCatalog.Sniper.Id && p.Equipment == "pc-8bits");
            }
        }),
        ("Sniper: Destiny descarta varias copias, elige destinos y mantiene ataque cancelado", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1];
            p.Skills.Add("destiny-shuffle"); p.Hand.AddRange([CardCatalog.Sniper.Id, CardCatalog.Sniper.Id, "attack"]);
            rng.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
            Check(g.ResolveDefense(1, 0, [0, 1, 2]).Success && g.State.Phase == Phase.SniperChoice && p.Health == 8);
            Check(g.ResolveSniper(1, true).Success && g.State.Phase == Phase.SniperChoice);
            Check(g.ResolveSniper(1, false).Success && g.State.Phase == Phase.Combat && p.Equipment == CardCatalog.Sniper.Id && p.Exchange.Single() == CardCatalog.Sniper.Id);
        }),
        ("Sniper: varios descartes difieren el canje hasta terminar elecciones y soportan reciclaje", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Players[1];
            p.Skills.Add("destiny-shuffle"); p.Hand.AddRange([CardCatalog.Sniper.Id, CardCatalog.Sniper.Id, "attack"]);
            p.Exchange.Add("attack"); p.Bonus = "attack"; p.Deck.Clear();
            rng.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success);
            Check(g.ResolveDefense(1, 0, [0, 1, 2]).Success);
            Check(g.ResolveSniper(1, false).Success && g.State.Phase == Phase.SniperChoice && p.Exchange.Count == 2);
            rng.EnqueueDie(2); Check(g.ResolveSniper(1, true).Success && g.State.Phase == Phase.RecycleChoice);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveRecycling(1, [0, 1]).Success && g.State.Phase == Phase.Combat && g.State.PendingSniper == null);
        }),
        ("Sniper: Error puede negar el equipo gratuito sin cobrar acciones", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Players[1]; p.Hand.Add(CardCatalog.Sniper.Id);
            g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("load-error");
            g.State.Phase = Phase.Discard; g.State.DiscardPlayer = 1; g.State.DiscardsRemaining = 1; g.State.DiscardResumePhase = Phase.Actions;
            Check(g.Discard(1, 0).Success && g.ResolveSniper(1, true).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(0, 0).Success && p.Equipment == null && p.ActionsLeft == 2 && p.Memory.Contains(CardCatalog.Sniper.Id));
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Equipment = null; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add("destiny-shuffle"); return (g, rng);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Mira de Sniper no satisfecha"); }
}
