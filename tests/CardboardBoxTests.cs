using Relick.Core;

static class CardboardBoxTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Caja de Cartón: equipo real, una copia y reemplazo de equipo", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
            p.Hand.Clear(); p.Hand.Add(CardCatalog.CardboardBox.Id); p.Equipment = CardCatalog.Pendant.Id;
            Check(CardCatalog.TrainingDeck().Count == 32 && CardCatalog.TrainingDeck().Count(id => id == CardCatalog.CardboardBox.Id) == 1);
            Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.CardboardBox.Id && p.Memory.Single() == CardCatalog.Pendant.Id && g.State.ActionsLeft == 1);
        }),
        ("Caja cancela daño reducido, normal y crítico sin requerir Destiny ni descartes", () =>
        {
            foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, rng) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
                rng.EnqueueDie(die); AttackKeeping(g);
                Check(g.State.Phase == Phase.DefenseChoice && g.State.DecisionPlayer == 1 && rival.Health == 8 && rival.Hand.Count == 0 && g.CanCancelAttackWithBox(1));
                var hits = 0; g.DamageReceived += (_, _, _) => hits++; var count = Count(g);
                Check(g.CancelAttackWithBox(1).Success && rival.Health == 8 && hits == 0 && rival.Equipment == null && rival.Memory.Single() == CardCatalog.CardboardBox.Id && Count(g) == count);
                Check(g.State.Phase == Phase.Combat && g.State.PendingAttack == null && g.State.LastCombat.Contains("Caja de Cartón") && g.State.Active.Relikia == 600);
            }
        }),
        ("Caja: se puede recibir el daño sin destruirla y no responde a fallos", () =>
        {
            var (g, rng) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
            rng.EnqueueDie(5); AttackKeeping(g); Check(g.PassDefense(1).Success && rival.Health == 5 && rival.Equipment != null);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add("attack"); rng.EnqueueDie(1);
            AttackKeeping(g); var before = Snapshot(g);
            Check(g.State.Phase == Phase.Combat && !g.CancelAttackWithBox(1).Success && before == Snapshot(g));
        }),
        ("Caja y Destiny son respuestas alternativas; cancelar daño conserva el crítico de Magnet", () =>
        {
            foreach (var useBox in new[] { false, true })
            {
                var (g, rng) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
                rival.Skills.Add(CardCatalog.Destiny.Id); rival.Hand.AddRange(Enumerable.Repeat("attack", 4));
                rng.EnqueueDie(5); AttackKeeping(g);
                Check(useBox ? g.CancelAttackWithBox(1).Success : g.ResolveDefense(1, 0, new[] { 0, 1, 2 }).Success);
                Check(rival.Health == 8 && (useBox ? rival.Hand.Count == 4 && rival.CountersOnSkill(0) == 0 && rival.Equipment == null
                    : rival.Hand.Count == 1 && rival.CountersOnSkill(0) == 1 && rival.Equipment != null));
            }
            var (critical, random) = Fixture(CardCatalog.Magnet.Id); var target = critical.State.Players[1];
            target.Equipment = CardCatalog.CardboardBox.Id; target.Hand.Add("senshuni"); random.EnqueueDie(6); AttackKeeping(critical);
            Check(critical.CancelAttackWithBox(1).Success && critical.State.Phase == Phase.MagnetChoice && target.Health == 8);
            Check(critical.ResolveMagnet(0, false, 0).Success && critical.State.Active.Hand.Single() == "senshuni");
        }),
        ("Caja: rechazar jugador, etapa o equipo incorrectos no modifica el estado", () =>
        {
            var (g, rng) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
            var before = Snapshot(g); Check(!g.CancelAttackWithBox(1).Success && before == Snapshot(g));
            rng.EnqueueDie(5); AttackKeeping(g); before = Snapshot(g);
            Check(!g.CancelAttackWithBox(0).Success && !g.CancelAttackWithBox(-1).Success && !g.Advance(0).Success && before == Snapshot(g));
            rival.Equipment = CardCatalog.Pendant.Id; before = Snapshot(g);
            Check(!g.CancelAttackWithBox(1).Success && before == Snapshot(g));
        }),
        ("Caja cancela código objeto antes de robar; ambos van a sus memorias y los costes se devuelven", () =>
        {
            var (g, _) = ObjectFixture(); var p = g.State.Active; var rival = g.State.Players[1]; var deck = p.Deck.ToArray(); var count = Count(g);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && g.State.DecisionPlayer == 1 && p.Hand.Count == 0 && p.Deck.SequenceEqual(deck));
            Check(g.HasAvailablePlay(1) && !g.HasAvailablePlay(0));
            var before = Snapshot(g); Check(!g.ResolveCodeResponse(0, true).Success && !g.Advance(0).Success && before == Snapshot(g));
            Check(g.ResolveCodeResponse(1, true).Success && p.Hand.Count == 0 && p.Deck.SequenceEqual(deck) && p.Memory.Single() == TestCards.ObjectCode.Id && rival.Memory.Single() == CardCatalog.CardboardBox.Id);
            Check(g.State.Phase == Phase.Actions && g.State.PendingCode == null && rival.Equipment == null && g.State.ActionsLeft == 2 && Count(g) == count);
        }),
        ("Caja: dejar resolver código objeto conserva equipo y activa el efecto una sola vez", () =>
        {
            var (g, _) = ObjectFixture(); var p = g.State.Active; var top = p.Deck.Take(2).ToArray();
            Check(g.PlayCode(0, 0).Success && g.ResolveCodeResponse(1, false).Success && p.Hand.SequenceEqual(top) && g.State.Players[1].Equipment == CardCatalog.CardboardBox.Id);
            var before = Snapshot(g); Check(!g.ResolveCodeResponse(1, false).Success && before == Snapshot(g));
        }),
        ("Caja solo cancela códigos objeto: Senshuni es de personaje", () =>
        {
            var (g, _) = ObjectFixture(); var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("senshuni");
            Check(CardCatalog.Senshuni.CodeType == CodeType.Character && g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 2 && g.State.Players[1].Equipment != null);
            var before = Snapshot(g); Check(!g.ResolveCodeResponse(1, true).Success && before == Snapshot(g));
        }),
        ("Caja puede cancelar activación desde memoria por Relikia sin corromper el código", () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var (g, _) = ObjectFixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(TestCards.ObjectCode.Id); p.Relikia = 400;
                Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && g.State.Phase == Phase.CodeResponse && p.CorruptedMemory.Count == 0 && p.Relikia == 0 && g.State.ActionsLeft == 0);
                var count = Count(g); Check(g.ResolveCodeResponse(1, cancel).Success && Count(g) == count);
                Check(cancel ? p.Hand.Count == 0 && p.Memory.Single() == TestCards.ObjectCode.Id && p.CorruptedMemory.Count == 0
                    : p.Hand.Count == 2 && p.Memory.Count == 0 && p.CorruptedMemory.Single() == TestCards.ObjectCode.Id);
            }
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture(string? skill = null)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Combat; g.State.CurrentPlayer = 0;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add(skill ?? CardCatalog.Destiny.Id); return (g, rng);
    }
    private static (GameEngine, ScriptedRandom) ObjectFixture()
    {
        var (g, rng) = Fixture(); g.State.Phase = Phase.Actions; g.State.ActionsLeft = 2;
        g.State.Active.Hand.Clear(); g.State.Active.Hand.Add(TestCards.ObjectCode.Id); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
        return (g, rng);
    }
    private static void AttackKeeping(GameEngine g)
    {
        Check(g.Attack(0, 0, 0).Success);
        if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Caja de Cartón no satisfecha"); }
}
