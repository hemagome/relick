using Relick.Core;

static class PointsTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Aumento de PR: seis caras, ambos jugadores y ambos signos; mínimo cero", () =>
        {
            var amounts = new[] { 300, 400, 500, 500, 1000, 1500 };
            foreach (var die in Enumerable.Range(1, 6)) foreach (var target in new[] { 0, 1 }) foreach (var increase in new[] { true, false })
            {
                var (g, rng) = Fixture(); foreach (var p in g.State.Players) p.Relikia = 600;
                rng.EnqueueDie(die); Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.PointsChoice && g.State.PendingPoints!.Amount == amounts[die - 1]);
                Check(g.State.Players.All(p => p.Relikia == 600) && g.State.ActionsLeft == 1);
                Check(g.ResolvePointsChange(0, target, increase).Success && g.State.Players[target].Relikia == (increase ? 600 + amounts[die - 1] : Math.Max(0, 600 - amounts[die - 1])));
                Check(g.State.Players[1 - target].Relikia == 600 && g.State.Phase == Phase.Actions && g.State.Active.Memory.Single() == "points-increase");
            }
        }),
        ("Aumento de PR es jugada: Caja no lo cancela; decisiones inválidas son atómicas", () =>
        {
            var (g, rng) = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            rng.EnqueueDie(6); Check(CardCatalog.PointsIncrease.CodeType == CodeType.Play && g.PlayCode(0, 0).Success && g.State.Phase == Phase.PointsChoice);
            var before = Snapshot(g); Check(!g.ResolveCodeResponse(1, true).Success && !g.ResolvePointsChange(1, 0, true).Success && !g.ResolvePointsChange(0, -1, true).Success && !g.Advance(0).Success && before == Snapshot(g));
            Check(g.ResolvePointsChange(0, 0, true).Success && g.State.Active.Relikia == 1500 && g.State.Active.HasNaturalSix);
            before = Snapshot(g); Check(!g.ResolvePointsChange(0, 0, true).Success && before == Snapshot(g));
        }),
        ("Aumento de PR usa el dado final de Yuno y Colgante; Relikia corrompe antes de tirar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Equipment = CardCatalog.Pendant.Id;
            rng.EnqueueDie(2); rng.EnqueueDie(4);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice && g.State.PendingPoints == null);
            Check(g.ResolveDieReroll(0, true).Success && g.UsePendant(0, PendantEffect.AddTwo).Success && g.ResolveDieReroll(0, false).Success);
            Check(g.State.PendingPoints!.Amount == 1500 && !p.HasNaturalSix && g.ResolvePointsChange(0, 0, true).Success && p.Relikia == 1500);
            var (replay, random) = Fixture(); p = replay.State.Active; p.Hand.Clear(); p.Memory.Add("points-increase"); p.Relikia = 400; random.EnqueueDie(5);
            Check(replay.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.CorruptedMemory.Single() == "points-increase" && replay.State.ActionsLeft == 0);
            Check(replay.ResolvePointsChange(0, 0, true).Success && p.Relikia == 1000);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0; g.State.ActionsLeft = 2;
        g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("points-increase"); g.State.Players[1].Hand.Clear(); return (g, rng);
    }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Aumento de PR no satisfecha"); }
}
