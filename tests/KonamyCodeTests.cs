using Relick.Core;
using System.Text.Json;

static class KonamyCodeTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Konamy: Jugada sin COMBO, una acción, cero PR y mazo de desarrollo", () =>
        {
            var c = CardCatalog.KonamyCode;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Play && !c.IsCombo && c.ActionCost == 1 && c.PrCost == 0);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Konamy: permite ataques, equipos y códigos, excluye habilidades y conserva el orden del mazo", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var target in new[] { 1, 2, 4 })
            {
                var (g, _) = Fixture(actor); var p = g.State.Active;
                var before = p.Deck.ToArray(); var total = Count(g); var cues = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(actor, 0).Success && g.State.Phase == Phase.Decision && p.ActionsLeft == 1 && p.Relikia == 0);
                Check(g.SearchableDeckCards(actor).SequenceEqual(new[] { 1, 2, 4 }));
                Check(g.ResolveDeckSearch(actor, target).Success && p.Hand.Single() == before[target] && p.Memory.Single() == CardCatalog.KonamyCode.Id);
                Check(p.Deck.SequenceEqual(before.Where((_, i) => i != target)) && Count(g) == total && g.State.Phase == Phase.Actions);
                Check(cues.Count(v => v.Kind == DuelVisualKind.Draw && v.Player == actor) == 1 && cues.All(v => v.Kind != DuelVisualKind.Shuffle));
            }
        }),
        ("Konamy: valida turno, fase, costes y no-habilidades disponibles antes de cobrar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; var before = g.SaveSnapshot();
            Check(!g.PlayCode(1, 0).Success && !g.PlayCode(0, -1).Success && before == g.SaveSnapshot());
            g.State.Phase = Phase.Combat; before = g.SaveSnapshot(); Check(!g.PlayCode(0, 0).Success && before == g.SaveSnapshot());
            g.State.Phase = Phase.Actions; p.ActionsLeft = 0; before = g.SaveSnapshot(); Check(!g.PlayCode(0, 0).Success && before == g.SaveSnapshot());
            p.ActionsLeft = 2;
            foreach (var deck in new[] { Array.Empty<string>(), new[] { CardCatalog.Magnet.Id, CardCatalog.Destiny.Id } })
            {
                p.Deck.Clear(); p.Deck.AddRange(deck); p.Memory.Add(CardCatalog.KonamyCode.Id); p.Relikia = 400; before = g.SaveSnapshot();
                Check(!g.PlayCode(0, 0).Success && !g.CanReplayMemoryCode(0, 0) && before == g.SaveSnapshot());
                Check(DuelOptions.For(g, 0).All(o => o.Move.Kind != DuelMoveKind.PlayCode));
            }
        }),
        ("Konamy: rechaza habilidades, índices y actor falsos sin cambios ni consumo de azar", () =>
        {
            var (g, _) = Fixture(); Check(g.PlayCode(0, 0).Success);
            var random = new DuelRandom(); random.Record(_ => throw new Exception("La búsqueda no debe consumir azar."));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random); var before = g.SaveSnapshot();
            Check(!g.ResolveDeckSearch(1, 1).Success && !g.ResolveDeckSearch(-1, 1).Success);
            foreach (var index in new[] { -1, 0, 3, 99 }) Check(!g.ResolveDeckSearch(0, index).Success && g.SaveSnapshot() == before);
            Check(!g.Advance(0).Success && g.ResolveDeckSearch(0, 2).Success && random.Finish().Count == 0);
            before = g.SaveSnapshot(); Check(!g.ResolveDeckSearch(0, 1).Success && g.SaveSnapshot() == before);
        }),
        ("Konamy: solo su dueño recibe objetivos e ilustraciones; el rival no conoce la carta añadida", () =>
        {
            var (g, _) = Fixture(); Check(g.PlayCode(0, 0).Success);
            var own = DuelSnapshot.For(g, 0, 1); var rival = DuelSnapshot.For(g, 1, 1); var board = DuelSnapshot.For(g, -1, 1);
            var options = own.Options.Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            var deck = g.State.Players[0].Deck;
            Check(options.Select(o => o.CardId).SequenceEqual(new[] { deck[1], deck[2], deck[4] }) && options.All(o => o.CardId != null));
            Check(rival.Options.All(o => o.CardId == null && o.Move.Kind != DuelMoveKind.Choose) && board.Options.Count == 0);
            // Both players see that a search is pending, never what it offers.
            Check(rival.Decision is { Kind: DecisionKind.ChooseCard, Decider: 0 } && !JsonSerializer.Serialize(rival.Decision).Contains(deck[1]));
            Check(!JsonSerializer.Serialize(rival).Contains(CardCatalog.DiamondHelmet.Id));
            Check(g.ResolveDeckSearch(0, 2).Success && DuelSnapshot.For(g, 1, 2).Players[0].Hand.Single() == null);
            Check(!DuelSnapshot.For(g, 1, 2).LastCombat.Contains(CardCatalog.DiamondHelmet.Name));
        }),
        ("Konamy: Error solo anula antes de buscar; pasar cierra la ventana y Caja no responde", () =>
        {
            var (g, _) = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision && !g.ResolveCodeResponse(1, true).Success);
            foreach (var during in new[] { false, true })
            {
                (g, _) = Fixture(); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
                if (during)
                {
                    Check(g.PassErrorResponse(1).Success && g.State.Phase == Phase.Decision);
                    var before = g.SaveSnapshot();
                    Check(!g.CanPlayLoadError(1) && !g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
                    Check(g.ResolveDeckSearch(0, 1).Success && g.State.Active.ActionsLeft == 1);
                    continue;
                }
                Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Actions && g.State.Active.ActionsLeft == 2);
                Check(g.State.Active.Hand.Count == 0 && g.State.Active.Deck.Count == 5 && g.State.Active.Memory.Contains(CardCatalog.KonamyCode.Id));
                Check(!g.ResolveDeckSearch(0, 1).Success);
            }
        }),
        ("Konamy: recuperar la elección y responder con contraerror conserva su búsqueda", () =>
        {
            var (g, r) = Fixture(); g.State.Active.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success);
            var saved = g.SaveSnapshot(); var clone = GameEngine.FromSnapshot(saved, r);
            Check(g.PassErrorResponse(1).Success && clone.PassErrorResponse(1).Success);
            Check(g.ResolveDeckSearch(0, 4).Success && clone.ResolveDeckSearch(0, 4).Success && g.SaveSnapshot() == clone.SaveSnapshot());
            g = GameEngine.FromSnapshot(saved, r);
            Check(g.PlayCode(1, 0).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision);
            Check(g.ResolveDeckSearch(0, 1).Success && g.State.Phase == Phase.Actions && g.State.Active.Hand.Single() == CardCatalog.Attack.Id);
        }),
        ("Konamy: Relikia corrompe el código, cobra dos acciones y 400 PR, y Error los devuelve", () =>
        {
            foreach (var cancel in new[] { 0, 1, 2 })
            {
                var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(CardCatalog.KonamyCode.Id); p.Relikia = 400;
                if (cancel > 0) g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success);
                if (cancel > 0)
                {
                    if (cancel == 2)
                    {
                        Check(g.PassErrorResponse(1).Success && g.State.Phase == Phase.Decision && p.CorruptedMemory.Count == 1);
                        var before = g.SaveSnapshot();
                        Check(!g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
                        Check(g.ResolveDeckSearch(0, 1).Success && p.Relikia == 0 && p.ActionsLeft == 0);
                        continue;
                    }
                    Check(g.PlayCode(1, 0).Success && p.Relikia == 400 && p.ActionsLeft == 2 && p.CorruptedMemory.Count == 0);
                    Check(p.Memory.Single() == CardCatalog.KonamyCode.Id);
                }
                else
                {
                    Check(p.Relikia == 0 && p.ActionsLeft == 0 && p.CorruptedMemory.Single() == CardCatalog.KonamyCode.Id);
                    Check(g.ResolveDeckSearch(0, 1).Success && p.Hand.Single() == CardCatalog.Attack.Id);
                }
            }
        }),
        ("Konamy: buscar la última carta activa reciclaje o segundo agotamiento con vidas inevitables", () =>
        {
            foreach (var recycled in new[] { false, true })
            {
                var (g, r) = Fixture(); var p = g.State.Active; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
                p.Recycled = recycled; p.Equipment = CardCatalog.DiamondHelmet.Id;
                Check(g.PlayCode(0, 0).Success); r.EnqueueDie(6);
                Check(g.ResolveDeckSearch(0, 0).Success && p.Hand.Single() == CardCatalog.Attack.Id);
                if (recycled) Check(g.State.IsOver && p.Health == 6 && g.State.Winner == 1);
                else
                {
                    Check(g.State.Phase == Phase.RecycleChoice && g.RecyclingCount == 1);
                    g = GameEngine.FromSnapshot(g.SaveSnapshot(), r);
                    Check(g.ResolveRecycling(0, [0]).Success && g.State.Phase == Phase.Actions && g.State.Active.Deck.Single() == CardCatalog.KonamyCode.Id);
                }
            }
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var random = new ScriptedRandom(); var g = new GameEngine(random); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = actor;
        g.State.Active.Hand.Add(CardCatalog.KonamyCode.Id); g.State.Active.Deck.Clear();
        g.State.Active.Deck.AddRange(new[] { CardCatalog.Magnet.Id, CardCatalog.Attack.Id, CardCatalog.DiamondHelmet.Id, CardCatalog.Destiny.Id, CardCatalog.ExtraLife.Id });
        return (g, random);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count);
    static void Check(bool value) { if (!value) throw new Exception("Regla de Código Konamy no satisfecha."); }
}
