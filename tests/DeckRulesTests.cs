using Relick.Core;
static class DeckRulesTests
{
    private const string Yuno = "yuno";

    public static (string, Action)[] Cases => new (string, Action)[]
    {
        ("Mazo: el de entrenamiento y sus Trampas cumplen las reglas oficiales", () =>
        {
            Legal(CardCatalog.TrainingDeck(), CardCatalog.TrainingTraps());
            Legal(CardCatalog.TrainingDeck(), []);
        }),
        ("Mazo: exactamente 42 cartas y exactamente 12 Ataques", () =>
        {
            var deck = CardCatalog.TrainingDeck();
            Broken(deck.Append("senshuni").ToList(), [], "exactamente 42");
            Broken(deck.Skip(1).ToList(), [], "exactamente 42", "exactamente 12");
            // Swapping an Attack for a Code keeps 42 cards but leaves 11 Attacks.
            Broken(Swap(deck, "attack", "senshuni"), [], "exactamente 12");
            Broken(Swap(deck, "pc-8bits", "attack"), [], "exactamente 12");
        }),
        ("Mazo: Triple Ataque Cruzado es un Código y no cuenta como Ataque", () =>
        {
            Check(!DeckRules.IsAttack(CardCatalog.TripleAttack) && DeckRules.IsAttack(CardCatalog.Attack));
            Check(!DeckRules.IsAttack(CardCatalog.SuperAttack) && DeckRules.IsTrap(CardCatalog.SuperAttack));
            Check(CardCatalog.TrainingDeck().Select(CardCatalog.Get).Count(DeckRules.IsAttack) == 12);
        }),
        ("Mazo: los artes de Ataque comparten el cupo y conservan su copia al jugar", () =>
        {
            var yuno = CardCatalog.AttackYuno.Id;
            var yunoDt = CardCatalog.AttackYunoDt.Id;
            var classic = CardCatalog.Attack.Id;
            var deck = CardCatalog.TrainingDeck().Skip(DeckRules.AttackCount).ToArray();
            foreach (var (variant, quantity) in new[] { (yuno, 6), (yunoDt, 10) })
                Legal(deck.Concat(Enumerable.Repeat(variant, quantity)).Concat(Enumerable.Repeat(classic, 12 - quantity)).ToList(), []);
            var mixed = deck.Concat(Enumerable.Repeat(classic, 4)).Concat(Enumerable.Repeat(yuno, 4)).Concat(Enumerable.Repeat(yunoDt, 4)).ToList();
            Legal(mixed, []);
            Broken(Swap(mixed, CardCatalog.Pc.Id, yuno), [], "exactamente 12");
            Broken(mixed, [], [yuno, yunoDt], "no admite cartas de Ataque");

            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Yuno", "Osamu"], StartingSeat = 0, Hands = [[yuno, yunoDt], []], Decks = [mixed, []], Bonuses = [null, null]
            }, new SeededRandom(3));
            game.State.Turn = 2;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            var restored = GameEngine.FromSnapshot(game.SaveSnapshot(), new SeededRandom(3));
            Check(DuelSnapshot.For(restored, 0, 1).Players[0].Hand.SequenceEqual([yuno, yunoDt]));
            Check(DuelSnapshot.For(restored, 1, 1).Players[0].Hand.All(id => id == null));
            var attack = DuelOptions.For(restored, 0).Single(option => option.Move.Kind == DuelMoveKind.Attack && option.Move.Index == 1);
            Check(attack.Move.Apply(restored, 0).Success);
            Check(restored.State.Players[0].Hand.SequenceEqual([yuno]) && restored.State.Players[0].Memory.Contains(yunoDt));
        }),
        ("Mazo: las variantes pertenecen a su Relikario y los mazos antiguos conservan sus 12 Ataques", () =>
        {
            foreach (var (relikario, initial, ownArt, foreignArt) in new[]
            {
                (RelikarioCatalog.Yuno.Id, CardCatalog.Attack, CardCatalog.AttackYuno, CardCatalog.AttackIronwingDt),
                (RelikarioCatalog.Osamu.Id, CardCatalog.Attack, CardCatalog.AttackOsamuDt, CardCatalog.AttackYunoDt),
                (RelikarioCatalog.Ironwing.Id, CardCatalog.AttackIronwingUt, CardCatalog.AttackIronwingNs, CardCatalog.AttackOsamu)
            })
            {
                var rest = CardCatalog.All.Values.Where(card => !DeckRules.IsAttack(card) && !DeckRules.IsTrap(card) && DeckRules.Compatible(card, relikario))
                    .OrderBy(card => card.Kind == CardKind.Skill ? 0 : 1).Take(15).SelectMany(card => new[] { card.Id, card.Id });
                var legacy = Enumerable.Repeat(CardCatalog.Attack.Id, 10).Concat([ownArt.Id, foreignArt.Id]).Concat(rest).ToList();
                Check(DeckRules.Validate(relikario, legacy, []).Single().Contains("No son compatibles"));
                var repaired = DeckRules.NormalizeAttackArt(relikario, legacy);
                Check(repaired.Take(12).SequenceEqual(Enumerable.Repeat(initial.Id, 10).Concat([ownArt.Id, initial.Id])));
                Check(repaired.Skip(12).SequenceEqual(legacy.Skip(12)) && DeckRules.Validate(relikario, repaired, []).Count == 0);
                Check(DeckRules.NormalizeAttackArt(relikario, repaired).SequenceEqual(repaired));
            }
        }),
        ("Mazo: mínimo tres habilidades compatibles", () =>
        {
            Broken(Swap(CardCatalog.TrainingDeck(), "reverse-shuffle", "senshuni"), [], "al menos 3 habilidades");
            Check(new[] { CardCatalog.Magnet, CardCatalog.Destiny, CardCatalog.Reverse }.All(skill => skill.Relikario == Yuno && DeckRules.Compatible(skill, Yuno)));
            Check(!DeckRules.Compatible(CardCatalog.Magnet, "otro") && DeckRules.Compatible(CardCatalog.Senshuni, "otro"));
        }),
        ("Mazo: hasta dos copias por nombre; los Ataques están exentos", () =>
        {
            var deck = CardCatalog.TrainingDeck();
            Legal(Swap(deck, "pc-8bits", "senshuni"), []);
            Broken(Swap(Swap(deck, "pc-8bits", "senshuni"), "sniper-scope", "senshuni"), [], "Máximo 2 copias de Senshuni");
            Legal(Swap(deck, "pc-8bits", "magnet-shuffle"), []);
        }),
        ("Mazo: los Códigos Trampa van aparte, hasta tres y dos copias de cada uno", () =>
        {
            var deck = CardCatalog.TrainingDeck();
            Broken(Swap(deck, "pc-8bits", "super-magnet"), [], "propio mazo");
            Broken(deck, ["senshuni"], "solo admite Códigos Trampa");
            Legal(deck, ["super-magnet", "super-magnet", "zahira-flair"]);
            Broken(deck, ["super-magnet", "super-magnet", "super-magnet"], "Máximo 2 copias de Súper Imán");
            Broken(deck, ["super-magnet", "super-magnet", "zahira-flair", "super-attack"], "hasta 3 cartas");
            Check(CardCatalog.All.Values.Where(DeckRules.IsTrap).Select(card => card.Id).Order()
                .SequenceEqual(new[] { "super-attack", "super-magnet", "zahira-flair" }));
        }),
        ("Mazo: el duelo arranca con el mazo de Trampas de cada jugador", () =>
        {
            GameEngine Open(List<string> mine)
            {
                var game = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], Hands = [[], []], Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
                    Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id], Traps = [mine, ["super-magnet"]],
                    // Ignored once a Trap deck is supplied.
                    IncludeSuperAttack = true, IncludeZahiraFlair = true, SuperMagnetCopies = [2, 2]
                }, new SeededRandom(3));
                game.State.Turn = 2; game.State.Phase = Phase.Actions;
                return game;
            }
            void TradeAttacks(GameEngine game)
            {
                var player = game.State.Active; var index = player.Hand.Count;
                player.Hand.AddRange([CardCatalog.AttackYuno.Id, CardCatalog.AttackOsamu.Id]); player.ActionsLeft = 2;
                Check(game.Exchange(0, index).Success && game.Exchange(0, index).Success);
            }

            var two = Open(["super-attack", "super-attack"]);
            Check(two.State.Players[0] is { SuperAttackAvailable: true, SuperAttackSpares: 1, ZahiraAvailable: false, SuperMagnetCopies: 0 });
            Check(two.State.Players[1] is { SuperAttackAvailable: false, SuperMagnetCopies: 1 });
            TradeAttacks(two); Check(two.ResolveTrapCraft(0, true).Success);
            two = GameEngine.FromSnapshot(two.SaveSnapshot(), new SeededRandom(3));
            Check(two.State.Players[0] is { SuperAttackAvailable: true, SuperAttackSpares: 0 });
            TradeAttacks(two); Check(two.State.Phase == Phase.TrapCraftChoice && two.ResolveTrapCraft(0, true).Success);
            Check(!two.State.Players[0].SuperAttackAvailable && two.State.Players[0].Hand.Count(id => id == "super-attack") == 2);
            TradeAttacks(two); Check(two.State.Phase == Phase.Actions);

            var none = Open([]);
            TradeAttacks(none); Check(none.State.Phase == Phase.Actions && !none.State.Players[0].ZahiraAvailable);
        }),
        ("Banquillo: hasta diez cartas, sin Ataques y solo compatibles", () =>
        {
            var deck = CardCatalog.TrainingDeck(); var traps = CardCatalog.TrainingTraps();
            // Each of these is in the training deck once, so a second copy on the bench stays within the limit.
            List<string> ten = ["senshuni", "dodge", "bonfire", "time-bomb", "mind-swap", "reroll", "blue-egg", "quick-load", "sniper-scope", "pc-8bits"];
            Legal(deck, traps, ten);
            Broken(deck, traps, ten.Append("plumber-star").ToList(), "hasta 10 cartas");
            Broken(deck, traps, ["attack"], "no admite cartas de Ataque");
            // Triple Ataque Cruzado is a Code, so it may wait in the side deck.
            Legal(deck, traps, ["triple-cross-attack"]);
            Check(DeckRules.Validate("otro", [], [], ["magnet-shuffle"]).Any(error => error.Contains("banquillo tiene cartas no compatibles")));
            Broken(deck, traps, ["no-existe"], "desconocidas: no-existe");
        }),
        ("Banquillo: los límites de copias cuentan mazo y banquillo juntos", () =>
        {
            var deck = CardCatalog.TrainingDeck();
            Legal(deck, [], ["senshuni"]);
            Broken(deck, [], ["senshuni", "senshuni"], "Máximo 2 copias de Senshuni entre el mazo y el banquillo");
            Broken(Swap(deck, "pc-8bits", "senshuni"), [], ["senshuni"], "Máximo 2 copias de Senshuni entre el mazo y el banquillo");
            // Trap Codes may wait in the side deck too; their limit adds the Trap deck.
            Legal(deck, ["super-magnet"], ["super-magnet", "zahira-flair"]);
            Broken(deck, ["super-magnet", "super-magnet"], ["super-magnet"], "Máximo 2 copias de Súper Imán entre el mazo de Códigos Trampa y el banquillo");
        }),
        ("Relikarios: la lista tiene los 18 y solo los programados se pueden elegir", () =>
        {
            var roster = RelikarioCatalog.Roster;
            Check(roster.Count == 18 && roster.Select(r => r.Id).Distinct().Count() == 18 && roster[0].Id == RelikarioCatalog.Yuno.Id);
            Check(roster.Where(r => r.Playable).Select(r => r.Definition!).SequenceEqual(RelikarioCatalog.Playable));
            Check(roster.Where(r => r.Playable).All(r => r.Id == r.Definition!.Id));
        }),
        ("Mazo: cartas y Relikarios desconocidos se rechazan sin lanzar", () =>
        {
            Broken(Swap(CardCatalog.TrainingDeck(), "pc-8bits", "no-existe"), [], "desconocidas: no-existe");
            Check(DeckRules.Validate("yuno-lv2", CardCatalog.TrainingDeck(), []).Any(error => error.Contains("Relikario del mazo no existe")));
            Check(DeckRules.Validate(Yuno, [], []).Count > 0);
        })
    };

    private static List<string> Swap(List<string> deck, string remove, string add)
    {
        var copy = deck.ToList();
        Check(copy.Remove(remove));
        copy.Add(add);
        return copy;
    }

    private static void Legal(List<string> deck, List<string> traps, List<string>? side = null)
    {
        var errors = DeckRules.Validate(Yuno, deck, traps, side);
        if (errors.Count > 0) throw new Exception("Se esperaba un mazo legal: " + string.Join(" | ", errors));
    }

    private static void Broken(List<string> deck, List<string> traps, params string[] expected) => Broken(deck, traps, [], expected);

    private static void Broken(List<string> deck, List<string> traps, List<string> side, params string[] expected)
    {
        var errors = DeckRules.Validate(Yuno, deck, traps, side);
        if (errors.Count != expected.Length || expected.Any(text => !errors.Any(error => error.Contains(text))))
            throw new Exception($"Se esperaba [{string.Join(" | ", expected)}] y se obtuvo [{string.Join(" | ", errors)}]");
    }

    private static void Check(bool condition) { if (!condition) throw new Exception("Comprobación fallida"); }
}
