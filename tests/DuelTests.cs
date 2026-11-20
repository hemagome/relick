using Relick.Core;
using System.Text.Json;

static class DuelTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("PvP: proyección oculta mazos, manos rivales, corruptas y registros", Privacy),
        ("PvP: reconstrucción conserva decisiones y continuaciones durante duelos completos", ReplayMatches),
        ("PvP: rechazo de un registro de azar incompleto", InvalidRandom),
        ("PvP: jugadas y fases viajan por nombre y rechazan valores numéricos", MovesTravelByName),
        ("PvP: señales visuales conservan tiradas repetidas sin revelar cartas ni mutar reglas", VisualPrivacy),
        ("Impactos: distinguen 4, 5 y 6 para ambos jugadores sin confundir daño ni dado secundario", VisualAttackImpact),
        ("Impactos: usan el resultado final tras relanzar y convertir el seis de Yuno", VisualFinalAttackDie),
        ("Impactos: conservan el resultado durante una defensa pendiente y su recuperación", VisualPendingImpact),
        ("Impactos: el daño sin ataque no hereda un crítico anterior", VisualNonAttackDamage)
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
                var saved = GameEngine.FromSnapshot(game.SaveSnapshot(), resumed.Random);
                if (saved.SaveSnapshot() != game.SaveSnapshot() ||
                    JsonSerializer.Serialize(DuelOptions.For(saved, saved.State.DecisionPlayer)) != JsonSerializer.Serialize(DuelOptions.For(game, game.State.DecisionPlayer)))
                    throw new Exception($"Recuperación diferente en {game.State.Phase}, seed {seed}.");
                var stored = entries.Select(e => JsonSerializer.Deserialize(JsonSerializer.Serialize(e, DuelJson.Default.DuelEntry), DuelJson.Default.DuelEntry)!).ToArray();
                resumed = DuelReplay.Restore(opening, stored);
                if (JsonSerializer.Serialize(game.State) != JsonSerializer.Serialize(resumed.Game.State))
                    throw new Exception($"Reconstrucción diferente en {game.State.Phase}, seed {seed}.");
                var count = game.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count +
                    p.Skills.Count + p.AttachedCards.Count + p.Exchange.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1));
                if (count != 2 * CardCatalog.TrainingDeck().Count) throw new Exception("Se perdió una carta al ejecutar el duelo.");
            }
            if (!resumed.Game.State.IsOver) throw new Exception("El duelo reconstruido no termina.");
        }
        foreach (var phase in new[] { Phase.AttackRerollChoice, Phase.DieRerollChoice, Phase.ErrorResponse, Phase.Decision, Phase.RecycleChoice })
            if (!phases.Contains(phase)) throw new Exception("No se ejercitó una continuación de " + phase);
    }

    static void InvalidRandom()
    {
        var random = new DuelRandom(); random.Replay([]);
        try { random.Next(6); }
        catch (InvalidOperationException) { return; }
        throw new Exception("Se generó azar nuevo al reconstruir una partida.");
    }

    static void MovesTravelByName()
    {
        var options = new List<DuelOption> { new() { Label = "Abandonar duelo", Move = new() { Kind = DuelMoveKind.Concede } } };
        var json = JsonSerializer.Serialize(options, DuelJson.Default.ListDuelOption);
        if (!json.Contains("\"Concede\"")) throw new Exception("La jugada no se serializó por nombre: " + json);
        if (JsonSerializer.Deserialize(json, DuelJson.Default.ListDuelOption)![0].Move.Kind != DuelMoveKind.Concede) throw new Exception("La jugada no se recuperó.");
        var snapshot = JsonSerializer.Serialize(new DuelSnapshot { Phase = Phase.Decision }, DuelJson.Default.DuelSnapshot);
        if (!snapshot.Contains("\"Decision\"")) throw new Exception("La fase no se serializó por nombre.");
        // A peer from another version sending a bare number must fail to decode, never map to a different move.
        try { JsonSerializer.Deserialize(json.Replace("\"Concede\"", "24"), DuelJson.Default.ListDuelOption); }
        catch (JsonException) { return; }
        throw new Exception("Se aceptó una jugada numérica.");
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
        if (!game.ResolveDieReroll(0, false).Success) throw new Exception("No se conservó el dado sin gastar destreza.");
        if (events.Count(v => v.Kind == DuelVisualKind.Die && v.Value == 2) != 1) throw new Exception("Falta el dado visual.");
        var json = JsonSerializer.Serialize(events);
        if (json.Contains("secret-") || json.Contains("extra-life")) throw new Exception("Una señal visual revela cartas.");
        // Repeated values are separate cues, not inferred from LastDie changing.
        game.State.Players[0].Hand.Add("extra-life"); random.Replay([1]);
        if (!game.PlayCode(0, 0).Success || events.Count(v => v.Kind == DuelVisualKind.Die && v.Value == 2) != 2)
            throw new Exception("Se pierde una tirada que repite el mismo valor.");
        if (!game.ResolveDieReroll(0, false).Success) throw new Exception("No se resolvió la curación repetida.");
        var view = DuelSnapshot.For(game, 0, 2); view.Visuals = events;
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!;
        if (restored.Visuals.Count != events.Count || restored.LastCombat != view.LastCombat) throw new Exception("Las señales no sobreviven al transporte.");
        if (restored.Visuals.Count(v => v.Kind == DuelVisualKind.Heal && v.Player == 0 && v.Value == 2) != 2)
            throw new Exception("Se pierden las curaciones repetidas al transportar las señales.");
    }

    static (GameEngine Game, ScriptedRandom Random) ImpactFixture(int actor = 0, string skill = "destiny-shuffle")
    {
        var random = new ScriptedRandom(); var game = TestGame.Create(random);
        game.State.Phase = Phase.Combat; game.State.CurrentPlayer = actor; game.State.Turn = 2;
        foreach (var player in game.State.Players) { player.Hand.Clear(); player.Skills.Clear(); player.Bonus = null; }
        game.State.Active.Hand.Add(CardCatalog.Attack.Id); game.State.Active.Skills.Add(skill);
        return (game, random);
    }
    static void Require(bool value) { if (!value) throw new Exception("Señal de impacto incorrecta."); }

    static void VisualAttackImpact()
    {
        foreach (var actor in new[] { 0, 1 }) foreach (var die in new[] { 4, 5, 6 })
        foreach (var skill in new[] { CardCatalog.Destiny, CardCatalog.Magnet })
        {
            var (game, random) = ImpactFixture(actor, skill.Id); var cues = DuelVisualEvents.Capture(game);
            random.EnqueueDie(die);
            if (skill == CardCatalog.Magnet && die != 6) random.EnqueueDie(6);
            Require(game.Attack(actor, 0, 0).Success && game.ResolveAttackReroll(actor, false).Success);
            var hit = cues.Single(c => c.Kind == DuelVisualKind.Hit);
            Require(hit.Player == 1 - actor && hit.AttackDie == die &&
                hit.Value == GameEngine.DamageFor(die, skill == CardCatalog.Magnet ? 6 : skill.NormalDamage, skill.CriticalDamage));
            if (skill == CardCatalog.Magnet && die != 6) Require(game.State.LastDie == 6);
            var snapshot = DuelSnapshot.For(game, actor, 1); snapshot.Visuals = cues;
            var copy = JsonSerializer.Deserialize(JsonSerializer.Serialize(snapshot, DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!;
            Require(copy.Visuals.Single(c => c.Kind == DuelVisualKind.Hit).AttackDie == die);
        }
    }

    static void VisualFinalAttackDie()
    {
        foreach (var level in new[] { 1, 2 })
        {
            var (game, random) = ImpactFixture();
            if (level == 2) game.State.Active.Relikario = RelikarioCatalog.YunoLv2;
            var cues = DuelVisualEvents.Capture(game); random.EnqueueDie(4); random.EnqueueDie(6);
            Require(game.Attack(0, 0, 0).Success && cues.All(c => c.Kind != DuelVisualKind.Hit));
            Require(game.ResolveAttackReroll(0, true).Success);
            if (game.State.Phase == Phase.AttackRerollChoice) Require(game.ResolveAttackReroll(0, false).Success);
            Require(cues.Single(c => c.Kind == DuelVisualKind.Hit).AttackDie == (level == 1 ? 5 : 6));
        }
    }

    static void VisualPendingImpact()
    {
        var (game, random) = ImpactFixture(); var defender = game.State.Players[1];
        defender.Skills.Add(CardCatalog.Destiny.Id); defender.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 4));
        random.EnqueueDie(5); Require(game.Attack(0, 0, 0).Success && game.ResolveAttackReroll(0, false).Success);
        Require(game.State.Phase == Phase.DefenseChoice && game.State.LastAttackDie == 5);
        var restored = GameEngine.FromSnapshot(game.SaveSnapshot(), new ScriptedRandom());
        var cues = DuelVisualEvents.Capture(restored);
        Require(restored.PassDefense(1).Success && cues.Single(c => c.Kind == DuelVisualKind.Hit).AttackDie == 5);
    }

    static void VisualNonAttackDamage()
    {
        var (game, _) = ImpactFixture(); var player = game.State.Active;
        game.State.Phase = Phase.Actions; game.State.ActionsLeft = 2; game.State.LastAttackDie = 6;
        player.Hand.Clear(); player.Hand.Add(CardCatalog.Senshuni.Id); player.Deck.Clear(); player.Recycled = true;
        var cues = DuelVisualEvents.Capture(game); Require(game.PlayCode(0, 0).Success);
        var hit = cues.Single(c => c.Kind == DuelVisualKind.Hit);
        Require(hit.Player == 0 && hit.Value == 2 && hit.AttackDie == 0);
    }
}
