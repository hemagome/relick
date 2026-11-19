using Relick.Core;

static class GuardTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Guard: Casco de Diamantes publica la reducción antes del golpe reducido", () =>
        {
            var (g, rng) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.DiamondHelmet.Id;
            var visuals = DuelVisualEvents.Capture(g);
            rng.EnqueueDie(5); AttackKeeping(g);
            if (g.State.Phase == Phase.DefenseChoice) Check(g.PassDefense(1).Success);
            var guard = visuals.Single(v => v.Kind == DuelVisualKind.Guard);
            var hit = visuals.Single(v => v.Kind == DuelVisualKind.Hit);
            Check(guard.Player == 1 && guard.Attacker == 0 && guard.GuardCard == CardCatalog.DiamondHelmet.Id && !guard.Blocked);
            Check(guard.Original - 2 == guard.Value && hit.Value == guard.Value && visuals.IndexOf(guard) < visuals.IndexOf(hit));
        }),
        ("Guard: Caja de Cartón y Destiny bloquean el ataque sin golpe", () =>
        {
            foreach (var box in new[] { true, false })
            {
                var (g, rng) = Fixture(); var rival = g.State.Players[1];
                if (box) rival.Equipment = CardCatalog.CardboardBox.Id;
                else { rival.Skills.Add(CardCatalog.Destiny.Id); rival.Hand.AddRange(Enumerable.Repeat("attack", 3)); }
                rng.EnqueueDie(5); AttackKeeping(g);
                var damage = g.State.PendingAttack!.Damage;
                var visuals = DuelVisualEvents.Capture(g);
                Check(box ? g.CancelAttackWithBox(1).Success : g.ResolveDefense(1, 0, new[] { 0, 1, 2 }).Success);
                var guard = visuals.Single(v => v.Kind == DuelVisualKind.Guard);
                Check(guard.Player == 1 && guard.Attacker == 0 && guard.Blocked && guard.Value == 0 && guard.Original == damage &&
                    guard.GuardCard == (box ? CardCatalog.CardboardBox.Id : CardCatalog.Destiny.Id) && !visuals.Any(v => v.Kind == DuelVisualKind.Hit));
            }
        }),
        ("Guard: Estrella del Fontanero y Hoguera transforman el daño en 0 como bloqueo", () =>
        {
            foreach (var star in new[] { true, false })
            {
                var (g, rng) = Fixture(); var rival = g.State.Players[1];
                if (star) rival.StarEffects = 1; else rival.Equipment = CardCatalog.Bonfire.Id;
                var visuals = DuelVisualEvents.Capture(g);
                rng.EnqueueDie(5); AttackKeeping(g);
                if (g.State.Phase == Phase.DefenseChoice) Check(g.PassDefense(1).Success);
                var guard = visuals.Single(v => v.Kind == DuelVisualKind.Guard);
                Check(guard.Blocked && guard.Value == 0 && guard.Original > 0 && guard.Attacker == 0 && rival.Health == 8 &&
                    guard.GuardCard == (star ? CardCatalog.PlumberStar.Id : CardCatalog.Bonfire.Id) && !visuals.Any(v => v.Kind == DuelVisualKind.Hit));
            }
        }),
        ("Guard: un golpe sin protección no publica Guard y el aviso sobrevive a JSON", () =>
        {
            var (g, rng) = Fixture(); var visuals = DuelVisualEvents.Capture(g);
            rng.EnqueueDie(5); AttackKeeping(g);
            if (g.State.Phase == Phase.DefenseChoice) Check(g.PassDefense(1).Success);
            Check(!visuals.Any(v => v.Kind == DuelVisualKind.Guard) && visuals.Any(v => v.Kind == DuelVisualKind.Hit));
            var cue = new DuelVisualEvent { Kind = DuelVisualKind.Guard, Player = 1, Value = 1, Original = 3, GuardCard = CardCatalog.DiamondHelmet.Id, Attacker = 0 };
            var json = System.Text.Json.JsonSerializer.Serialize(new DuelSnapshot { Visuals = [cue] }, DuelJson.Default.DuelSnapshot);
            var back = System.Text.Json.JsonSerializer.Deserialize(json, DuelJson.Default.DuelSnapshot)!.Visuals.Single();
            Check(back.Original == 3 && back.Value == 1 && back.GuardCard == CardCatalog.DiamondHelmet.Id && back.Attacker == 0 && !back.Blocked);
            Check(!System.Text.Json.JsonSerializer.Serialize(new DuelSnapshot { Visuals = [new() { Kind = DuelVisualKind.Hit }] }, DuelJson.Default.DuelSnapshot).Contains("GuardCard"));
        })
    };

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(CardCatalog.Destiny.Id); return (g, rng);
    }
    private static void AttackKeeping(GameEngine g)
    {
        Check(g.Attack(0, 0, 0).Success);
        if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
    }
    private static void Check(bool value) { if (!value) throw new Exception("Aviso Guard no satisfecho"); }
}
