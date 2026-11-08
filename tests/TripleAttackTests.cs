using Relick.Core;
static class TripleAttackTests
{
    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Triple: busca hasta tres Ataques, preserva orden restante y no baraja", () =>
        {
            foreach (var count in Enumerable.Range(1, 5))
            {
                var (g, _) = Fixture(); var p = g.State.Active;
                p.Deck.Clear(); p.Deck.Add("senshuni"); p.Deck.AddRange(Enumerable.Repeat("attack", count)); p.Deck.Add("destiny-shuffle");
                var events = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(0, 0).Success && p.Hand.SequenceEqual(Enumerable.Repeat("attack", Math.Min(3, count))));
                Check(p.Deck.SequenceEqual(new[] { "senshuni" }.Concat(Enumerable.Repeat("attack", Math.Max(0, count - 3))).Append("destiny-shuffle")));
                Check(p.ActionsLeft == 1 && p.Memory.Single() == CardCatalog.TripleAttack.Id);
                Check(events.Count(e => e.Kind == DuelVisualKind.Draw) == Math.Min(3, count) && events.All(e => e.Kind != DuelVisualKind.Shuffle));
            }
        }),
        ("Triple: rechazos atómicos sin ataques, sin acciones, fuera de acciones y jugador rival", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            p.Deck.Clear(); p.Deck.Add("destiny-shuffle"); Reject(g, () => g.PlayCode(0, 0));
            p.Deck.Add("attack"); p.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); p.ActionsLeft = 2;
            g.State.Phase = Phase.Combat; Reject(g, () => g.PlayCode(0, 0)); g.State.Phase = Phase.Actions;
            Reject(g, () => g.PlayCode(1, 0));
            Check(CardCatalog.TrainingDeck().Count == 23 && CardCatalog.TrainingDeck().Count(id => id == "attack") == 3);
        }),
        ("Triple: Error devuelve acción antes de buscar; Caja no responde a Jugada", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; var other = g.State.Players[1];
            other.Equipment = "cardboard-box"; other.Hand.Add("load-error"); var deck = p.Deck.ToArray();
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(1, 0).Success && p.ActionsLeft == 2 && p.Hand.Count == 0 && p.Deck.SequenceEqual(deck) && other.Equipment != null);
            (g, _) = Fixture(); g.State.Players[1].Equipment = "cardboard-box";
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && g.State.Active.Hand.Count == 3);
        }),
        ("Triple: Relikia corrompe y cobra dos acciones, búsqueda privada", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(CardCatalog.TripleAttack.Id); p.Relikia = 400;
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.ActionsLeft == 0 && p.Relikia == 0 && p.Hand.Count == 3);
            Check(p.CorruptedMemory.Single() == CardCatalog.TripleAttack.Id && DuelSnapshot.For(g, 1, 1).Players[0].Hand.All(id => id == null));
        }),
        ("Triple: agotar mazo pausa en reciclaje y no busca ataques del mazo repuesto", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Deck.Clear(); p.Deck.AddRange(["attack", "attack", "attack"]);
            p.Memory.AddRange(["attack", "attack"]); rng.EnqueueDie(2);
            Check(g.PlayCode(0, 0).Success && p.Hand.Count == 3 && g.State.Phase == Phase.RecycleChoice);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveRecycling(0, [0, 1]).Success && g.State.Active.Hand.Count == 3 && g.State.Phase == Phase.Actions);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Actions;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Equipment = null; p.Deck.Clear(); p.Deck.AddRange(["attack", "senshuni", "attack", "destiny-shuffle", "attack"]); }
        g.State.Active.Hand.Add(CardCatalog.TripleAttack.Id); return (g, rng);
    }
    private static void Reject(GameEngine g, Func<CommandResult> action) { var before = g.SaveSnapshot(); Check(!action().Success && g.SaveSnapshot() == before); }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Triple Ataque Cruzado no satisfecha"); }
}
