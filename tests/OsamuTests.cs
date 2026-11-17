using Relick.Core;

static class OsamuTests
{
    public static (string, Action)[] Cases =>
    [
        ("Osamu: mazo compatible, 10 vidas y 0 destreza desde la apertura", () =>
        {
            var deck = CardCatalog.TrainingDeck();
            deck = deck.Where(id => CardCatalog.Get(id).Relikario != "yuno")
                .Concat([CardCatalog.TurboSword.Id, CardCatalog.TurboSword.Id, CardCatalog.TurboOverlap.Id]).ToList();
            Check(DeckRules.Validate("osamu", deck, []).Count == 0 && DeckRules.Validate("yuno", deck, []).Count > 0);
            var game = Open(new ScriptedRandom(), RelikarioCatalog.Osamu);
            var p = game.State.Active;
            Check(p.Relikario == RelikarioCatalog.Osamu && p.Health == 10 && p.Dexterity == 0);
            Check(DuelSnapshot.For(game, 1, 0).Players[0].RelikarioId == "osamu");
            p.Hand.Add(CardCatalog.Magnet.Id);
            Check(!game.Learn(0, 0).Success);
        }),
        ("Osamu Lv 1: éxito ofrece solo las dos cartas del mazo antes del daño, y puede pasar", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.Osamu);
            var player = game.State.Active;
            player.Skills.Add(CardCatalog.TurboOverlap.Id);
            player.Hand.Add(CardCatalog.Attack.Id);
            player.Deck.AddRange([CardCatalog.TimeBomb.Id, CardCatalog.Agility.Id, CardCatalog.Senshuni.Id]);
            random.EnqueueDie(5);
            Check(game.Attack(0, 0, 0).Success && game.State.Phase == Phase.Decision && game.State.OtherHealth() == 8);
            var options = DuelOptions.For(game, 0);
            Check(options.Where(o => o.Move.Kind == DuelMoveKind.Choose).Select(o => o.CardId)
                .SequenceEqual([CardCatalog.TimeBomb.Id, CardCatalog.Agility.Id, null]));
            Check(DuelSnapshot.For(game, 1, 0).Options.All(o => o.CardId == null));
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(options.Single(o => o.Move.Kind == DuelMoveKind.Choose && o.CardId == null).Move.Apply(game, 0).Success);
            Check(game.State.OtherHealth() == 5 && game.State.Active.Relikia == 300 && game.State.Active.Hand.Count == 0);
        }),
        ("Osamu Lv 1: un fallo con 2 recupera Agilidad y un 1 no ofrece búsqueda", () =>
        {
            foreach (var die in new[] { 1, 2 })
            {
                var random = new ScriptedRandom();
                var game = Open(random, RelikarioCatalog.Osamu);
                var p = game.State.Active;
                p.Hand.Add(CardCatalog.Attack.Id);
                p.Skills.Add(CardCatalog.TurboSword.Id);
                p.Memory.AddRange([CardCatalog.Agility.Id, CardCatalog.TimeBomb.Id]);
                random.EnqueueDie(die);
                Check(game.Attack(0, 0, 0).Success);
                if (die == 2)
                {
                    var choice = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose);
                    Check(choice.CardId == CardCatalog.Agility.Id && choice.Owner == 0 && choice.Move.Apply(game, 0).Success);
                }
                Check(p.Hand.Contains(CardCatalog.Agility.Id) == (die == 2) && game.State.Phase == Phase.Combat);
            }
        }),
        ("Osamu Lv 2: evoluciona a cuatro vidas por 1500 PR, toma Agilidad y sube incluso el 1 de ataque", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.Osamu);
            var p = game.State.Active;
            p.Relikia = 1500;
            p.Health = 5;
            p.Memory.Add(CardCatalog.Agility.Id);
            Check(!game.Evolve(0).Success && p.Relikia == 1500);
            p.Health = 4;
            Check(game.Evolve(0).Success && p.Relikario == RelikarioCatalog.OsamuLv2 && p.Relikia == 0 && p.Health == 4);
            Check(game.ResolveDecision(0, 0).Success && p.Hand.Single() == CardCatalog.Agility.Id);
            Check(!p.CorruptsOnMemory(CardZone.Hand, 0) && !game.CanUseOsamuSearch(0));
            p.Skills.Add(CardCatalog.TurboSword.Id); p.Hand.Add(CardCatalog.Attack.Id);
            random.EnqueueDie(1);
            Check(game.Attack(0, 1, 0).Success && game.State.LastAttackDie == 2);
        }),
        ("Osamu Lv 2: paga una acción y corrompe solo la copia buscada incluso después de guardar", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.OsamuLv2);
            game.State.Phase = Phase.Actions;
            var p = game.State.Active;
            p.Hand.Add(CardCatalog.Senshuni.Id);
            p.Memory.Add(CardCatalog.Senshuni.Id);
            p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 6));
            Check(DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.RelikarioSearch).Move.Apply(game, 0).Success);
            Check(!game.ResolveDecision(1, 0).Success && !game.ResolveDecision(0, 99).Success);
            var option = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Owner == 0);
            Check(option.Move.Apply(game, 0).Success && p.ActionsLeft == 1 && !game.CanUseOsamuSearch(0));
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            p = game.State.Active;
            Check(game.PlayCode(0, 1).Success && p.Hand.Count == 3 && p.Memory.Count == 0);
            Check(p.CorruptedMemory.SequenceEqual([CardCatalog.Senshuni.Id]) && !p.CorruptsOnMemory(CardZone.Hand, 0));
        }),
        ("Osamu Lv 2: el equipo buscado conserva su marca al equiparse y se corrompe al destruirse", () =>
        {
            var game = Open(new ScriptedRandom(), RelikarioCatalog.OsamuLv2);
            game.State.Phase = Phase.Actions;
            var p = game.State.Active;
            p.Deck.AddRange([CardCatalog.Pendant.Id, CardCatalog.Attack.Id]);
            Check(game.UseOsamuSearch(0).Success && game.ResolveDecision(0, 0).Success);
            Check(game.Equip(0, 0).Success && p.Equipment == CardCatalog.Pendant.Id && p.Memory.Count == 0);
            Check(p.CorruptsOnMemory(CardZone.Equipment, 0));
            game.State.CurrentPlayer = 1;
            game.State.Players[1].Hand.Add(CardCatalog.BlueEgg.Id);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Equipment).Success);
            Check(p.Equipment == null && p.CorruptedMemory.SequenceEqual([CardCatalog.Pendant.Id]) && p.Memory.Count == 0);
        }),
        ("Turbo Sword: daño variable respetando mínimos y 500 PR", () =>
        {
            foreach (var (attack, damageDie, damage) in new[] { (4, 6, 3), (5, 1, 2), (6, 1, 4), (6, 6, 7) })
            {
                var random = new ScriptedRandom();
                var game = Open(random, RelikarioCatalog.OsamuLv2);
                game.State.Active.Hand.Add(CardCatalog.Attack.Id);
                game.State.Active.Skills.Add(CardCatalog.TurboSword.Id);
                random.EnqueueDie(attack - 1); random.EnqueueDie(damageDie);
                Check(game.Attack(0, 0, 0).Success && game.State.LastAttackDie == attack);
                Check(game.State.OtherHealth() == 8 - damage && game.State.Active.Relikia == 500);
            }
        }),
        ("Turbo Sword: el 5 suma contador a esa copia y el 6 refleja la mitad sin sumar contador", () =>
        {
            foreach (var die in new[] { 4, 5, 6 })
            {
                var random = new ScriptedRandom();
                var game = Open(random, RelikarioCatalog.Osamu);
                var defender = game.State.Players[1];
                defender.Skills.AddRange([CardCatalog.TurboSword.Id, CardCatalog.TurboSword.Id]);
                defender.SkillCounters[0] = 2;
                game.State.Active.Hand.Add(CardCatalog.Attack.Id);
                game.State.Active.Skills.Add(CardCatalog.TurboSword.Id);
                game.State.Active.SkillCounters[0] = 2;
                random.EnqueueDie(6); random.EnqueueDie(5); random.EnqueueDie(die);
                Check(game.Attack(0, 0, 0).Success);
                Check(game.State.OtherHealth() == (die == 4 ? 2 : 8) && game.State.Active.Health == (die == 6 ? 7 : 10));
                Check(defender.CountersOnSkill(0) == 2 && defender.CountersOnSkill(1) == (die == 5 ? 1 : 0));
                Check(game.State.Active.Relikia == 500 && game.State.LastAttackDie == 6);
            }
        }),
        ("Turbo Sword: su dado de defensa se puede modificar y reanudar desde un estado guardado", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.Osamu);
            game.State.Active.Hand.Add(CardCatalog.Attack.Id);
            game.State.Active.Skills.Add(CardCatalog.TurboOverlap.Id);
            var defender = game.State.Players[1];
            defender.Skills.Add(CardCatalog.TurboSword.Id);
            defender.Hand.Add(CardCatalog.Agility.Id);
            random.EnqueueDie(5); random.EnqueueDie(3);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingDieRoll is { Player: 1, Purpose: DiePurpose.TurboSwordDefense });
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.PlayCode(1, 0).Success);
            if (game.State.Phase == Phase.DieRerollChoice) Check(game.ResolveDieReroll(1, false).Success);
            Check(game.State.OtherHealth() == 8 && game.State.Players[1].CountersOnSkill(0) == 1 &&
                game.State.Active.Relikia == 300 && game.State.LastAttackDie == 5);
        }),
        ("Turbo Overlap: busca al enseñar y permite escoger cualquiera de sus dos dados al hacer crítico", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.OsamuLv2);
            var p = game.State.Active;
            p.Memory.Add(CardCatalog.Agility.Id);
            p.Hand.AddRange([CardCatalog.TurboOverlap.Id, CardCatalog.Attack.Id]);
            p.Deck.AddRange([CardCatalog.Agility.Id, CardCatalog.TimeBomb.Id]);
            Check(game.Learn(0, 0).Success);
            var fromMemory = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Owner == 0);
            Check(fromMemory.Move.Apply(game, 0).Success && p.Hand.Contains(CardCatalog.Agility.Id));
            p.Hand.Remove(CardCatalog.Agility.Id);
            random.EnqueueDie(6); random.EnqueueDie(2); random.EnqueueDie(5);
            var cues = DuelVisualEvents.Capture(game);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingDecision?.Kind == DecisionKind.Confirm);
            var dice = cues.Where(cue => cue.Kind == DuelVisualKind.Die).ToArray();
            Check(dice is [{ Value: 6, SecondDie: 0 }, { Value: 2, SecondDie: 5 }]);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.DecisionChoices(0).Select(o => o.Value).SequenceEqual([2, 5]) && game.ResolveDecision(0, 0).Success);
            Check(game.State.OtherHealth() == 6 && game.State.Phase == Phase.Decision);
            Check(game.ResolveDecision(0, 0).Success && game.State.Active.Hand.Single() == CardCatalog.Agility.Id &&
                game.State.Active.Relikia == 300 && !game.State.Active.CorruptsOnMemory(CardZone.Hand, 0));
        }),
        ("Turbo Overlap: ambos dados salen juntos, conservan sus modificadores y se pueden elegir aunque empaten", () =>
        {
            var random = new ScriptedRandom();
            var game = Open(random, RelikarioCatalog.OsamuLv2);
            game.State.Active.Skills.Add(CardCatalog.TurboOverlap.Id);
            game.State.Active.Hand.Add(CardCatalog.Attack.Id);
            game.State.Active.Dexterity = 1;
            random.EnqueueDie(6); random.EnqueueDie(3); random.EnqueueDie(2);
            var cues = DuelVisualEvents.Capture(game);
            Check(game.Attack(0, 0, 0).Success && game.State.PendingDieRoll?.Die == 3);
            Check(cues.Last() is { Kind: DuelVisualKind.Die, Value: 3, SecondDie: 2 });
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            cues = DuelVisualEvents.Capture(game);
            Check(game.ResolveDieReroll(0, false).Success && game.State.PendingDieRoll?.Die == 2 && cues.Count == 0);
            Check(game.UseDexterity(0).Success && cues.Single() is { Kind: DuelVisualKind.DieAdjustment, Value: 3 });
            Check(game.DecisionChoices(1).Count == 0 && game.DecisionChoices(0).Select(choice => choice.Value).SequenceEqual([3, 3]));
            Check(game.ResolveDecision(0, 1).Success && game.State.OtherHealth() == 5);
        })
    ];

    private static GameEngine Open(ScriptedRandom random, RelikarioDefinition form)
    {
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Osamu", "Rival"], Hands = [[], []], Decks = [[], []], Bonuses = [null, null], Relikarios = [form.Id, "yuno"]
        }, random);
        game.State.Turn = 2;
        game.State.Players[1].Dexterity = 0;
        return game;
    }

    private static int OtherHealth(this GameState state) => state.Players[1].Health;

    private static void Check(bool passed, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!passed) throw new Exception($"Regla de Osamu no satisfecha en la línea {line}.");
    }
}
