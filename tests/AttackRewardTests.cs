using Relick.Core;

static class AttackRewardTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Premio: espera a la defensa y al daño, conserva la habilidad declarada y se paga una sola vez", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikia = 123;
            g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            var prAtDamage = -1; g.DamageReceived += (_, _, _) => prAtDamage = p.Relikia;
            Attack(g, rng, 5);
            Check(g.State.Phase == Phase.DefenseChoice && p.Relikia == 123);
            p.Skills.Clear(); Check(g.PassDefense(1).Success);
            Check(prAtDamage == 123 && p.Relikia == 723 && g.State.PendingAttack == null);
            var before = g.SaveSnapshot(); Check(!g.PassDefense(1).Success && before == g.SaveSnapshot());
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.State.Active.Relikia == 723 && RewardCount(g) == 1);
        }),
        ("Premio: cancelar solo el daño con Destiny o Caja lo conserva", () =>
        {
            foreach (var source in new[] { "destiny", "box" })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; var defender = g.State.Players[1];
                if (source == "box") defender.Equipment = CardCatalog.CardboardBox.Id;
                else { defender.Skills.Add(CardCatalog.Destiny.Id); defender.Hand.AddRange(["attack", "attack", "attack"]); }
                Attack(g, rng, 5); Check(p.Relikia == 0);
                Check((source == "box" ? g.CancelAttackWithBox(1) : g.ResolveDefense(1, 0, [0, 1, 2])).Success);
                Check(p.Relikia == 600 && defender.Health == 8 && RewardCount(g) == 1);
            }
        }),
        ("Premio: Esquivar cancela el ataque completo sin quitar PR anteriores; un fallo aún permite cobrar después", () =>
        {
            foreach (var die in new[] { 2, 4, 5, 6 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Relikia = 777;
                g.State.Players[1].Hand.Add(CardCatalog.Dodge.Id);
                Attack(g, rng, 5); Check(p.Relikia == 777);
                rng.EnqueueDie(die); Check(g.PlayCode(1, 0).Success && p.Relikia == 777);
                if (die <= 3)
                {
                    Check(g.State.Phase == Phase.DefenseChoice && g.PassDefense(1).Success);
                    Check(p.Relikia == 1377 && RewardCount(g) == 1);
                }
                else Check(g.State.PendingAttack == null && RewardCount(g) == 0 && g.State.Players[1].Health == 8);
            }
        }),
        ("Premio: crítico Magnet espera a elegir o pasar; el Bonus y su reciclaje también se resuelven antes", () =>
        {
            foreach (var mode in new[] { "skip", "hand", "bonus-recycle" })
            {
                var (g, rng) = Fixture(CardCatalog.Magnet.Id); var target = g.State.Players[1];
                target.Hand.Add("attack");
                if (mode == "bonus-recycle")
                {
                    target.Bonus = "senshuni"; target.Deck.Clear(); target.Deck.Add("attack"); target.Memory.AddRange(["attack", "attack"]);
                }
                Attack(g, rng, 6);
                Check(g.State.Phase == Phase.MagnetChoice && g.State.Active.Relikia == 0 && target.Health == 3);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                if (mode == "skip") Check(g.SkipMagnet(0).Success);
                else if (mode == "hand")
                {
                    // The steal cue carries only slots, so the client can fly the card between the hands.
                    var cues = DuelVisualEvents.Capture(g); var slot = target.Hand.Count - 1;
                    Check(g.ResolveMagnet(0, false, slot).Success);
                    var steal = cues.Single(c => c.Kind == DuelVisualKind.HandSteal);
                    Check(steal.Player == 1 && steal.Target == 0 && steal.SourceIndex == slot &&
                        steal.Value == g.State.Players[0].Hand.Count - 1 && g.State.Players[0].Hand[steal.Value] == "attack" &&
                        steal.Caption == "" && cues.All(c => c.Kind != DuelVisualKind.Draw));
                }
                else
                {
                    rng.EnqueueDie(2); Check(g.ResolveMagnet(0, true).Success);
                    Check(g.State.Phase == Phase.RecycleChoice && g.State.Players[0].Relikia == 0);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                    Check(g.ResolveRecycling(1, [0, 1]).Success);
                }
                Check(g.State.Phase == Phase.Combat && g.State.Players[0].Relikia == 500 && RewardCount(g) == 1);
                Check(g.State.PendingAttack == null);
            }
        }),
        ("Premio: espera al Sniper descartado por Destiny y a sus respuestas, incluso al recuperar", () =>
        {
            foreach (var counter in new[] { false, true })
            {
                var (g, rng) = Fixture(); var defender = g.State.Players[1];
                defender.Skills.Add(CardCatalog.Destiny.Id); defender.Hand.AddRange([CardCatalog.Sniper.Id, "attack", "attack"]);
                if (counter) g.State.Active.Hand.Add(CardCatalog.LoadError.Id);
                Attack(g, rng, 5);
                Check(g.ResolveDefense(1, 0, [0, 1, 2]).Success && g.State.Phase == Phase.SniperChoice && g.State.Active.Relikia == 0);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.ResolveSniper(1, true).Success);
                if (counter)
                {
                    Check(g.State.Phase == Phase.ErrorResponse && g.State.Active.Relikia == 0);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                    Check(g.PlayCode(0, 0).Success);
                }
                Check(g.State.Phase == Phase.Combat && g.State.Active.Relikia == 600 && RewardCount(g) == 1);
            }
        }),
        ("Premio: ataque exitoso sin daño también cobra, fallo no cobra y la derrota termina la resolución", () =>
        {
            var cards = (Dictionary<string, CardDefinition>)CardCatalog.All;
            const string id = "test-zero-damage-reward";
            cards.Add(id, new(id, "Fixture sin daño", CardKind.Skill, "", Reward: 500));
            try
            {
                foreach (var die in new[] { 3, 5 })
                {
                    var (g, rng) = Fixture(id); Attack(g, rng, die);
                    Check(g.State.Active.Relikia == (die == 5 ? 500 : 0) && g.State.Players[1].Health == 8);
                }
            }
            finally { cards.Remove(id); }
            var (last, random) = Fixture(); last.State.Players[1].Health = 0; last.State.Players[1].HasReachedZero = true;
            Attack(last, random, 5);
            Check(last.State.IsOver && last.State.Winner == 0 && last.State.Players[0].Relikia == 0 && RewardCount(last) == 0);
        }),
        ("Premio: recuperar una defensa antigua ya pagada no duplica el premio", () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var (g, rng) = Fixture(); g.State.Phase = Phase.DefenseChoice;
                g.State.PendingAttack = new(0, CardCatalog.Destiny.Id, 3, false);
                g.State.Active.Relikia = 700;
                if (cancel) g.State.Players[1].Hand.Add(CardCatalog.Dodge.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                if (cancel) { rng.EnqueueDie(4); Check(g.PlayCode(1, 0).Success); }
                else Check(g.PassDefense(1).Success);
                Check(g.State.Players[0].Relikia == (cancel ? 100 : 700) && g.State.PendingAttack == null);
            }
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture(string? skill = null)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 2;
        foreach (var p in g.State.Players)
        {
            p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 20)); p.Bonus = null;
        }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id);
        return (g, rng);
    }
    private static void Attack(GameEngine g, ScriptedRandom rng, int die)
    {
        rng.EnqueueDie(die); Check(g.Attack(0, 0, 0).Success);
        if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
    }
    private static int RewardCount(GameEngine g) => g.State.Log.Count(line => line.Contains(" obtiene ") && line.Contains(" PR."));
    private static void Check(bool ok) { if (!ok) throw new Exception("Orden del premio no satisfecho."); }
}
