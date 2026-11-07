using Relick.Core;

static class DiamondHelmetTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Casco: reduce cada ataque en dos, sin vida negativa ni impacto si se absorbe todo", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, r) = Fixture(actor); var target = g.State.Players[1 - actor]; target.Equipment = CardCatalog.DiamondHelmet.Id;
                var cues = DuelVisualEvents.Capture(g); r.EnqueueDie(die);
                Check(g.Attack(actor, 0, 0).Success && g.ResolveAttackReroll(actor, false).Success);
                var damage = Math.Max(0, GameEngine.DamageFor(die, 3, 5) - 2);
                Check(target.Health == 8 - damage && target.Equipment == CardCatalog.DiamondHelmet.Id);
                Check(damage == 0 ? cues.All(c => c.Kind != DuelVisualKind.Hit) :
                    cues.Single(c => c.Kind == DuelVisualKind.Hit).Value == damage);
            }
        }),
        ("Casco: a cero vidas absorbe dos puntos, pero el daño restante sí termina el duelo", () =>
        {
            var (g, r) = Fixture(); var target = g.State.Players[1]; target.Equipment = CardCatalog.DiamondHelmet.Id;
            target.Health = 0; target.HasReachedZero = true; r.EnqueueDie(4);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && !g.State.IsOver && target.Health == 0);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add(CardCatalog.Attack.Id); r.EnqueueDie(5);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && g.State.IsOver && g.State.Winner == 0);
        }),
        ("Casco: un uno intermedio lo destruye aunque Re-lanza termine en cinco", () =>
        {
            var (g, r) = Fixture(); var p = g.State.Active; var target = g.State.Players[1];
            target.Equipment = CardCatalog.DiamondHelmet.Id; p.Hand.AddRange(new[] { CardCatalog.Reroll.Id, CardCatalog.Reroll.Id });
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 10));
            r.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success); r.EnqueueDie(1); Check(g.PlayCode(0, 0).Success);
            r.EnqueueDie(5); Check(g.PlayCode(0, 0).Success);
            if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
            Check(target.Health == 5 && target.Equipment == null && target.Memory.Count(id => id == CardCatalog.DiamondHelmet.Id) == 1);
        }),
        ("Casco: un uno inicial de cualquiera destruye ambos y los mueve una sola vez a memoria", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, r) = Fixture(actor); foreach (var p in g.State.Players) p.Equipment = CardCatalog.DiamondHelmet.Id;
                var cues = DuelVisualEvents.Capture(g); r.EnqueueDie(1); Check(g.Attack(actor, 0, 0).Success);
                Check(g.State.Players.All(p => p.Equipment == null && p.Memory.Count(id => id == CardCatalog.DiamondHelmet.Id) == 1));
                Check(cues.Count(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.Equipment) == 2);
            }
        }),
        ("Casco: se rompe con un uno de Yuno, Colgante y Re-lanza aunque el dado cambie después", () =>
        {
            foreach (var source in new[] { "yuno", "pendant", "combo" })
            {
                var (g, r) = Fixture(); g.State.Players[1].Equipment = CardCatalog.DiamondHelmet.Id;
                if (source == "pendant") g.State.Active.Equipment = CardCatalog.Pendant.Id;
                if (source == "combo") g.State.Active.Hand.Add(CardCatalog.Reroll.Id);
                r.EnqueueDie(5); Check(g.Attack(0, 0, 0).Success); r.EnqueueDie(1);
                Check(source == "yuno" ? g.ResolveAttackReroll(0, true).Success : source == "pendant" ?
                    g.UsePendant(0, PendantEffect.Reroll).Success : g.PlayCode(0, 0).Success);
                Check(g.State.Players[1].Equipment == null && g.State.Players[1].Memory.Contains(CardCatalog.DiamondHelmet.Id));
            }
        }),
        ("Casco: unos de iniciativa, curación y daño secundario no lo destruyen", () =>
        {
            var (g, r) = Fixture(); var target = g.State.Players[1]; target.Equipment = CardCatalog.DiamondHelmet.Id;
            g.State.Active.Skills[0] = CardCatalog.Magnet.Id; r.EnqueueDie(5); r.EnqueueDie(1);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && target.Health == 8 && target.Equipment != null);
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; g.State.Active.Hand.Add(CardCatalog.ExtraLife.Id); r.EnqueueDie(1);
            Check(g.PlayCode(0, 0).Success && target.Equipment != null);
            var init = TestGame.Create(r); foreach (var p in init.State.Players) p.Equipment = CardCatalog.DiamondHelmet.Id;
            r.EnqueueDie(1); r.EnqueueDie(2); Check(init.Start([]).Success && init.State.Players.All(p => p.Equipment != null));
        }),
        ("Casco: reduce la penalización de agotamiento del mazo", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.DiamondHelmet.Id;
            p.Deck.Clear(); p.Recycled = true; p.Hand.Clear(); p.Hand.Add(CardCatalog.Senshuni.Id);
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; var cues = DuelVisualEvents.Capture(g);
            Check(g.PlayCode(0, 0).Success && p.Health == 8 && cues.All(c => c.Kind != DuelVisualKind.Hit));
        }),
        ("Casco: equipar, reemplazar y recuperar conserva su efecto", () =>
        {
            var (g, r) = Fixture(); g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            g.State.Active.Hand.Clear(); g.State.Active.Hand.Add(CardCatalog.DiamondHelmet.Id);
            g.State.Active.Equipment = CardCatalog.Pendant.Id;
            Check(g.Equip(0, 0).Success && g.State.Active.Equipment == CardCatalog.DiamondHelmet.Id && g.State.Active.Memory.Contains(CardCatalog.Pendant.Id));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), r); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 1;
            g.State.Active.Hand.Add(CardCatalog.Attack.Id); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            r.EnqueueDie(5); Check(g.Attack(1, 0, 0).Success && g.ResolveAttackReroll(1, false).Success && g.State.Players[0].Health == 7);
        })
    };
    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var r = new ScriptedRandom(); var g = TestGame.Create(r); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = actor;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Bonus = null; }
        g.State.Active.Hand.Add(CardCatalog.Attack.Id); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        return (g, r);
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla del Casco de Diamantes incorrecta."); }
}
