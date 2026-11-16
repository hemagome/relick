using Relick.Core;
using System.Text.Json;

static class TimeBombTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Bomba: código objeto, una acción, cero PR y equipo de cualquiera a memoria", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            {
                var (g, _) = Fixture(); var target = g.State.Players[owner]; target.Equipment = CardCatalog.DiamondHelmet.Id;
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision);
                Check(g.ResolveDestruction(0, owner, FieldZone.Equipment).Success && target.Equipment == null && target.Memory.Contains(CardCatalog.DiamondHelmet.Id));
                Check(g.State.Active.ActionsLeft == 1 && g.State.Active.Relikia == 0);
            }
        }),
        ("Bomba: destruye habilidades en Bonus y repone el Bonus de su dueño", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            {
                var (g, _) = Fixture(); var p = g.State.Players[owner]; p.Bonus = CardCatalog.Destiny.Id;
                p.Deck.Clear(); p.Deck.AddRange(new[] { CardCatalog.Attack.Id, CardCatalog.Attack.Id });
                Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, owner, FieldZone.Bonus).Success);
                Check(p.Memory.Contains(CardCatalog.Destiny.Id) && p.Bonus == CardCatalog.Attack.Id && p.Deck.Count == 1);
            }
        }),
        ("Bomba: sortea una carta de cada mano sin permitir escoger el índice", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            {
                var (g, r) = Fixture(); var p = g.State.Players[owner];
                p.Hand.AddRange(new[] { CardCatalog.Attack.Id, CardCatalog.Magnet.Id, CardCatalog.Destiny.Id });
                Check(g.PlayCode(0, 0).Success); var before = State(g);
                Check(!g.ResolveDestruction(0, owner, FieldZone.Hand, 0).Success && State(g) == before);
                r.EnqueueDie(3); var cues = DuelVisualEvents.Capture(g);
                Check(g.ResolveDestruction(0, owner, FieldZone.Hand).Success && p.Hand.SequenceEqual(new[] { CardCatalog.Attack.Id, CardCatalog.Magnet.Id }));
                Check(p.Memory.Contains(CardCatalog.Destiny.Id));
                var moved = cues.Single(c => c.Kind == DuelVisualKind.Pile);
                Check(moved.Player == owner && moved.Source == CardZone.Hand && moved.Value == 2);
            }
        }),
        ("Bomba: las opciones no revelan identidades ni posiciones de la mano rival", () =>
        {
            var (g, _) = Fixture(); g.State.Players[1].Hand.AddRange(new[] { CardCatalog.Magnet.Id, CardCatalog.Destiny.Id });
            Check(g.PlayCode(0, 0).Success);
            var options = DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(options.Length == 1 && options[0].Field is { Index: -1, Owner: 1, Zone: FieldZone.Hand, CardId: "" });
            var text = JsonSerializer.Serialize(options);
            Check(!text.Contains(CardCatalog.Magnet.Id) && !text.Contains(CardCatalog.Destiny.Id) && !text.Contains("Shuffle"));
        }),
        ("Bomba: rechaza zonas vacías, canje, adjuntos y actor incorrecto sin mutar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            p.Exchange.Add(CardCatalog.Attack.Id); p.AttachedCards.Add(CardCatalog.Senshuni.Id);
            var before = State(g); Check(!g.PlayCode(0, 0).Success && State(g) == before);
            g.State.Players[1].Hand.Add(CardCatalog.Attack.Id); Check(g.PlayCode(0, 0).Success); before = State(g);
            Check(!g.ResolveDestruction(1, 1, FieldZone.Hand).Success && !g.ResolveDestruction(0, 0, FieldZone.Exchange, 0).Success &&
                !g.ResolveDestruction(0, 0, FieldZone.Attached, 0).Success && !g.ResolveDestruction(0, 1, FieldZone.Bonus).Success && State(g) == before);
        }),
        ("Bomba: Caja cancela antes de elegir y Error cancela sin consumir azar", () =>
        {
            var (g, _) = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && g.ResolveCodeResponse(1, true).Success);
            Check(g.State.Active.ActionsLeft == 2 && g.State.PendingDecision == null);
            (g, _) = Fixture(); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && g.PlayCode(1, 0).Success);
            Check(g.State.Phase == Phase.Actions && g.State.Active.ActionsLeft == 2 && g.State.PendingDecision == null);
        }),
        ("Bomba: no se atasca si los contraerrores consumen los únicos objetivos", () =>
        {
            var (g, _) = Fixture(); g.State.Active.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success && g.PlayCode(0, 0).Success);
            Check(g.State.Phase == Phase.Actions && g.State.PendingDecision == null && g.State.Players.All(p => p.Hand.Count == 0));
        }),
        ("Bomba: recuperar la elección y el azar produce el mismo resultado", () =>
        {
            var (g, _) = Fixture(); g.State.Players[1].Hand.AddRange(new[] { CardCatalog.Magnet.Id, CardCatalog.Attack.Id });
            Check(g.PlayCode(0, 0).Success); var saved = g.SaveSnapshot();
            var a = new DuelRandom(); a.Replay([1]); var first = GameEngine.FromSnapshot(saved, a);
            Check(!first.ResolveDestruction(0, 1, FieldZone.Hand, 0).Success && first.SaveSnapshot() == saved);
            Check(first.ResolveDestruction(0, 1, FieldZone.Hand).Success); a.Finish();
            var b = new DuelRandom(); b.Replay([1]); var second = GameEngine.FromSnapshot(saved, b);
            Check(second.ResolveDestruction(0, 1, FieldZone.Hand).Success); b.Finish(); Check(State(first) == State(second));
        }),
        ("Bomba: pasar Error cierra la respuesta durante la elección y conserva los objetivos", () =>
        {
            var (g, _) = Fixture(); g.State.Active.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.PassErrorResponse(1).Success && g.State.Phase == Phase.Decision);
            var before = g.SaveSnapshot(); Check(!g.PlayCode(1, 0).Success && before == g.SaveSnapshot());
            Check(!g.CanPlayLoadError(1) && g.ResolveDestruction(0, 1, FieldZone.Hand).Success);
            Check(g.State.Phase == Phase.Actions && g.State.PendingDecision == null && g.State.Players[1].Hand.Count == 0);
            Check(g.State.Active.Hand.Single() == CardCatalog.LoadError.Id && g.State.Active.ActionsLeft == 1);
        }),
        ("Bomba: Relikia consume dos acciones y 400 PR y corrompe el código", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(CardCatalog.TimeBomb.Id); p.Relikia = 400;
            g.State.Players[1].Bonus = CardCatalog.Magnet.Id;
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.Relikia == 0 && p.ActionsLeft == 0 && p.CorruptedMemory.Contains(CardCatalog.TimeBomb.Id));
            Check(g.ResolveDestruction(0, 1, FieldZone.Bonus).Success);
        })
    };
    static (GameEngine, ScriptedRandom) Fixture()
    {
        var r = new ScriptedRandom(); var g = TestGame.Create(r); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Skills.Clear(); p.Bonus = null; p.ActionsLeft = 2; }
        g.State.Active.Hand.Add(CardCatalog.TimeBomb.Id); return (g, r);
    }
    static string State(GameEngine g) => g.SaveSnapshot();
    static void Check(bool value) { if (!value) throw new Exception("Regla de Bomba de Tiempo incorrecta."); }
}
