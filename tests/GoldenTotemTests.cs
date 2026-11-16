using Relick.Core;

static class GoldenTotemTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Oro: Personaje sin COMBO, una acción y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.GoldenTotem;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Character && c.DeployTotems && !c.IsCombo && c.ActionCost == 1 && c.PrCost == 0);
            var deck = CardCatalog.TrainingDeck();
            Check(deck.Count == 42 && deck.Count(id => id == c.Id) == 1 && CardCatalog.All.ContainsKey(c.Id));
        }),
        ("Oro: coloca cero, uno o dos Tótems elegidos, sustituye otro equipo y conserva cartas", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var amount in new[] { 0, 1, 2 })
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active; p.Equipment = CardCatalog.Pc.Id; p.EquipmentDie = 4; var count = Count(g);
                Check(g.PlayCode(actor, 0).Success && g.State.Phase == Phase.GoldenTotemChoice && p.ActionsLeft == 1);
                Check(p.EquipmentCovers == null && p.Hand.Count == 3 && p.Memory.Single() == CardCatalog.GoldenTotem.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(new DuelMove { Kind = DuelMoveKind.GoldenTotem, Discards = Enumerable.Range(0, amount).ToArray() }.Apply(g, actor).Success);
                Check(p.EquipmentCovers!.Single() == CardCatalog.GoldenTotem.Id && p.Hand.Count == 3 - amount && p.ActionsLeft == 1 && Count(g) == count);
                Check(amount == 0 ? p.Equipment == CardCatalog.Pc.Id && p.EquipmentDie == 4 :
                    p.Equipment == CardCatalog.Totem.Id && p.EquipmentStackedCopies == amount - 1 && p.Memory.Single() == CardCatalog.Pc.Id);
                Check(p.RevealedHandSlots.Count == 0 && g.State.PendingGoldenTotem == null && g.State.Phase == Phase.Actions);
                Check(DuelSnapshot.For(g, 1 - actor, 2).Players[actor].EquipmentCovers!.Single() == CardCatalog.GoldenTotem.Id);
            }
        }),
        ("Oro: exige equipo debajo y rechaza cero si aún no existe, antes de mutar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.RemoveAll(id => id == CardCatalog.Totem.Id);
            Reject(g, () => g.PlayCode(0, 0)); p.Hand.Add(CardCatalog.Totem.Id);
            Check(g.PlayCode(0, 0).Success);
            Check(DuelOptions.For(g, 0).Where(o => o.Move.Kind == DuelMoveKind.GoldenTotem).All(o => o.RequiredDiscards == 1));
            Reject(g, () => g.ResolveGoldenTotem(0, []));
            Check(g.ResolveGoldenTotem(0, [1]).Success && p.Equipment == CardCatalog.Totem.Id && p.EquipmentCovers!.Count == 1);
        }),
        ("Oro: valida propietario, índices distintos, tipo de carta y máximo de dos", () =>
        {
            var (g, _) = Fixture(); g.State.Active.Hand.Add(CardCatalog.Totem.Id);
            Reject(g, () => g.ResolveGoldenTotem(0, [])); Check(g.PlayCode(0, 0).Success);
            Reject(g, () => g.ResolveGoldenTotem(1, [0])); Reject(g, () => g.ResolveGoldenTotem(0, [0, 0]));
            Reject(g, () => g.ResolveGoldenTotem(0, [0, 1, 3])); Reject(g, () => g.ResolveGoldenTotem(0, [2]));
            Reject(g, () => g.ResolveGoldenTotem(0, [-1])); Reject(g, () => g.ResolveGoldenTotem(0, [99]));
            Reject(g, () => g.Advance(0)); Check(g.ResolveGoldenTotem(0, [1, 3]).Success);
        }),
        ("Oro: añade Tótems a la pila existente y otra copia de Oro sin destruir nada", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; Stack(p, 2, 1); var count = Count(g);
            Check(g.PlayCode(0, 0).Success && g.ResolveGoldenTotem(0, [0, 1]).Success);
            Check(p.EquipmentStackedCopies == 3 && p.EquipmentCovers!.Count == 2 && p.Memory.Count == 0 && Count(g) == count);
        }),
        ("Oro: Error inicial impide colocar o revelar Tótems; la selección ya iniciada no se cancela", () =>
        {
            foreach (var initial in new[] { true, false })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; Stack(p, 1, 1);
                if (initial) g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                var count = Count(g); Check(g.PlayCode(0, 0).Success);
                if (!initial) { g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id); count++; }
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                if (!initial)
                {
                    Reject(g, () => g.PlayCode(1, 1));
                    Check(!g.CanPlayLoadError(1) && g.ResolveGoldenTotem(0, [0]).Success);
                    Check(p.ActionsLeft == 1 && p.EquipmentStackedCopies == 1 && p.EquipmentCovers!.Count == 2 && Count(g) == count);
                    continue;
                }
                Check(g.PlayCode(1, 1).Success && p.ActionsLeft == 2 && p.EquipmentStackedCopies == 0 && p.EquipmentCovers!.Count == 1);
                Check(p.Hand.Count == 3 && p.RevealedHandSlots.Count == 0 && p.Memory.Single() == CardCatalog.GoldenTotem.Id && Count(g) == count && g.State.PendingGoldenTotem == null);
            }
        }),
        ("Oro: un contraerror conserva la selección y coloca una sola copia de Oro", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 1).Success && g.PlayCode(0, p.Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveGoldenTotem(0, [0, 1]).Success && p.EquipmentCovers!.Count == 1 && p.EquipmentStackedCopies == 1 && p.ActionsLeft == 0);
        }),
        ("Oro: Caja no responde a Personaje y Relikia no puede activarlo", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            p.Memory.Add(CardCatalog.GoldenTotem.Id); p.Relikia = 500;
            Check(!g.CanReplayMemoryCode(0, 0) && !g.ReplayableMemoryCodes(0).Contains(0));
            Reject(g, () => g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0));
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.GoldenTotemChoice);
            Reject(g, () => g.ResolveCodeResponse(1, true)); Check(g.ResolveGoldenTotem(0, [0]).Success);
            Check(g.State.Players[1].Equipment == CardCatalog.CardboardBox.Id);
        }),
        ("Oro: muestra solo las copias elegidas durante una sustitución protegida y recupera el progreso", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pc.Id; p.EquipmentDie = 4; p.Skills.Add(CardCatalog.Reverse.Id); var count = Count(g);
            Check(g.PlayCode(0, 0).Success && g.ResolveGoldenTotem(0, [0, 1]).Success && g.State.Phase == Phase.DestructionResponse);
            var view = DuelSnapshot.For(g, 1, 1);
            Check(view.Players[0].Hand[0] == CardCatalog.Totem.Id && view.Players[0].Hand[1] == CardCatalog.Totem.Id && view.Players[0].Hand[2] == null);
            Check(!g.CanPlayLoadError(1)); g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDestructionProtection(0, 1).Success && p.Equipment == CardCatalog.Pc.Id && p.EquipmentDie == 4 && p.EquipmentCovers!.Count == 1);
            Check(p.Memory.Count(id => id == CardCatalog.Totem.Id) == 2 && p.RevealedHandSlots.Count == 0 && p.Hand.Single() == CardCatalog.Attack.Id && Count(g) == count);
        }),
        ("Oro: una protección letal destruye Oro y conserva el único Tótem y todas las vidas", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1, 1); target.Health = 3; var count = Count(g);
            var visuals = DuelVisualEvents.Capture(g); Attack(g, rng);
            Check(g.State.Phase == Phase.TotemChoice && g.ResolveTotem(1, true).Success);
            Check(target.Health == 3 && target.Equipment == CardCatalog.Totem.Id && target.EquipmentCovers == null && target.Memory.Single() == CardCatalog.GoldenTotem.Id);
            Check(!target.HasReachedZero && Count(g) == count && visuals.Count(c => c.Kind == DuelVisualKind.Hit) == 0);
            Check(visuals.Single(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.EquipmentCover).Destination == CardZone.Memory);
        }),
        ("Oro: daño no letal consume una cobertura por ocasión y conserva el bonus de dos Tótems", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2, 1);
            Attack(g, rng); Check(target.Health == 5 && target.EquipmentStackedCopies == 1 && target.EquipmentCovers == null && target.Memory.Single() == CardCatalog.GoldenTotem.Id);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add(CardCatalog.Attack.Id); Attack(g, rng);
            Check(target.Health == 2 && target.EquipmentStackedCopies == 0 && target.Memory.Count(id => id == CardCatalog.Totem.Id) == 1);
        }),
        ("Oro: una sola copia de Salvación no consume Oro con daño no letal", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1, 1); Attack(g, rng);
            Check(target.Health == 5 && target.EquipmentCovers!.Count == 1 && target.Memory.Count == 0);
        }),
        ("Oro: Huevo sustituye la destrucción de Salvación y Bomba puede destruir Oro directamente", () =>
        {
            foreach (var direct in new[] { false, true })
            {
                var (g, _) = Fixture(); var target = g.State.Players[1]; Stack(target, 2, 2); var p = g.State.Active;
                p.Relikia = 200; p.Hand[0] = direct ? CardCatalog.TimeBomb.Id : CardCatalog.BlueEgg.Id;
                Check(g.PlayCode(0, 0).Success);
                var choice = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Field is { Owner: 1 } field &&
                    field.Zone == (direct ? FieldZone.EquipmentCover : FieldZone.Equipment) && field.Index == (direct ? 1 : -1));
                Check(choice.Move.Apply(g, 0).Success && target.EquipmentStackedCopies == 1 && target.EquipmentCovers!.Count == 1 && target.Memory.Single() == CardCatalog.GoldenTotem.Id);
            }
        }),
        ("Oro: protege Salvación al sustituir equipo y el equipo nuevo queda en memoria", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; Stack(p, 2, 1); p.Hand[0] = CardCatalog.Pendant.Id; var count = Count(g);
            Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.Totem.Id && p.EquipmentStackedCopies == 1 && p.EquipmentCovers == null);
            Check(p.Memory.Contains(CardCatalog.Pendant.Id) && p.Memory.Contains(CardCatalog.GoldenTotem.Id) && Count(g) == count);
        }),
        ("Oro: Reverse sustituye la destrucción de Oro sin consumir la pila ni alterar el daño", () =>
        {
            foreach (var protect in new[] { true, false })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2, 1); target.Skills.Add(CardCatalog.Reverse.Id);
                Attack(g, rng); Check(g.State.Phase == Phase.DestructionResponse && g.State.PendingDestruction!.Target is { Zone: FieldZone.EquipmentCover, CardId: "golden-totem" });
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); target = g.State.Players[1];
                Check(g.ResolveDestructionProtection(1, protect ? 0 : -1).Success && target.Health == 5 && target.EquipmentStackedCopies == 1);
                Check(protect ? target.EquipmentCovers!.Count == 1 && target.Memory.Single() == CardCatalog.Reverse.Id : target.EquipmentCovers == null && target.Memory.Single() == CardCatalog.GoldenTotem.Id);
                Check(g.State.PendingAttack == null && g.State.Active.Relikia == 600);
            }
        }),
        ("Oro: pasar Error impide cancelar Huevo durante la protección; Reverse sigue protegiendo la cobertura", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; var p = g.State.Active; Stack(target, 2, 1);
            target.Skills.Add(CardCatalog.Reverse.Id); target.Hand.Add(CardCatalog.LoadError.Id); p.Hand[0] = CardCatalog.BlueEgg.Id; p.Relikia = 200;
            Check(g.PlayCode(0, 0).Success);
            if (g.State.Phase == Phase.ErrorResponse) Check(g.PassErrorResponse(1).Success);
            Check(g.ResolveDestruction(0, 1, true).Success && g.State.Phase == Phase.DestructionResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; target = g.State.Players[1];
            Reject(g, () => g.PlayCode(1, 1));
            Check(!g.CanPlayLoadError(1) && g.ResolveDestructionProtection(1, 0).Success);
            Check(target.EquipmentCovers!.Count == 1 && target.EquipmentStackedCopies == 1 && target.Skills.Count == 0);
            Check(p.ActionsLeft == 1 && p.Relikia == 200 && g.State.PendingDestruction == null &&
                (g.State.Phase == Phase.Decision && g.State.PendingDecision!.Kind == DecisionKind.Confirm));
        }),
        ("Oro: varias coberturas absorben intentos sucesivos, una por intento", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1, 2); target.Health = 3;
            for (var left = 1; left >= 0; left--)
            {
                Attack(g, rng); Check(g.ResolveTotem(1, true).Success && target.Health == 3 && target.Equipment == CardCatalog.Totem.Id && (target.EquipmentCovers?.Count ?? 0) == left);
                g.State.AttackedThisTurn = false; g.State.Active.Hand.Add(CardCatalog.Attack.Id);
            }
            Check(target.Memory.Count == 2 && target.Memory.All(id => id == CardCatalog.GoldenTotem.Id));
        }),
        ("Oro: no protege otros equipos y puede permanecer solo tras perder el equipo inferior", () =>
        {
            var (g, _) = Fixture(); var target = g.State.Players[1]; target.Equipment = CardCatalog.Pc.Id; target.EquipmentCovers = [CardCatalog.GoldenTotem.Id];
            g.State.Active.Hand[0] = CardCatalog.BlueEgg.Id; g.State.Active.Relikia = 200;
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, true).Success);
            Check(target.Equipment == null && target.EquipmentCovers!.Count == 1 && target.Memory.Single() == CardCatalog.Pc.Id);
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var hands = new List<string>[] { [CardCatalog.Attack.Id], [CardCatalog.Attack.Id] };
        hands[actor] = [CardCatalog.GoldenTotem.Id, CardCatalog.Totem.Id, CardCatalog.Totem.Id, CardCatalog.Attack.Id];
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = hands,
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        g.State.Turn = 2;
        foreach (var p in g.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
        g.State.Phase = Phase.Actions; g.State.Active.Skills.Add(CardCatalog.Destiny.Id); return (g, rng);
    }
    static void Stack(PlayerState p, int totems, int covers)
    { p.Equipment = CardCatalog.Totem.Id; p.EquipmentStackedCopies = totems - 1; p.EquipmentCovers = Enumerable.Repeat(CardCatalog.GoldenTotem.Id, covers).ToList(); }
    static void Attack(GameEngine g, ScriptedRandom rng)
    {
        g.State.Phase = Phase.Combat; var p = g.State.Active;
        var index = p.Hand.IndexOf(CardCatalog.Attack.Id); rng.EnqueueDie(5);
        Check(g.Attack(g.State.CurrentPlayer, index, 0).Success);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count + p.Exchange.Count + p.AttachedCards.Count +
        (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1));
    static void Reject(GameEngine g, Func<CommandResult> action) { var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot()); }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Tótem de Oro incorrecta."); }
}
