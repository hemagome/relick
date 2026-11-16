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
            rival.Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && g.State.PendingDecision is { Step: "target", Kind: DecisionKind.ChooseTarget, AllowsCombos: true });
            var star = p.Hand.IndexOf(CardCatalog.PlumberStar.Id);
            Check(g.CodeUnavailableReason(0, star) == null);
            var target = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose);
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
            p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id]); p.ActionsLeft = 2; rival.Bonus = CardCatalog.Senshuni.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision);
            var pending = g.State.PendingDecision!;
            Check(g.PlayCode(0, p.Hand.IndexOf(CardCatalog.PlumberStar.Id)).Success && g.State.PendingDecision == null && g.State.Phase == Phase.TurnBuffDiscard);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), new ScriptedRandom()); p = g.State.Active; rival = g.State.Players[1];
            Check(g.ResolveTurnBuffDiscard(0, 0).Success && p.StarEffects == 1);
            Check(g.State.Phase == Phase.Decision && g.State.PendingDecision is { Source: "blue-egg", Step: "target" } restored && restored.ResumePhase == pending.ResumePhase);
            Check(g.ResolveDecision(0, 0).Success && rival.Memory.Contains(CardCatalog.Senshuni.Id) && g.State.Phase == Phase.Actions);
        }),
        ("Decisión: un duelo guardado con la fase anterior de Huevo se termina con sus jugadas de entonces", () =>
        {
            var g = Fixture(CardCatalog.BlueEgg.Id); var p = g.State.Active; var rival = g.State.Players[1];
            p.Relikia = 200; rival.Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success);
            // Rewrite the save as the previous version wrote it: the destruction phase and its actor, no decision.
            var json = JsonNode.Parse(g.SaveSnapshot())!; var state = json["State"]!.AsObject();
            state["Phase"] = (int)Phase.DestroyChoice; state["DestroyActor"] = 0; state.Remove("PendingDecision");
            var legacy = GameEngine.FromSnapshot(json.ToJsonString(), new ScriptedRandom()); p = legacy.State.Active; rival = legacy.State.Players[1];
            var destroy = DuelOptions.For(legacy, 0).Single(o => o.Move.Kind == DuelMoveKind.Destroy);
            Check(destroy.Move.Kind == DuelMoveKind.Destroy && destroy.Move.Apply(legacy, 0).Success && rival.Equipment == null);
            Check(legacy.State.Phase == Phase.BlueEggDrawChoice && DuelOptions.For(legacy, 0).Count(o => o.Move.Kind == DuelMoveKind.BlueEggDraw) == 2);
            Check(legacy.ResolveBlueEggDraw(0, false).Success && legacy.State.Phase == Phase.Actions && p.Relikia == 200);
        }),
        ("Decisión: las cartas con efecto registrado no necesitan indicadores en su definición", () =>
        {
            foreach (var card in new[] { CardCatalog.BlueEgg, CardCatalog.TimeBomb, CardCatalog.KonamyCode })
                Check(card with { Text = "" } == new CardDefinition(card.Id, card.Name, card.Kind, "", CodeType: card.CodeType));
        }),
    };

    static GameEngine Fixture(string code)
    {
        var g = new GameEngine(new ScriptedRandom()); g.State.Phase = Phase.Actions; g.State.CurrentPlayer = 0; g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Hand.Clear(); p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 6)); p.Bonus = null; p.Equipment = null; }
        g.State.Active.Hand.Add(code);
        return g;
    }

    static void Check(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { if (!value) throw new Exception($"Decisión genérica no satisfecha (línea {line})"); }
}
