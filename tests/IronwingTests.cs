using Relick.Core;

static class IronwingTests
{
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Fire = CardCatalog.IronwingFire.Id;
    private static readonly string Roll = CardCatalog.IronwingRoll.Id;
    private static readonly string Area = CardCatalog.BlackHole.Id;
    private static readonly string Code = CardCatalog.Senshuni.Id;

    public static (string, Action)[] Cases =>
    [
        ("Ironwing: mazo, artes alternativas, 9 vidas y una ficha", () =>
        {
            var deck = CardCatalog.TrainingDeck().Where(id => CardCatalog.Get(id).Relikario != "yuno").ToList();
            deck.Remove(Code);
            deck.AddRange([Roll, Fire, CardCatalog.Game.Id, Area]);
            deck[0] = CardCatalog.AttackIronwingUt.Id;
            deck[1] = CardCatalog.AttackIronwingDt.Id;
            deck[2] = CardCatalog.AttackIronwingNs.Id;
            Check(DeckRules.Validate("ironwing", deck, []).Count == 0 && DeckRules.Validate("yuno", deck, []).Count > 0);
            Check(!CardCatalog.IronwingFire.Special && DeckRules.Compatible(CardCatalog.IronwingFire, "ironwing") &&
                !DeckRules.Compatible(CardCatalog.IronwingFire, "yuno") && !DeckRules.Compatible(CardCatalog.IronwingFire, "osamu") &&
                !DeckRules.Compatible(CardCatalog.IronwingRoll, "yuno"));
            var (g, _) = Open();
            Check(g.State.Active.Health == 9 && g.State.Active.Dexterity == 1 && g.State.Active.Relikario == RelikarioCatalog.Ironwing);
        }),
        ("Ironwing Fire: ambos niveles pueden enseñarla y jugarla; una copia antigua fuera de Ironwing no se puede usar", () =>
        {
            foreach (var form in new[] { RelikarioCatalog.Ironwing, RelikarioCatalog.IronwingLv2 })
            {
                var (g, _) = Open(form);
                g.State.Active.Hand.AddRange([Fire, Attack]);
                Check(g.Learn(0, 0).Success && g.CanAttack(0));
                Check(DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.Attack));
            }

            var (other, rng) = Open(RelikarioCatalog.Osamu);
            var p = other.State.Active;
            p.Hand.AddRange([Fire, Attack]);
            p.Deck.AddRange([Attack, Attack, Attack, Code]);
            other.State.Players[1].Equipment = CardCatalog.Pendant.Id;
            Check(!other.Learn(0, 0).Success);
            p.Skills.Add(Fire);
            other = GameEngine.FromSnapshot(other.SaveSnapshot(), rng);
            Check(!other.CanAttack(0) && !other.Attack(0, 1, 0).Success && !other.UseIronwingFire(0, 0).Success);
            Check(DuelOptions.For(other, 0).All(o => o.Move.Kind is not (DuelMoveKind.Attack or DuelMoveKind.IronwingFire)));
            other.State.Active.Skills.Add(CardCatalog.Game.Id);
            Check(DuelOptions.For(other, 0).Single(o => o.Move.Kind == DuelMoveKind.Attack).Move.Value == 1);
        }),
        ("Ironwing Lv 1: paga cuatro Ataques para subir el D6, cambia el Bonus tres veces y cobra el premio", () =>
        {
            var (g, rng) = Open();
            var p = g.State.Active;
            p.Dexterity = 0;
            p.Hand.Add(Attack); p.Skills.Add(Roll);
            p.Bonus = Code; p.Area = Area; p.AreaSideA = 0;
            p.Deck.AddRange([Attack, Attack, Attack, Attack, CardCatalog.ExtraLife.Id, CardCatalog.Game.Id, CardCatalog.Pendant.Id, Code]);
            p.MarkForCorruption(CardZone.Deck, 0);
            rng.EnqueueDie(3);
            Move(g, 0, DuelMoveKind.Attack, index: 0);
            Check(g.State.Phase == Phase.AttackRerollChoice);
            Check(!g.BoostIronwingDie(1, 2).Success && !g.BoostIronwingDie(0, 5).Success);
            Move(g, 0, DuelMoveKind.IronwingDie, value: 4);
            Check(g.State.LastAttackDie == 5 && g.State.Players[1].Health == 8 && p.CorruptedMemory.Single() == Attack);
            Check(g.State.PendingDecision?.Source == "ironwing" && p.Memory.Count == 4);
            for (var i = 0; i < 3; i++) Choose(g, 0, c => c.Value == 0);
            Check(p.Area == null && p.Bonus == CardCatalog.Pendant.Id && p.Deck.Contains(Area));
            Check(g.State.Phase == Phase.Combat && g.State.Players[1].Health == 7 && p.Relikia == 300);
        }),
        ("Ironwing: agotar el mazo con el coste guarda y reanuda el ataque después del reciclaje", () =>
        {
            var (g, rng) = Open();
            var p = g.State.Active;
            p.Dexterity = 0; p.Hand.Add(Attack); p.Skills.Add(Roll);
            p.Deck.AddRange([Attack, CardCatalog.AttackIronwingNs.Id]);
            rng.EnqueueDie(3); rng.EnqueueDie(2);
            Move(g, 0, DuelMoveKind.Attack, index: 0);
            Move(g, 0, DuelMoveKind.IronwingDie, value: 2);
            Check(g.State.Phase == Phase.RecycleChoice && g.RecyclingCount == 2);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveRecycling(0, [0, 2]).Success);
            Check(g.State.Phase == Phase.Combat && g.State.LastAttackDie == 4 &&
                g.State.Active.Relikia == 300 && g.State.Players[1].Health == 7);
        }),
        ("Ironwing evoluciona con Agujero Negro y 1200 PR; elige exactamente cinco Ataques de la memoria", () =>
        {
            var (g, rng) = Open();
            var p = g.State.Active;
            p.Relikia = 1200; p.Bonus = Area;
            Check(!g.CanEvolve(0));
            p.Area = Area; p.AreaSideA = 0; p.Relikia = 1199;
            Check(!g.CanEvolve(0));
            p.Relikia = 1200;
            p.Memory.AddRange([Code, Attack, Attack, Attack, Attack, Attack, CardCatalog.AttackIronwingDt.Id]);
            Move(g, 0, DuelMoveKind.Evolve);
            Check(p.Relikario == RelikarioCatalog.IronwingLv2 && p.Relikia == 0 && p.ActionsLeft == 2);
            Check(DuelSnapshot.For(g, 1, 0).Options.All(option => option.Move.Kind != DuelMoveKind.Choose));
            Choose(g, 0, c => c.CardId == CardCatalog.AttackIronwingDt.Id);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            for (var i = 0; i < 4; i++) Choose(g, 0, c => c.Value == i + 1 && c.CardId == Attack);
            Check(g.State.Active.Memory.SequenceEqual([Code, Attack]) && g.State.Active.Deck.Count == 5 &&
                g.State.Active.Deck.Contains(CardCatalog.AttackIronwingDt.Id));
            g.State.Active.Hand.Add(Attack); g.State.Active.Skills.Add(Roll);
            rng.EnqueueDie(1);
            Move(g, 0, DuelMoveKind.Attack, index: 0);
            Check(g.State.LastAttackDie == 2);
        }),
        ("Ironwing Lv 2: puede aumentar un dado de efecto propio pagando un Ataque por punto", () =>
        {
            var (g, rng) = Open(RelikarioCatalog.IronwingLv2);
            var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Health = 5; p.Dexterity = 0;
            p.Hand.Add(CardCatalog.ExtraLife.Id); p.Deck.AddRange([Attack, Attack, Code]);
            rng.EnqueueDie(4);
            Move(g, 0, DuelMoveKind.PlayCode, index: 0);
            Check(g.State.Phase == Phase.DieRerollChoice);
            Move(g, 0, DuelMoveKind.IronwingDie, value: 1);
            Check(g.State.PendingDieRoll?.Die == 5);
            Move(g, 0, DuelMoveKind.IronwingDie, value: 1);
            Check(p.Health == 9 && p.Deck.SequenceEqual([Code]) && g.State.Phase == Phase.Actions);
        }),
        ("Agujero Negro: resuelve ambos lados, preserva la carta elegida y repone un Bonus destruido", () =>
        {
            var (g, rng) = Open();
            var p = g.State.Active; p.Dexterity = 0; g.State.Players[1].Dexterity = 0;
            p.Hand.Add(Area); p.Skills.Add(Roll); p.Bonus = Code; p.Equipment = CardCatalog.Pendant.Id;
            p.Deck.AddRange([Attack, Code]);
            g.State.Phase = Phase.Actions;
            rng.EnqueueDie(4); rng.EnqueueDie(6);
            Move(g, 0, DuelMoveKind.ActivateArea, index: 0);
            Choose(g, 0, c => c.Value == 0);
            Choose(g, 0, c => c.Value == 0);
            Move(g, 0, DuelMoveKind.EffectDie);
            Check(g.State.PendingDecision?.Kind == DecisionKind.ChooseTarget);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Choose(g, 0, c => c.Field?.CardId == Roll);
            p = g.State.Active;
            Check(p.Area == null && p.Equipment == null && p.Bonus == Attack && p.Skills.Single() == Roll &&
                p.Memory.ToHashSet().SetEquals([Area, Code, CardCatalog.Pendant.Id]) && p.ActionsLeft == 1 &&
                g.State.Phase == Phase.Actions && g.State.LastDie == 6);
        }),
        ("Agujero Negro: desde el Bonus inicial y el lado A a la mano rival sin lanzar dados", () =>
        {
            var rng = new ScriptedRandom();
            var g = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Relikarios = ["ironwing", "yuno"], Hands = [[], []],
                Decks = [[Attack, Code], []], Bonuses = [Area, null]
            }, rng);
            Check(g.State.Active.Area == Area && g.State.Active.Bonus == Attack);
            Choose(g, 0, c => c.Value == 1);
            Choose(g, 1, c => c.Value == 1);
            Check(g.State.Active.Area == null && g.State.Players[1].Hand.Single() == Area &&
                g.State.Players[1].IsForeignHandCard(0) && g.State.LastDie == 0);
        }),
        ("Agujero Negro: el dado de Ironwing Lv 2 se cambia a 6 y conserva el Área", () =>
        {
            var (g, rng) = Open(RelikarioCatalog.IronwingLv2);
            g.State.Phase = Phase.Actions;
            g.State.Active.Hand.Add(Area);
            var adjusted = new List<int>();
            g.DieAdjusted += adjusted.Add;
            rng.EnqueueDie(2); rng.EnqueueDie(6);
            Move(g, 0, DuelMoveKind.ActivateArea, index: 0);
            Choose(g, 0, c => c.Value == 0);
            Choose(g, 0, c => c.Value == 0);
            Check(adjusted.SequenceEqual([6]) && g.State.Active.Area == Area && g.State.Phase == Phase.Actions);
        }),
        ("Agujero Negro: el lado B elige qué conservar si el lado A conserva el Área con un 6", () =>
        {
            var (g, rng) = Open();
            var rival = g.State.Players[1];
            rival.Dexterity = 0; rival.Equipment = CardCatalog.DiamondHelmet.Id;
            rival.Skills.Add(CardCatalog.Destiny.Id); rival.Exchange.Add(Code);
            g.State.Phase = Phase.Actions;
            g.State.Active.Hand.Add(Area);
            rng.EnqueueDie(6); rng.EnqueueDie(5);
            Move(g, 0, DuelMoveKind.ActivateArea, index: 0);
            Choose(g, 0, c => c.Value == 0);
            Choose(g, 0, c => c.Value == 0);
            Check(g.State.DecisionPlayer == 1 && g.DecisionChoices(0).Count == 0);
            Choose(g, 1, c => c.Field?.Zone == FieldZone.Equipment);
            Check(g.State.Active.Area == Area && rival.Equipment == CardCatalog.DiamondHelmet.Id &&
                rival.Skills.Count == 0 && rival.Exchange.Count == 0 && g.State.Phase == Phase.Actions);
        }),
        ("Ironwing Roll: cuesta dos Ataques o cuatro ante crítico y se destruye con el tercer contador", () =>
        {
            foreach (var (die, cost) in new[] { (4, 2), (5, 2), (6, 4) })
            {
                var (g, rng) = Open(RelikarioCatalog.Yuno, RelikarioCatalog.Ironwing);
                var p = g.State.Players[1];
                g.State.Active.Hand.Add(Attack); g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
                p.Skills.Add(Roll); p.SkillCounters[0] = 2;
                p.Deck.AddRange(Enumerable.Repeat(Attack, cost).Append(Code));
                rng.EnqueueDie(die);
                Move(g, 0, DuelMoveKind.Attack, index: 0);
                if (g.State.Phase == Phase.AttackRerollChoice) Move(g, 0, DuelMoveKind.AttackDie);
                Check(g.State.Phase == Phase.DefenseChoice && g.IronwingRollCost == cost);
                Move(g, 1, DuelMoveKind.IronwingRoll, index: 0);
                Check(p.Health == 9 && p.Deck.Single() == Code && p.Skills.Count == 0 && p.Memory.Count == cost + 1 &&
                    g.State.Active.Relikia == CardCatalog.Destiny.Reward);
            }
        }),
        ("Ironwing Fire: aumenta el daño o destruye una no-habilidad, una vez por copia y turno", () =>
        {
            var (g, rng) = Open();
            var p = g.State.Active; var rival = g.State.Players[1];
            p.Hand.Add(Attack); p.Skills.AddRange([Fire, Fire]);
            p.Deck.AddRange(Enumerable.Repeat(Attack, 5).Append(Code));
            rival.Equipment = CardCatalog.Pendant.Id; rival.Skills.Add(CardCatalog.Magnet.Id);
            rng.EnqueueDie(5);
            Move(g, 0, DuelMoveKind.Attack, index: 0);
            Choose(g, 0, c => c.Value == 0);
            Choose(g, 0, c => c.Value < 0);
            Check(rival.Health == 4 && p.Relikia == 400 && !g.IronwingFireDestructions(0).Contains(0));
            Move(g, 0, DuelMoveKind.IronwingFire, index: 1);
            Check(g.DecisionChoices(0).All(c => c.Field?.Zone != FieldZone.Skill));
            Choose(g, 0, c => c.Field?.Zone == FieldZone.Equipment);
            Check(rival.Equipment == null && rival.Memory.Single() == CardCatalog.Pendant.Id && p.Deck.Single() == Code &&
                p.ActionsLeft == 2 && p.IronwingFireUsed!.SetEquals([0, 1]));
        })
    ];

    private static (GameEngine Game, ScriptedRandom Random) Open(RelikarioDefinition? own = null, RelikarioDefinition? opponent = null)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Relikarios = [(own ?? RelikarioCatalog.Ironwing).Id, (opponent ?? RelikarioCatalog.Yuno).Id],
            Hands = [[], []], Decks = [[], []], Bonuses = [null, null]
        }, random);
        game.State.Turn = 2;
        return (game, random);
    }

    private static void Move(GameEngine game, int actor, DuelMoveKind kind, int index = -1, int value = 0)
    {
        var result = new DuelMove { Kind = kind, Index = index, Value = value }.Apply(game, actor);
        if (!result.Success) throw new Exception(result.Message);
    }

    private static void Choose(GameEngine game, int actor, Func<DecisionChoice, bool> matches)
    {
        var index = game.DecisionChoices(actor).Select((choice, i) => (choice, i)).Single(c => matches(c.choice)).i;
        Move(game, actor, DuelMoveKind.Choose, index);
    }

    private static void Check(bool condition, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        if (!condition) throw new Exception($"Ironwing · línea {line}.");
    }
}
