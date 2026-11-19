using Relick.Core;

static class MindResetTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Resetear: Jugada COMBO cuesta una acción y mazo conserva 42 cartas", () =>
        {
            var c = CardCatalog.MindReset;
            Check(c.IsCombo && c.CodeType == CodeType.Play && c.ActionCost == 1);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Resetear: memorias propias, rivales o mixtas; baraja solo mazos afectados y roba solo objetivo", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var target in new[] { 0, 1 })
            foreach (var owners in new[] { new[] { 0, 0 }, new[] { 1, 1 }, new[] { 0, 1 } })
            {
                var (g, _) = Fixture(actor); var total = Count(g);
                var cues = DuelVisualEvents.Capture(g); var hands = g.State.Players.Select(p => p.Hand.Count).ToArray();
                Check(g.PlayCode(actor, 0).Success && g.State.Players[actor].ActionsLeft == 1);
                Check(g.ChooseMindResetTarget(actor, target).Success);
                Check(g.ChooseMindResetMemory(actor, owners[0], 0).Success);
                Check(g.ChooseMindResetMemory(actor, owners[1], owners[0] == owners[1] ? 1 : 0).Success);
                Check(g.State.Phase == Phase.Actions);
                Check(g.State.Players[actor].Memory.Contains(CardCatalog.MindReset.Id));
                Check(Count(g) == total && g.State.Players[target].Hand.Count == hands[target] - (target == actor ? 1 : 0));
                Check(cues.Count(v => v.Kind == DuelVisualKind.Draw && v.Player == target) == 1);
                Check(cues.Where(v => v.Kind == DuelVisualKind.Shuffle).Select(v => v.Player).Order().SequenceEqual(owners.Append(target).Distinct().Order()));
                foreach (var owner in Enumerable.Range(0, 2))
                    Check(g.State.Players[owner].Deck.Count == 4 + owners.Count(o => o == owner));
            }
        }),
        ("Resetear: mira solo mano elegida en proyección privada y recupera cada selección", () =>
        {
            var (g, rng) = Fixture(); g.State.Players[1].Hand.Add(CardCatalog.Attack.Id);
            Check(g.PlayCode(0, 0).Success);
            Check(DuelSnapshot.For(g, 0, 1).Players[1].Hand.All(id => id == null));
            Check(g.ChooseMindResetTarget(0, 1).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(DuelSnapshot.For(g, 0, 2).Players[1].Hand.SequenceEqual(g.State.Players[1].Hand));
            Check(DuelSnapshot.For(g, -1, 2).Players[1].Hand.All(id => id == null));
            Check(DuelSnapshot.For(g, 1, 2).Players[0].Hand.All(id => id == null));
            Check(g.ChooseMindResetMemory(0, 0, 0).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ChooseMindResetMemory(0, 1, 0).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveMindReset(0, 0).Success && g.State.PendingDecision == null);
            Check(DuelSnapshot.For(g, 0, 3).Players[1].Hand.All(id => id == null));
        }),
        ("Resetear: rechazos atómicos, sin acciones, memoria insuficiente, habilidad y selección duplicada", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.ActionsLeft = 0;
            Reject(g, () => g.PlayCode(0, 0)); p.ActionsLeft = 2;
            foreach (var player in g.State.Players) player.Memory.Clear();
            Reject(g, () => g.PlayCode(0, 0)); p.Memory.AddRange(["attack", "attack"]);
            g.State.Players[1].Hand.Add(CardCatalog.Attack.Id);
            Check(g.PlayCode(0, 0).Success);
            Reject(g, () => g.ChooseMindResetTarget(1, 0));
            Reject(g, () => g.ChooseMindResetTarget(0, 7));
            Check(g.ChooseMindResetTarget(0, 1).Success);
            Reject(g, () => g.ChooseMindResetMemory(0, -1, 0));
            Check(g.ChooseMindResetMemory(0, 0, 0).Success);
            Reject(g, () => g.ChooseMindResetMemory(0, 0, 0));
            Check(g.ChooseMindResetMemory(0, 0, 1).Success);
            Reject(g, () => g.ResolveMindReset(0, 1)); // Destiny is a skill.
            Reject(g, () => g.ResolveMindReset(1, 0));
            Check(g.ResolveMindReset(0, 0).Success);
        }),
        ("Resetear: Error solo cancela antes de revelar la mano; la selección pendiente no se puede cancelar", () =>
        {
            foreach (var late in new[] { false, true })
            {
                var (g, _) = Fixture(); var other = g.State.Players[1]; other.Equipment = "cardboard-box";
                if (!late) other.Hand.Add("load-error");
                var memories = g.State.Players.Select(p => p.Memory.ToArray()).ToArray();
                Check(g.PlayCode(0, 0).Success);
                if (late)
                {
                    Check(g.ChooseMindResetTarget(0, 1).Success && g.ChooseMindResetMemory(0, 1, 0).Success);
                    other.Hand.Add("load-error");
                    Reject(g, () => g.PlayCode(1, other.Hand.Count - 1));
                    Check(!g.CanPlayLoadError(1) && g.State.PendingDecision is { Source: "mind-reset", ShowsHandOf: 1 } && g.State.Active.ActionsLeft == 1);
                    Check(g.ChooseMindResetMemory(0, 0, 0).Success && g.ResolveMindReset(0, 0).Success);
                    continue;
                }
                Check(g.PlayCode(1, other.Hand.Count - 1).Success && g.State.Phase == Phase.Actions);
                Check(g.State.PendingDecision == null && g.State.Active.ActionsLeft == 2 && other.ActionsLeft == 1);
                Check(g.State.Active.Memory.SequenceEqual(memories[0].Append(CardCatalog.MindReset.Id)));
                Check(other.Memory.SequenceEqual(memories[1].Append("load-error")) && other.Equipment == "cardboard-box");
            }
        }),
        ("Resetear: COMBO en ataque propio o rival conserva dado y descuenta al actor correcto", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat;
                var attacker = g.State.Players[0]; attacker.Skills.Add("destiny-shuffle"); attacker.Hand.Add("attack");
                rng.EnqueueDie(2); Check(g.Attack(0, attacker.Hand.Count - 1, 0).Success);
                Check(g.PlayCode(actor, 0).Success && g.State.DecisionPlayer == actor);
                Check(g.ChooseMindResetTarget(actor, 1 - actor).Success);
                Check(g.ChooseMindResetMemory(actor, 0, 0).Success && g.ChooseMindResetMemory(actor, 1, 0).Success);
                Check(g.State.Phase == Phase.AttackRerollChoice);
                Check(g.State.PendingAttackRoll is { Die: 2, Modified: false } && g.State.Players[actor].ActionsLeft == 1);
                Check(g.ResolveAttackReroll(0, false).Success && g.Advance(0).Success);
                Check(attacker.ActionsLeft == (actor == 0 ? 1 : 2));
            }
        }),
        ("Resetear: Relikia corrompe y cobra dos acciones; no ofrece reciclaje con memoria insuficiente", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.RemoveAt(0);
            p.Memory.Add(CardCatalog.MindReset.Id); p.Relikia = 400;
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 2).Success && p.ActionsLeft == 0 && p.Relikia == 0);
            Check(p.CorruptedMemory.Single() == CardCatalog.MindReset.Id);
            Check(g.ChooseMindResetTarget(0, 1).Success && g.ChooseMindResetMemory(0, 0, 0).Success && g.ChooseMindResetMemory(0, 1, 0).Success);
            Check(g.State.Phase == Phase.Actions);
            (g, _) = Fixture(); p = g.State.Active; p.Hand.Clear(); p.Memory.Clear(); g.State.Players[1].Memory.Clear();
            p.Memory.AddRange([CardCatalog.MindReset.Id, "attack"]); p.Relikia = 400;
            Check(!g.CanReplayMemoryCode(0, 0));
        }),
        ("Resetear: robo que agota mazo pausa y recupera el dado de curación original", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Relikario = RelikarioCatalog.YunoLv2; p.Health = 1; p.Hand.Add("extra-life");
            rng.EnqueueDie(4); Check(g.PlayCode(0, p.Hand.Count - 1).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.PlayCode(0, 0).Success && g.ChooseMindResetTarget(0, 1).Success);
            g.State.Players[1].Deck.Clear(); rng.EnqueueDie(2);
            Check(g.ChooseMindResetMemory(0, 0, 0).Success && g.ChooseMindResetMemory(0, 0, 1).Success);
            Check(g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveRecycling(1, Enumerable.Range(0, g.RecyclingCount)).Success && g.State.Phase == Phase.DieRerollChoice);
            Check(g.State.PendingDieRoll is { Player: 0, Die: 4, Purpose: DiePurpose.ExtraLife });
            Check(g.ResolveDieReroll(0, false).Success && g.State.Players[0].Health == 4 && g.State.Players[0].ActionsLeft == 0);
        }),
        ("Resetear: mano solo habilidades no mueve memorias ni roba tras mirarla", () =>
        {
            var (g, _) = Fixture(); var target = g.State.Players[1]; target.Hand.Clear(); target.Hand.Add("destiny-shuffle");
            var deck = target.Deck.ToArray(); var memory = target.Memory.ToArray();
            Check(g.PlayCode(0, 0).Success && g.ChooseMindResetTarget(0, 1).Success);
            Check(g.ResolveMindReset(0, -1).Success && target.Deck.SequenceEqual(deck) && target.Memory.SequenceEqual(memory));
            Check(g.State.Active.ActionsLeft == 1 && g.State.Phase == Phase.Actions);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng);
        g.State.CurrentPlayer = actor; g.State.Phase = Phase.Actions; g.State.Turn = 2;
        foreach (var p in g.State.Players)
        {
            p.Hand.Clear(); p.Hand.AddRange(["senshuni", "destiny-shuffle"]);
            p.Memory.Clear(); p.Memory.AddRange(["attack", "magnet-shuffle"]);
            p.Skills.Clear(); p.Equipment = null;
            p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 4));
        }
        g.State.Active.Hand.Insert(0, CardCatalog.MindReset.Id); return (g, rng);
    }
    private static int Count(GameEngine g) => g.State.Players.Sum(p => p.Deck.Count + p.Hand.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.AttachedCards.Count + p.Exchange.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1));
    private static void Reject(GameEngine g, Func<CommandResult> act) { var before = g.SaveSnapshot(); Check(!act().Success && g.SaveSnapshot() == before); }
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Resetear la Mente no satisfecha"); }
}
