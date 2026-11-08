using Relick.Core;

static class SenshuniTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Senshuni es código real con dos copias; jugarlo roba exactamente 2 y cuesta 1 acción", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            Check(CardCatalog.Senshuni.Kind == CardKind.Code && CardCatalog.Senshuni.Draw == 2 && CardCatalog.TrainingDeck().Count(id => id == "senshuni") == 1);
            var top = p.Deck.Take(2).ToArray(); var beforeCount = Count(g); var draws = new List<int>(); g.CardDrawn += (actor, _) => draws.Add(actor);
            Check(g.PlayCode(0, 0).Success && p.Hand.SequenceEqual(top) && p.Memory.Single() == "senshuni" && g.State.ActionsLeft == 1);
            Check(draws.SequenceEqual(new[] { 0, 0 }) && p.Relikia == 0 && p.Health == 8 && Count(g) == beforeCount);
        }),
        ("Senshuni: jugar fuera de etapa, sin acción o con índice inválido no cambia nada", () =>
        {
            var (g, _) = Fixture(); g.State.Phase = Phase.Combat; var before = Snapshot(g);
            Check(!g.PlayCode(0, 0).Success && before == Snapshot(g));
            g.State.Phase = Phase.Actions; before = Snapshot(g);
            Check(!g.PlayCode(1, 0).Success && !g.PlayCode(0, -1).Success && !g.PlayCode(0, 99).Success && before == Snapshot(g));
            g.State.ActionsLeft = 0; before = Snapshot(g); Check(!g.PlayCode(0, 0).Success && before == Snapshot(g));
        }),
        ("Senshuni no limita la mano y conserva cartas al reciclar y agotar el mazo", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.AddRange(Enumerable.Repeat("attack", 10));
            Check(g.PlayCode(0, 0).Success && p.Hand.Count == 12);
            var (last, rng) = Fixture(); p = last.State.Active; p.Deck.Clear(); p.Deck.Add("attack"); rng.EnqueueDie(1);
            var count = Count(last); Check(last.PlayCode(0, 0).Success && last.State.Phase == Phase.RecycleChoice);
            Check(last.ResolveRecycling(0, [0]).Success && p.Recycled && last.State.IsOver && p.Hand.Count == 2 && p.Health == 6 && Count(last) == count);
        }),
        ("Senshuni se puede activar por Relikia y se corrompe antes de robar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add("senshuni"); p.Relikia = 400;
            var next = p.Deck.Take(2).ToArray(); var count = Count(g);
            Check(g.CanUseRelikiaEffect(0, RelikiaEffect.ReplayCode) && g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success);
            Check(p.CorruptedMemory.Single() == "senshuni" && p.Memory.Count == 0 && p.Hand.SequenceEqual(next) && p.Relikia == 0 && g.State.ActionsLeft == 0 && Count(g) == count);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0; g.State.ActionsLeft = 2;
        g.State.Active.Hand.Clear(); g.State.Active.Hand.Add("senshuni"); return (g, rng);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.Skills.Count + p.CorruptedMemory.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Senshuni no satisfecha"); }
}
