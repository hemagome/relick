using Relick.Core;

static class EquipmentTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Catálogo jugable solo real; equipo y cartas sobre Relikario empiezan vacíos", () =>
        {
            var deck = CardCatalog.TrainingDeck(); Check(deck.Count == 20 && deck.Count(id => id == CardCatalog.Pendant.Id) == 2);
            Check(deck.All(id => id is "attack" or "magnet-shuffle" or "destiny-shuffle" or "lucky-pendant" or "senshuni" or "cardboard-box" or "extra-life" or "points-increase" or "blue-egg" or "reroll" or "load-error"));
            Check(TestGame.Create(42).State.Players.All(p => p.Equipment == null && p.AttachedCards.Count == 0));
        }),
        ("Equipar cuesta una acción y reemplazar destruye el anterior sin perder cartas", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            p.Hand.Clear(); p.Hand.AddRange(new[] { CardCatalog.Pendant.Id, CardCatalog.Pendant.Id });
            var count = Count(g); Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.Pendant.Id && p.Hand.Count == 1 && p.Memory.Count == 0 && g.State.ActionsLeft == 1);
            Check(g.Equip(0, 0).Success && p.Memory.Single() == CardCatalog.Pendant.Id && p.Hand.Count == 0 && p.Equipment == CardCatalog.Pendant.Id && g.State.ActionsLeft == 0 && Count(g) == count);
            var before = Snapshot(g); Check(!g.Equip(0, 0).Success && before == Snapshot(g));
        }),
        ("Equipar valida turno, etapa, tipo e índices sin modificar la partida", () =>
        {
            var (g, _) = Fixture(); g.State.Active.Hand.Add(CardCatalog.Pendant.Id); var before = Snapshot(g);
            Check(!g.Equip(0, 1).Success && !g.Equip(1, 1).Success && before == Snapshot(g));
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; before = Snapshot(g);
            Check(!g.Equip(0, 0).Success && !g.Equip(0, -1).Success && !g.Equip(0, 99).Success && before == Snapshot(g));
        }),
        ("Colgante permite relanzar un 1 sin consumir la habilidad de Yuno", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
            rng.EnqueueDie(1); Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
            Check(!g.CanUseYunoOnPendingDie(0) && g.CanUsePendant(0, PendantEffect.Reroll));
            var count = Count(g); rng.EnqueueDie(6); Check(g.UsePendant(0, PendantEffect.Reroll).Success);
            Check(p.Equipment == null && p.Memory.Count == 2 && !p.AttackRerollUsedThisTurn && !p.HasNaturalSix && Count(g) == count);
            Check(g.ResolveAttackReroll(0, false).Success && g.State.LastAttackDie == 6 && g.State.Players[1].Health == 3 && !p.HasNaturalSix);
        }),
        ("Colgante suma +1/+2 solo tras relanzar y nunca produce crítico por suma", () =>
        {
            foreach (var effect in new[] { PendantEffect.AddOne, PendantEffect.AddTwo })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
                rng.EnqueueDie(3); Check(g.Attack(0, 0, 0).Success); var before = Snapshot(g);
                Check(!g.UsePendant(0, effect).Success && before == Snapshot(g));
                rng.EnqueueDie(3); Check(g.ResolveAttackReroll(0, true).Success && g.State.Phase == Phase.AttackRerollChoice);
                Check(g.UsePendant(0, effect).Success && p.Equipment == null && p.Memory.Contains(CardCatalog.Pendant.Id));
                var die = effect == PendantEffect.AddOne ? 4 : 5;
                Check(g.State.LastAttackDie == die && g.State.Players[1].Health == 8 - GameEngine.DamageFor(die, 3, 5));
            }
            var (blocked, random) = Fixture(); blocked.State.Active.Equipment = CardCatalog.Pendant.Id;
            random.EnqueueDie(3); random.EnqueueDie(5); Check(blocked.Attack(0, 0, 0).Success && blocked.ResolveAttackReroll(0, true).Success);
            var unchanged = Snapshot(blocked);
            Check(!blocked.UsePendant(0, PendantEffect.AddOne).Success && !blocked.UsePendant(0, PendantEffect.AddTwo).Success && unchanged == Snapshot(blocked));
        }),
        ("Colgante no permite volver a relanzar con Yuno tras detenerlo en 1", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Equipment = CardCatalog.Pendant.Id;
            rng.EnqueueDie(3); rng.EnqueueDie(1); Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, true).Success);
            Check(!g.CanUseYunoOnPendingDie(0) && g.CanUsePendant(0, PendantEffect.AddTwo));
            Check(g.UsePendant(0, PendantEffect.AddTwo).Success && g.State.LastAttackDie == 3 && g.State.Phase == Phase.Combat);
        }),
        ("Colgante: otro dado puede subir a 6; no cuenta como 6 natural", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
            g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2; p.Relikia = 500; p.Bonus = CardCatalog.Magnet.Id;
            rng.EnqueueDie(1); Check(g.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success && g.State.Phase == Phase.DieRerollChoice);
            rng.EnqueueDie(6); Check(g.UsePendant(0, PendantEffect.Reroll).Success && g.State.Phase == Phase.RelikiaChoice && !p.HasNaturalSix);
            var (second, random) = Fixture(); p = second.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Equipment = CardCatalog.Pendant.Id;
            second.State.Phase = Phase.Actions; second.State.ActionsLeft = 2; p.Relikia = 500; p.Bonus = CardCatalog.Magnet.Id;
            random.EnqueueDie(2); random.EnqueueDie(4);
            Check(second.UseRelikiaEffect(0, RelikiaEffect.ClaimBonus).Success && second.ResolveDieReroll(0, true).Success);
            Check(second.UsePendant(0, PendantEffect.AddTwo).Success && second.State.PendingDieRoll!.Die == 6);
            Check(second.ResolveDieReroll(0, false).Success && second.State.Phase == Phase.RelikiaChoice && !p.HasNaturalSix);
        }),
        ("Colgante: pasar conserva equipo y 6 original; rechazos no lo destruyen", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
            rng.EnqueueDie(6); Check(g.Attack(0, 0, 0).Success); var before = Snapshot(g);
            Check(!g.UsePendant(1, PendantEffect.Reroll).Success && !g.UsePendant(0, (PendantEffect)99).Success && before == Snapshot(g));
            Check(g.ResolveAttackReroll(0, false).Success && p.Equipment == CardCatalog.Pendant.Id && p.HasNaturalSix);
            before = Snapshot(g); Check(!g.UsePendant(0, PendantEffect.Reroll).Success && before == Snapshot(g));
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(CardCatalog.Destiny.Id); return (g, rng);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.Skills.Count + p.CorruptedMemory.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de equipo/Colgante no satisfecha"); }
}
