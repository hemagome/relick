using System.Text.Json;
using System.Text.Json.Nodes;
using Relick.Core;

static class DecisionTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Decisión: Konamy por el protocolo ofrece cartas solo a quien busca y el historial no las nombra", () =>
        {
            var g = Fixture(CardCatalog.KonamyCode.Id); var p = g.State.Active;
            p.Deck.Clear(); p.Deck.AddRange([CardCatalog.Magnet.Id, CardCatalog.DiamondHelmet.Id, CardCatalog.Senshuni.Id]);
            g.PublicHistory = new DuelHistory();
            Check(new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 0 }.Apply(g, 0).Success && g.State.Phase == Phase.Decision);
            var own = DuelSnapshot.For(g, 0, 1); var rival = DuelSnapshot.For(g, 1, 1);
            var choices = own.Options.Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(choices.Select(o => o.CardId).SequenceEqual([CardCatalog.DiamondHelmet.Id, CardCatalog.Senshuni.Id]) && choices.All(o => o.Button == "Añadir a mano"));
            Check(own.Decision is { Kind: DecisionKind.ChooseCard, Decider: 0, Title: "Código Konamy" } && rival.Decision?.Instruction == own.Decision.Instruction);
            Check(!rival.Options.Any(o => o.Move.Kind == DuelMoveKind.Choose) && !JsonSerializer.Serialize(rival).Contains(CardCatalog.DiamondHelmet.Id));
            Check(choices[0].Move.Apply(g, 0).Success && p.Hand.Single() == CardCatalog.DiamondHelmet.Id && g.State.PendingDecision == null && g.State.Phase == Phase.Actions);
            var line = g.PublicHistory.Entries.Last().Text;
            Check(line == $"{p.Name} añade una carta del mazo a su mano." && !JsonSerializer.Serialize(DuelSnapshot.For(g, 1, 2)).Contains(CardCatalog.DiamondHelmet.Name));
        }),
        ("Decisión: el robo opcional de Huevo es una confirmación sin COMBO; el objetivo admite COMBO como antes", () =>
        {
            var g = Fixture(CardCatalog.BlueEgg.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]); p.Relikia = 200; p.ActionsLeft = 2;
            rival.Equipment = CardCatalog.Pendant.Id; p.Bonus = CardCatalog.Senshuni.Id;
            Check(g.PlayCode(0, 0).Success && g.State.PendingDecision is { Step: "target", Kind: DecisionKind.ChooseTarget, AllowsCombos: true });
            var star = p.Hand.IndexOf(CardCatalog.PlumberStar.Id);
            Check(g.CodeUnavailableReason(0, star) == null);
            var target = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Field?.Zone == FieldZone.Equipment);
            Check(target.Field is { Owner: 1, Zone: FieldZone.Equipment } && target.Button == "Destruir" && target.CardId == null);
            Check(target.Move.Apply(g, 0).Success && rival.Equipment == null && g.State.PendingDecision is { Step: "draw", Kind: DecisionKind.Confirm, AllowsCombos: false });
            Check(g.CodeUnavailableReason(0, p.Hand.IndexOf(CardCatalog.PlumberStar.Id)) != null);
            var options = DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(options.Length == 2 && options[0].Primary && options[0].Label == "Robar 1 carta · 200 PR" && options[1].Label == "Continuar sin robar");
            var hand = p.Hand.Count;
            Check(options[0].Move.Apply(g, 0).Success && p.Relikia == 0 && p.Hand.Count == hand + 1 && g.State.Phase == Phase.Actions);
        }),
        ("Decisión: Estrella del Fontanero interrumpe la elección de Huevo y la devuelve intacta", () =>
        {
            var g = Fixture(CardCatalog.BlueEgg.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id]); p.ActionsLeft = 2; rival.Bonus = CardCatalog.Senshuni.Id; p.Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision);
            var pending = g.State.PendingDecision!;
            Check(g.PlayCode(0, p.Hand.IndexOf(CardCatalog.PlumberStar.Id)).Success && g.State.PendingDecision == null && g.State.Phase == Phase.TurnBuffDiscard);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ScriptedRandom()); p = g.State.Active; rival = g.State.Players[1];
            Check(g.ResolveTurnBuffDiscard(0, 0).Success && p.StarEffects == 1);
            Check(g.State.Phase == Phase.Decision && g.State.PendingDecision is { Source: "blue-egg", Step: "target" } restored && restored.ResumePhase == pending.ResumePhase);
            Check(g.ResolveDestruction(0, 1, FieldZone.Bonus).Success && rival.Memory.Contains(CardCatalog.Senshuni.Id) && g.State.Phase == Phase.Actions);
        }),
        ("Decisión: un duelo guardado con la fase anterior de Huevo se abre como decisión y acepta sus jugadas de entonces", () =>
        {
            var g = Fixture(CardCatalog.BlueEgg.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Relikia = 200; rival.Equipment = CardCatalog.Pendant.Id; p.Bonus = CardCatalog.Senshuni.Id;
            Check(g.PlayCode(0, 0).Success);
            // Rewrite the save as the previous version wrote it: the destruction phase and its actor, no decision.
            var json = JsonNode.Parse(g.SaveSnapshot())!; var state = json["State"]!.AsObject();
            state["Phase"] = (int)Phase.DestroyChoice; state["DestroyActor"] = 0; state.Remove("PendingDecision");
            var legacy = GameEngine.FromSnapshot(json.ToJsonString(), new ScriptedRandom()); p = legacy.State.Active; rival = legacy.State.Players[1];
            Check(legacy.State.PendingDecision is { Source: "blue-egg", Step: "target", AllowsCombos: true, Decider: 0 } && legacy.State.DestroyActor == null);
            Check(new DuelMove { Kind = DuelMoveKind.Destroy, Target = 1, Value = (int)FieldZone.Equipment, Index = -1 }.Apply(legacy, 0).Success && rival.Equipment == null);
            Check(legacy.State.PendingDecision is { Source: "blue-egg", Step: "draw" } && legacy.ResolveBlueEggDraw(0, false).Success &&
                legacy.State.Phase == Phase.Actions && p.Relikia == 200);
        }),
        ("Decisión: una tirada guardada antes de mover Vida Extra a su módulo termina con la regla anterior", () =>
        {
            var g = Fixture(CardCatalog.ExtraLife.Id); var p = g.State.Active; p.Health = 3; p.Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice && g.State.PendingDieRoll!.Purpose == DiePurpose.ExtraLife);
            var json = JsonNode.Parse(g.SaveSnapshot())!; var die = json["PendingDie"]!.AsObject();
            Check(die["Step"]?.GetValue<string>() == "heal"); die.Remove("Step"); die["SkillId"] = "";
            var legacy = GameEngine.FromSnapshot(json.ToJsonString(), new ScriptedRandom()); var rolled = legacy.State.PendingDieRoll!.Die;
            Check(legacy.ResolveDieReroll(0, false).Success && legacy.State.Active.Health == Math.Min(8, 3 + rolled switch { 1 => 1, 2 => 2, 6 => 4, _ => 3 }));
        }),
        ("Decisión: Aumento de PR guardado en su fase anterior se termina con su jugada de entonces", () =>
        {
            var g = Fixture(CardCatalog.PointsIncrease.Id); var p = g.State.Active; p.Dexterity = 0;
            Check(g.PlayCode(0, 0).Success && g.State.PendingDecision is { Kind: DecisionKind.Confirm } decision && decision.Value > 0);
            var amount = g.State.PendingDecision!.Value;
            Check(DuelOptions.For(g, 0).Count(o => o.Move.Kind == DuelMoveKind.Choose) == 4 && DuelOptions.For(g, 0).Single(o => o.Primary).Button == $"+{amount} a mis PR");
            var json = JsonNode.Parse(g.SaveSnapshot())!; var state = json["State"]!.AsObject();
            state.Remove("PendingDecision"); state["Phase"] = (int)Phase.PointsChoice;
            state["PendingPoints"] = new JsonObject { ["Actor"] = 0, ["Die"] = 6, ["Amount"] = amount };
            var legacy = GameEngine.FromSnapshot(json.ToJsonString(), new ScriptedRandom());
            var points = DuelOptions.For(legacy, 0).Where(o => o.Move.Kind == DuelMoveKind.Points).ToArray();
            Check(points.Length == 4);
            var result = legacy.ResolvePointsChange(0, 0, true);
            Check(result.Success, result.Message);
            Check(legacy.State.Active.Relikia == p.Relikia + amount && legacy.State.Phase == Phase.Actions);
        }),
        ("Decisión: Resetear la Mente pausa la búsqueda de Konamy, muestra la mano solo a quien decide y la devuelve intacta", () =>
        {
            var g = Fixture(CardCatalog.KonamyCode.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.AddRange([CardCatalog.MindReset.Id, CardCatalog.Attack.Id]); p.ActionsLeft = 2; p.Deck[0] = CardCatalog.Pendant.Id;
            p.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Magnet.Id]); rival.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Destiny.Id, CardCatalog.Attack.Id]);
            g.PublicHistory = new DuelHistory();
            Check(g.PlayCode(0, 0).Success && g.State.PendingDecision is { Source: "konamy-code" } search);
            Check(g.PlayCode(0, p.Hand.IndexOf(CardCatalog.MindReset.Id)).Success);
            Check(g.State.PendingDecision is { Source: "mind-reset", Step: "target", Kind: DecisionKind.Confirm, Suspended.Source: "konamy-code" });
            var targets = DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(targets.Single(o => o.Button == "Mano rival").Move.Apply(g, 0).Success && g.State.PendingDecision is { Step: "memory", ShowsHandOf: 1 });
            Check(DuelSnapshot.For(g, 0, 1).Players[1].Hand.SequenceEqual(rival.Hand) && DuelSnapshot.For(g, 1, 1).Players[0].Hand.All(c => c == null));
            Check(DuelSnapshot.For(g, -1, 1).Players[1].Hand.All(c => c == null) && !DuelOptions.For(g, 1).Any(o => o.Move.Kind == DuelMoveKind.Choose));
            // Saved halfway through, the picks and the paused search come back.
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ScriptedRandom()); p = g.State.Active; rival = g.State.Players[1]; g.PublicHistory = new DuelHistory();
            Check(g.ResolveDecision(0, 0).Success && g.State.PendingDecision is { Step: "memory", Picks.Count: 1 });
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ScriptedRandom()); p = g.State.Active; rival = g.State.Players[1]; g.PublicHistory = new DuelHistory();
            Check(g.ResolveDecision(0, 0).Success && g.State.PendingDecision is { Step: "hand", Kind: DecisionKind.ChooseCard });
            var hand = DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(hand.Select(o => o.CardId).SequenceEqual([CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]) && hand[0].Button == "Devolver al mazo");
            // Senshuni and both memory cards go to their decks, and the rival draws one card back.
            Check(hand[0].Move.Apply(g, 0).Success && rival.Hand.Count == 3 && rival.Deck.Count == 6 && p.Memory.SequenceEqual([CardCatalog.KonamyCode.Id, CardCatalog.MindReset.Id]));
            Check(!JsonSerializer.Serialize(g.PublicHistory.Entries).Contains(CardCatalog.Senshuni.Name));
            Check(g.State.Phase == Phase.Decision && g.State.PendingDecision is { Source: "konamy-code", Suspended: null } && DuelSnapshot.For(g, 0, 2).Players[1].Hand.All(c => c == null));
            var pendant = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.CardId == CardCatalog.Pendant.Id);
            Check(pendant.Move.Apply(g, 0).Success && p.Hand.Contains(CardCatalog.Pendant.Id) && g.State.Phase == Phase.Actions && g.State.PendingDecision == null);
        }),
        ("Decisión: Resetear la Mente guardado en su fase anterior se termina con sus jugadas de entonces", () =>
        {
            var g = Fixture(CardCatalog.MindReset.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Magnet.Id]); rival.Hand.Add(CardCatalog.Senshuni.Id);
            Check(g.PlayCode(0, 0).Success && g.State.PendingDecision is { Source: "mind-reset" });
            var json = JsonNode.Parse(g.SaveSnapshot())!; var state = json["State"]!.AsObject();
            state.Remove("PendingDecision"); state["Phase"] = (int)Phase.MindResetChoice;
            state["PendingMindReset"] = new JsonObject { ["Actor"] = 0, ["Target"] = -1, ["Memories"] = new JsonArray(), ["ResumePhase"] = (int)Phase.Actions };
            var legacy = GameEngine.FromSnapshot(json.ToJsonString(), new ScriptedRandom()); rival = legacy.State.Players[1];
            Check(DuelOptions.For(legacy, 0).Any(o => o.Move.Kind == DuelMoveKind.MindResetTarget));
            Check(legacy.ChooseMindResetTarget(0, 1).Success && DuelSnapshot.For(legacy, 0, 1).Players[1].Hand.SequenceEqual(rival.Hand));
            Check(legacy.ChooseMindResetMemory(0, 0, 0).Success && legacy.ChooseMindResetMemory(0, 0, 1).Success);
            Check(legacy.ResolveMindReset(0, 0).Success && legacy.State.PendingMindReset == null && legacy.State.Phase == Phase.Actions);
            Check(rival.Hand.Count == 1 && legacy.State.Active.Memory.SequenceEqual([CardCatalog.MindReset.Id]));
        }),
        ("Decisión: las cartas con efecto registrado no necesitan indicadores en su definición", () =>
        {
            foreach (var card in new[] { CardCatalog.BlueEgg, CardCatalog.TimeBomb, CardCatalog.KonamyCode, CardCatalog.PointsIncrease, CardCatalog.FloppyDisk, CardCatalog.TripleAttack, CardCatalog.GoldenTotem })
                Check(card with { Text = "" } == new CardDefinition(card.Id, card.Name, card.Kind, "", CodeType: card.CodeType));
            // A COMBO keeps its speed and action cost, which are common costs, not card rules.
            Check(CardCatalog.MindReset with { Text = "" } == new CardDefinition(CardCatalog.MindReset.Id, CardCatalog.MindReset.Name, CardKind.Code, "", CodeType: CodeType.Play, IsCombo: true));
            Check(CardCatalog.Dodge with { Text = "" } == new CardDefinition(CardCatalog.Dodge.Id, CardCatalog.Dodge.Name, CardKind.Code, "", CodeType: CodeType.Play, IsCombo: true));
            // Data the module reads, not a rule switch: how many cards Carga Rápida, Senshuni and Cartucho draw.
            // The first-turn ban is a common activation cost every code may carry.
            Check(CardCatalog.QuickLoad with { Text = "" } == new CardDefinition(CardCatalog.QuickLoad.Id, CardCatalog.QuickLoad.Name, CardKind.Code, "", Draw: 2, CodeType: CodeType.Play));
            Check(CardCatalog.Senshuni with { Text = "" } == new CardDefinition(CardCatalog.Senshuni.Id, CardCatalog.Senshuni.Name, CardKind.Code, "", Draw: 2, CodeType: CodeType.Character));
            Check(CardCatalog.MindSwap with { Text = "" } == new CardDefinition(CardCatalog.MindSwap.Id, CardCatalog.MindSwap.Name, CardKind.Code, "", CodeType: CodeType.Play, EarliestTurn: 2));
            Check(CardCatalog.ReloadCartridge with { Text = "" } == new CardDefinition(CardCatalog.ReloadCartridge.Id, CardCatalog.ReloadCartridge.Name, CardKind.Code, "", Draw: 4, CodeType: CodeType.Play, EarliestTurn: 2));
        }),
    };

    static GameEngine Fixture(string code)
    {
        var g = new GameEngine(new ScriptedRandom()); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 6)); p.Bonus = null; p.Equipment = null; }
        g.State.Active.Hand.Add(code);
        return g;
    }

    static void Check(bool value, string? detail = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { if (!value) throw new Exception($"Decisión genérica no satisfecha (línea {line}){(detail == null ? "" : ": " + detail)}"); }
}
