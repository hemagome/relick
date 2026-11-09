using Relick.Core;

static class ExtraLifeTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Vida Extra: código objeto real con una copia; los seis resultados curan 1/2/3/3/3/4", () =>
        {
            Check(CardCatalog.ExtraLife.Kind == CardKind.Code && CardCatalog.ExtraLife.CodeType == CodeType.Object && CardCatalog.TrainingDeck().Count(id => id == "extra-life") == 1);
            var expected = new[] { 1, 2, 3, 3, 3, 4 };
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active; var rolls = new List<int>(); g.DieRolled += rolls.Add;
                rng.EnqueueDie(die); var count = Count(g); var deck = p.Deck.ToArray();
                Check(g.PlayCode(0, 0).Success && p.Health == 1 + expected[die - 1] && p.Memory.Single() == "extra-life");
                Check(g.State.ActionsLeft == 1 && p.Relikia == 0 && p.Hand.Count == 0 && p.Deck.SequenceEqual(deck) && rolls.SequenceEqual(new[] { die }) && Count(g) == count);
                Check(p.HasNaturalSix == (die == 6) && !p.AttackRerollUsedThisTurn && g.State.Phase == Phase.Actions);
            }
        }),
        ("Vida Extra respeta el máximo ocho en ambos niveles de Yuno, cura desde cero y conserva el historial", () =>
        {
            foreach (var level in new[] { 1, 2 }) foreach (var health in new[] { 0, 7, 8 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                if (level == 2) p.Relikario = RelikarioCatalog.YunoLv2;
                p.Health = health; p.HasReachedZero = health == 0; p.AttackRerollUsedThisTurn = true;
                rng.EnqueueDie(6); Check(g.PlayCode(0, 0).Success && p.MaxHealth == 8 && p.Health == Math.Min(health + 4, 8) && p.HasReachedZero == (health == 0));
            }
        }),
        ("Vida Extra: Caja cancela antes de lanzar, o permite resolver conservando equipo", () =>
        {
            foreach (var cancel in new[] { true, false })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
                var cues = DuelVisualEvents.Capture(g);
                var rolls = new List<int>(); g.DieRolled += rolls.Add; rng.EnqueueDie(6);
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && rolls.Count == 0 && p.Health == 1);
                Check(g.ResolveCodeResponse(1, cancel).Success && p.Memory.Single() == "extra-life" && g.State.ActionsLeft == (cancel ? 2 : 1));
                Check(cancel ? p.Health == 1 && rolls.Count == 0 && rival.Equipment == null && rival.Memory.Single() == CardCatalog.CardboardBox.Id
                    : p.Health == 5 && rolls.Single() == 6 && rival.Equipment != null);
                Check(cues.Count(c => c.Kind == DuelVisualKind.Heal) == (cancel ? 0 : 1));
            }
        }),
        ("Vida Extra por Relikia se corrompe antes de tirar; cancelarla la deja en memoria", () =>
        {
            foreach (var cancel in new[] { true, false })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add("extra-life"); p.Relikia = 400;
                g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
                rng.EnqueueDie(2); Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.CorruptedMemory.Count == 0 && g.State.Phase == Phase.CodeResponse);
                Check(g.ResolveCodeResponse(1, cancel).Success && p.Relikia == (cancel ? 400 : 0) && g.State.ActionsLeft == (cancel ? 2 : 0));
                Check(cancel ? p.Health == 1 && p.Memory.Single() == "extra-life" && p.CorruptedMemory.Count == 0
                    : p.Health == 3 && p.Memory.Count == 0 && p.CorruptedMemory.Single() == "extra-life");
            }
        }),
        ("Vida Extra y Yuno Lv 2: curación espera al dado final y un 1 detiene los relanzamientos", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2;
            rng.EnqueueDie(2); rng.EnqueueDie(6); rng.EnqueueDie(1);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice && p.Health == 1 && g.State.PendingDieRoll!.Purpose == DiePurpose.ExtraLife);
            Check(g.ResolveDieReroll(0, true).Success && g.State.Phase == Phase.DieRerollChoice && p.Health == 1);
            Check(g.ResolveDieReroll(0, true).Success && g.State.Phase == Phase.Actions && p.Health == 2 && !p.HasNaturalSix);
            var before = Snapshot(g); Check(!g.ResolveDieReroll(0, true).Success && before == Snapshot(g));
        }),
        ("Vida Extra y Colgante: relanzar o sumar modifica la curación sin conceder 6 natural", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
            rng.EnqueueDie(1); rng.EnqueueDie(6); Check(g.PlayCode(0, 0).Success && p.Health == 1);
            Check(g.UsePendant(0, PendantEffect.Reroll).Success && p.Health == 5 && p.Equipment == null && !p.HasNaturalSix && !p.AttackRerollUsedThisTurn);
            var (second, random) = Fixture(); p = second.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Equipment = CardCatalog.Pendant.Id;
            random.EnqueueDie(3); random.EnqueueDie(4);
            Check(second.PlayCode(0, 0).Success && second.ResolveDieReroll(0, true).Success && second.UsePendant(0, PendantEffect.AddTwo).Success);
            Check(second.ResolveDieReroll(0, false).Success && p.Health == 5 && !p.HasNaturalSix && p.Equipment == null);
        }),
        ("Curación visual: ambos jugadores, ganancia efectiva, cero vidas y límite máximo", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var health in new[] { 0, 1, 7, 8 })
            {
                var (g, rng) = Fixture(); g.State.CurrentPlayer = actor; g.State.ActionsLeft = 2;
                var p = g.State.Active; p.Hand.Clear(); p.Hand.Add(CardCatalog.ExtraLife.Id);
                p.Health = health; p.HasReachedZero = health == 0;
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(6);
                Check(g.PlayCode(actor, 0).Success);
                var heal = cues.Where(c => c.Kind == DuelVisualKind.Heal).ToArray();
                var restored = Math.Min(4, p.MaxHealth - health);
                Check(restored == 0 ? heal.Length == 0 : heal.Length == 1 && heal[0].Player == actor && heal[0].Value == restored);
                Check(p.Health == health + restored && p.HasReachedZero == (health == 0));
            }
        }),
        ("Curación visual: espera a resolver el relanzamiento y se emite una sola vez", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Relikario = RelikarioCatalog.YunoLv2;
            var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(2); rng.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && cues.All(c => c.Kind != DuelVisualKind.Heal));
            Check(g.ResolveDieReroll(0, true).Success && cues.All(c => c.Kind != DuelVisualKind.Heal));
            Check(g.ResolveDieReroll(0, false).Success);
            var heal = cues.Single(c => c.Kind == DuelVisualKind.Heal);
            Check(heal.Player == 0 && heal.Value == 4 && cues.Last() == heal);
            Check(!g.ResolveDieReroll(0, false).Success && cues.Count(c => c.Kind == DuelVisualKind.Heal) == 1);
        }),
        ("Vida Extra: rechazos no consumen carta, acción, vida ni azar; el rival prioriza curarse", () =>
        {
            var (g, rng) = Fixture(); g.State.ActionsLeft = 0; var before = Snapshot(g); var rolls = 0; g.DieRolled += _ => rolls++;
            Check(!g.PlayCode(0, 0).Success && before == Snapshot(g) && rolls == 0);
            g.State.ActionsLeft = 2; g.State.CurrentPlayer = 1; var p = g.State.Active; p.Health = 1; p.Hand.Clear(); p.Hand.Add("extra-life");
            rng.EnqueueDie(5); Check(SimulationDriver.Step(g, 1).Success && p.Health == 4 && p.Memory.Single() == "extra-life");
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0; g.State.ActionsLeft = 2;
        g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("extra-life"); g.State.Active.Health = 1;
        g.State.Players[1].Hand.Clear();
        return (g, rng);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Vida Extra no satisfecha"); }
}
