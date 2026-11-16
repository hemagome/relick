using Relick.Core;
static class EggComboTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Huevo: Bonus y canje de ambos jugadores; repone Bonus y conserva cartas", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            foreach (var zone in new[] { FieldZone.Bonus, FieldZone.Exchange })
            {
                var (g, _) = Fixture(); var p = g.State.Active; var target = g.State.Players[owner];
                g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200;
                if (zone == FieldZone.Bonus) target.Bonus = "senshuni";
                else target.Exchange.Add("senshuni");
                var total = CardCount(g); var deck = target.Deck.Count;
                var placements = 0; g.BonusPlaced += (who, _) => { Check(who == owner); placements++; };
                Check(g.DestructionTargets().Single().Zone == zone && g.PlayCode(0, 0).Success);
                var before = Snapshot(g);
                Check(!g.ResolveDestruction(0, owner, FieldZone.Exchange, 99).Success && Snapshot(g) == before);
                Check(g.ResolveDestruction(0, owner, zone, 0).Success && target.Memory.Contains("senshuni") && CardCount(g) == total);
                Check(p.Relikia == 200 && p.ActionsLeft == 1 && (g.State.Phase == Phase.Decision && g.State.PendingDecision!.Kind == DecisionKind.Confirm));
                Check(g.ResolveBlueEggDraw(0, false).Success && p.Relikia == 200 && p.Hand.Count == 0 && g.State.Phase == Phase.Actions);
                if (zone == FieldZone.Bonus) Check(target.Bonus == "attack" && target.Deck.Count == deck - 1 && placements == 1);
                else Check(target.Exchange.Count == 0 && target.Deck.Count == deck && placements == 0);
            }
        }),
        ("Huevo: habilidades en Bonus o canje no son objetivos y no consumen costes", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200;
            p.Bonus = "magnet-shuffle"; p.Exchange.Add("destiny-shuffle");
            var before = Snapshot(g);
            Check(!g.DestructionTargets().Any() && !g.PlayCode(0, 0).Success && before == Snapshot(g));
        }),
        ("Huevo: destruir en canje conserva la otra carta y permite completar un nuevo canje", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand.Clear(); p.Hand.AddRange(new[] { "blue-egg", "attack" });
            p.Bonus = "senshuni"; p.Exchange.Add("attack");
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, FieldZone.Exchange, 0).Success);
            Check(g.Exchange(0, 0).Success && p.Exchange.Single() == "attack" && p.Bonus == "senshuni");
        }),
        ("Huevo: se activa con 0 PR, destruye una no-habilidad y rechaza objetivos inválidos", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("blue-egg"); g.State.Phase = Phase.Actions;
            var before = Snapshot(g); Check(!g.PlayCode(0, 0).Success && before == Snapshot(g));
            g.State.Players[1].Equipment = "lucky-pendant";
            Check(g.PlayCode(0, 0).Success && p.Relikia == 0 && g.State.ActionsLeft == 1 && g.State.Phase == Phase.Decision);
            before = Snapshot(g); Check(!g.ResolveDestruction(1, 1, true).Success && !g.ResolveDestruction(0, 2, true).Success && !g.Advance(0).Success && before == Snapshot(g));
            Check(g.ResolveDestruction(0, 1, true).Success && g.State.Players[1].Equipment == null && g.State.Players[1].Memory.Single() == "lucky-pendant");
            Check(g.State.Phase == Phase.Actions && !g.ResolveBlueEggDraw(0, true).Success);
            g.State.ActionsLeft = 2; p.Hand.Add("blue-egg"); p.Relikia = 199; p.AttachedCards.Add("senshuni"); p.AttachedCards.Add("magnet-shuffle");
            Check(g.DestructionTargets().Count() == 1 && g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, false, 0).Success && p.Memory.Contains("senshuni") && p.AttachedCards.Single() == "magnet-shuffle");
            Check(g.State.Phase == Phase.Actions && p.Relikia == 199 && p.Hand.Count == 0);
        }),
        ("Huevo: Caja cancela el efecto; Relikia cuesta 400 PR antes del robo opcional", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Hand.Add("blue-egg"); p.Relikia = 200; g.State.Phase = Phase.Actions;
            g.State.Players[1].Equipment = "cardboard-box";
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && g.ResolveCodeResponse(1, true).Success && p.Relikia == 200 && g.State.PendingDecision == null);
            Check(g.State.Phase == Phase.Actions && p.ActionsLeft == 2 && p.Hand.Count == 0);
            p.Memory.Clear(); p.Memory.Add("blue-egg"); p.Equipment = "lucky-pendant"; p.Relikia = 399;
            var before = Snapshot(g); Check(!g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && before == Snapshot(g));
            p.Relikia = 400; Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.Relikia == 0 && p.CorruptedMemory.Single() == "blue-egg" && g.ResolveDestruction(0, 0, true).Success);
            Check(g.State.Phase == Phase.Actions && p.Hand.Count == 0);
        }),
        ("Huevo: tras restaurar la elección, solo el activador puede pagar y roba una sola vez", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand[0] = CardCatalog.BlueEgg.Id; p.Relikia = 200;
            g.State.Players[1].Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && p.Relikia == 200);
            Check(g.ResolveDestruction(0, 1, true).Success && p.Relikia == 200 && p.Hand.Count == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            var choices = DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(choices.Length == 2 && !DuelOptions.For(g, 1).Any(o => o.Move.Kind == DuelMoveKind.Choose));
            var draw = choices.Single(o => o.Primary).Move;
            var before = g.SaveSnapshot();
            Check(!draw.Apply(g, 1).Success && !g.Advance(0).Success && g.SaveSnapshot() == before);
            var deckCount = p.Deck.Count;
            Check(draw.Apply(g, 0).Success && p.Relikia == 0 && p.Deck.Count == deckCount - 1 &&
                p.Hand.Single() == CardCatalog.Attack.Id && p.ActionsLeft == 1 && g.State.Phase == Phase.Actions);
            Check(!draw.Apply(g, 0).Success);
        }),
        ("Huevo: repone el Bonus y espera el reciclaje antes de ofrecer el robo", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var rival = g.State.Players[1];
            g.State.Phase = Phase.Actions; p.Hand[0] = CardCatalog.BlueEgg.Id; p.Relikia = 200;
            rival.Bonus = CardCatalog.Senshuni.Id;
            rival.Deck.Clear(); rival.Deck.Add(CardCatalog.Attack.Id); rng.EnqueueDie(1);
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, FieldZone.Bonus).Success);
            Check(g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == 1 && rival.Bonus == CardCatalog.Attack.Id);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveRecycling(1, [0]).Success && (g.State.Phase == Phase.Decision && g.State.PendingDecision!.Kind == DecisionKind.Confirm));
            Check(g.ResolveBlueEggDraw(0, true).Success && p.Relikia == 0 && p.Hand.Count == 1 && g.State.Phase == Phase.Actions);
        }),
        ("Huevo: robar la última carta abre el reciclaje y cobra una sola vez", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Hand[0] = CardCatalog.BlueEgg.Id; p.Relikia = 200;
            p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id); rng.EnqueueDie(1);
            g.State.Players[1].Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, true).Success);
            Check(g.ResolveBlueEggDraw(0, true).Success && p.Hand.Single() == CardCatalog.Attack.Id && p.Relikia == 0);
            Check(g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveRecycling(0, [0]).Success && g.State.Phase == Phase.Actions &&
                p.Hand.Count == 1 && p.Deck.Single() == CardCatalog.BlueEgg.Id && p.Relikia == 0);
        }),
        ("Huevo: el registro anterior conserva el pago de activación al reconstruir la partida", () =>
        {
            var opening = new DuelOpening
            {
                Names = ["A", "B"], StartingSeat = 0, Hands = [[CardCatalog.BlueEgg.Id], []],
                Decks = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id, CardCatalog.Attack.Id]],
                Bonuses = [null, CardCatalog.Senshuni.Id]
            };
            var (game, rng) = DuelReplay.Restore(opening, [], legacyBlueEgg: true);
            var p = game.State.Active;
            Check(game.Advance(0).Success && !game.PlayCode(0, 0).Success);
            p.Relikia = 400;
            Check(game.PlayCode(0, 0).Success && p.Relikia == 200);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), rng);
            Check(game.ResolveDestruction(0, 1, FieldZone.Bonus).Success &&
                game.State.Active.Relikia == 200 && game.State.Phase == Phase.Actions);
        }),
        ("Re-lanza: gratis sin acciones, relanza 1, roba solo con 1–3, 6 no natural", () =>
        {
            foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.ActionsLeft = 0; p.Hand.Add("reroll"); rng.EnqueueDie(1);
                Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                rng.EnqueueDie(die); Check(g.PlayCode(0, 0).Success);
                if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(0, false).Success);
                Check(g.State.LastAttackDie == die && p.Hand.Count == (die <= 3 ? 1 : 0) && p.ActionsLeft == 0 && !p.AttackRerollUsedThisTurn && !p.HasNaturalSix);
            }
        }),
        ("Re-lanza: no dado ni dado enemigo no cumplen requisitos", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Hand.Add("reroll"); var before = Snapshot(g);
            Check(!g.PlayCode(0, 1).Success && before == Snapshot(g));
            g.State.Players[1].Hand.Add("reroll"); rng.EnqueueDie(2); Check(g.Attack(0, 0, 0).Success); before = Snapshot(g);
            Check(!g.PlayCode(1, 0).Success && before == Snapshot(g));
        }),
        ("Combo: cupo propio fuera de turno, no refresca al entrar en acciones y se renueva cada turno", () =>
        {
            var catalogue = (Dictionary<string, CardDefinition>)CardCatalog.All;
            catalogue["test-paid-combo"] = CardCatalog.Reroll with { Id = "test-paid-combo", ActionCost = 1 };
            try
            {
                var (g, rng) = Fixture(); g.State.CurrentPlayer = 1; var p = g.State.Players[0];
                p.Hand.Clear(); p.Hand.AddRange(new[] { "test-paid-combo", "test-paid-combo" }); p.Bonus = "senshuni"; p.Deck.Clear(); p.Deck.Add("attack"); p.Memory.AddRange(Enumerable.Repeat("attack", 6));
                var rival = g.State.Players[1]; rival.Hand.Add("attack"); rival.Skills.Add("magnet-shuffle");
                rng.EnqueueDie(6); Check(g.Attack(1, 0, 0).Success && g.ResolveAttackReroll(1, false).Success);
                rng.EnqueueDie(2); Check(g.ResolveMagnet(1, true).Success && g.State.DecisionPlayer == 0);
                rng.EnqueueDie(4); Check(g.PlayCode(0, 0).Success && p.ActionsLeft == 1 && rival.ActionsLeft == 2);
                rng.EnqueueDie(6); Check(g.PlayCode(0, 0).Success && p.ActionsLeft == 0 && g.State.CurrentPlayer == 1);
                Check(g.State.Phase == Phase.RecycleChoice);
                Check(g.ResolveRecycling(0, Enumerable.Range(0, g.RecyclingCount)).Success);
                Check(g.Advance(1).Success && rival.ActionsLeft == 2 && p.ActionsLeft == 0 && g.Advance(1).Success && p.ActionsLeft == 2 && rival.ActionsLeft == 2);
            }
            finally { catalogue.Remove("test-paid-combo"); }
        }),
        ("Re-lanza: robo que recicla pausa y reanuda el dado original una sola vez", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Hand.Add("reroll"); p.Deck.Clear(); p.Memory.AddRange(Enumerable.Repeat("attack", 5));
            rng.EnqueueDie(1); Check(g.Attack(0, 0, 0).Success); rng.EnqueueDie(2); rng.EnqueueDie(4);
            Check(g.PlayCode(0, 0).Success && g.State.PendingDieRoll?.Purpose == DiePurpose.Recycling && g.State.PendingAttackRoll == null);
            Check(g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.RecycleChoice);
            Check(g.ResolveRecycling(0, [0, 1, 2, 3]).Success && g.State.PendingAttackRoll?.Die == 3 && p.Hand.Count == 1);
            Check(g.ResolveAttackReroll(0, false).Success && g.State.Phase == Phase.Combat && g.State.PendingDieRoll == null);
        })
    };
    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var rng = new ScriptedRandom(); var g = TestGame.Create(rng); g.State.CurrentPlayer = 0; g.State.Phase = Phase.Combat; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Memory.Clear(); p.Skills.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat("attack", 8)); }
        g.State.Active.Hand.Add("attack"); g.State.Active.Skills.Add("destiny-shuffle"); return (g, rng);
    }
    private static string Snapshot(GameEngine g) => System.Text.Json.JsonSerializer.Serialize(g.State);
    private static int CardCount(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count + (p.Bonus == null ? 0 : 1) + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0));
    private static void Check(bool value) { if (!value) throw new Exception("Regla de Huevo/Combo no satisfecha"); }
}
