using Relick.Core;
using System.Text.Json;

static class DuelTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("PvP: proyección oculta mazos, manos rivales, corruptas y registros", Privacy),
        ("PvP: reconstrucción conserva decisiones y continuaciones durante duelos completos", ReplayMatches),
        ("PvP: rechazo de un registro de azar incompleto", InvalidRandom),
        ("PvP: señales visuales conservan tiradas repetidas sin revelar cartas ni mutar reglas", VisualPrivacy)
    };

    static DuelOpening Opening(int seed)
    {
        var game = TestGame.Create(seed); game.Start([]);
        return new DuelOpening
        {
            Names = game.State.Players.Select(p => p.Name).ToArray(), StartingSeat = game.State.CurrentPlayer,
            Hands = game.State.Players.Select(p => p.Hand.ToList()).ToArray(),
            Decks = game.State.Players.Select(p => p.Deck.ToList()).ToArray(),
            Bonuses = game.State.Players.Select(p => p.Bonus).ToArray()
        };
    }

    static void Privacy()
    {
        var opening = Opening(5);
        opening.Decks[0] = ["secret-deck-one"]; opening.Decks[1] = ["secret-deck-two"];
        opening.Hands[1] = ["secret-opponent-hand"];
        var game = GameEngine.FromOpening(opening, new SeededRandom(1));
        game.State.Players[1].CorruptedMemory.Add("secret-corrupted"); game.State.Log.Add("secret-log");
        var view = DuelSnapshot.For(game, 0, 4);
        var json = JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot);
        if (json.Contains("secret-") || view.Players[1].Hand.Any(id => id != null) || view.Players[1].CorruptedCount != 1)
            throw new Exception("Una proyección contiene información oculta.");
        game.State.Players[1].Hand[0] = CardCatalog.Attack.Id;
        game.State.Phase = Phase.MagnetChoice; game.State.CurrentPlayer = 0;
        if (DuelSnapshot.For(game, 0, 5).Players[1].Hand[0] != CardCatalog.Attack.Id) throw new Exception("Magnet no permite inspeccionar la mano.");
        game.State.Phase = Phase.Combat;
        if (DuelSnapshot.For(game, 0, 6).Players[1].Hand[0] != null) throw new Exception("La inspección de Magnet permanece activa.");
    }

    static void ReplayMatches()
    {
        var phases = new HashSet<Phase>();
        for (var seed = 0; seed < 24; seed++)
        {
            var opening = Opening(seed);
            var resumed = DuelReplay.Restore(opening, []);
            var live = new SeededRandom(seed + 100);
            var entries = new List<DuelEntry>();
            var step = 0;
            while (!resumed.Game.State.IsOver && step++ < 1000)
            {
                var game = resumed.Game; phases.Add(game.State.Phase);
                var actor = game.State.DecisionPlayer;
                var choices = DuelOptions.For(game, actor).Where(o => o.Move.Kind != DuelMoveKind.Concede).ToList();
                if (choices.Count == 0) throw new Exception($"Sin decisión en {game.State.Phase}, seed {seed}.");
                var choice = choices[0];
                choice.Move.Discards = Enumerable.Range(0, choice.RequiredDiscards).ToArray();
                resumed.Random.Record(live.Next);
                var result = choice.Move.Apply(game, actor);
                if (!result.Success) throw new Exception($"{choice.Label}: {result.Message}");
                entries.Add(new DuelEntry { Actor = actor, Move = choice.Move, RandomValues = resumed.Random.Finish() });
                var stored = entries.Select(e => JsonSerializer.Deserialize(JsonSerializer.Serialize(e, DuelJson.Default.DuelEntry), DuelJson.Default.DuelEntry)!).ToArray();
                resumed = DuelReplay.Restore(opening, stored);
                if (JsonSerializer.Serialize(game.State) != JsonSerializer.Serialize(resumed.Game.State))
                    throw new Exception($"Reconstrucción diferente en {game.State.Phase}, seed {seed}.");
                var count = game.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count +
                    p.Skills.Count + p.AttachedCards.Count + p.Exchange.Count + (p.Equipment == null ? 0 : 1) + (p.Bonus == null ? 0 : 1));
                if (count != 40) throw new Exception("Se perdió una carta al ejecutar el duelo.");
            }
            if (!resumed.Game.State.IsOver) throw new Exception("El duelo reconstruido no termina.");
        }
        foreach (var phase in new[] { Phase.AttackRerollChoice, Phase.DieRerollChoice, Phase.ErrorResponse, Phase.PointsChoice, Phase.DestroyChoice })
            if (!phases.Contains(phase)) throw new Exception("No se ejercitó una continuación de " + phase);
    }

    static void InvalidRandom()
    {
        var random = new DuelRandom(); random.Replay([]);
        try { random.Next(6); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Se generó azar nuevo al reconstruir una partida.");
    }

    static void VisualPrivacy()
    {
        var opening = Opening(5);
        opening.StartingSeat = 0;
        opening.Hands[0] = ["extra-life"];
        opening.Hands[1] = [CardCatalog.Magnet.Id];
        opening.Decks[0] = ["secret-deck"];
        var random = new DuelRandom(); random.Replay([1]);
        var game = GameEngine.FromOpening(opening, random);
        game.State.Phase = Phase.Actions;
        game.State.Players[0].Health = 4;
        var events = DuelVisualEvents.Capture(game);
        var before = JsonSerializer.Serialize(game.State);
        if (events.Count != 0 || before != JsonSerializer.Serialize(game.State)) throw new Exception("Capturar señales cambia la partida.");
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se ejecutó Vida Extra.");
        if (events.Count(v => v.Kind == DuelVisualKind.Die && v.Value == 2) != 1) throw new Exception("Falta el dado visual.");
        var json = JsonSerializer.Serialize(events);
        if (json.Contains("secret-") || json.Contains("extra-life")) throw new Exception("Una señal visual revela cartas.");
        // Repeated values are separate cues, not inferred from LastDie changing.
        game.State.Players[0].Hand.Add("extra-life"); random.Replay([1]);
        if (!game.PlayCode(0, 0).Success || events.Count(v => v.Kind == DuelVisualKind.Die && v.Value == 2) != 2)
            throw new Exception("Se pierde una tirada que repite el mismo valor.");
        var view = DuelSnapshot.For(game, 0, 2); view.Visuals = events;
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!;
        if (restored.Visuals.Count != events.Count || restored.LastCombat != view.LastCombat) throw new Exception("Las señales no sobreviven al transporte.");
        if (restored.Visuals.Count(v => v.Kind == DuelVisualKind.Heal && v.Player == 0 && v.Value == 2) != 2)
            throw new Exception("Se pierden las curaciones repetidas al transportar las señales.");
    }
}
