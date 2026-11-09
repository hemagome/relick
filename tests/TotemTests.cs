using Relick.Core;

static class TotemTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Tótem: equipo apilable y una copia en el mazo de 42 cartas", () =>
        {
            Check(CardCatalog.Totem.Kind == CardKind.Equipment && CardCatalog.Totem.Stackable);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == CardCatalog.Totem.Id) == 1);
        }),
        ("Tótem: equipar otra copia apila sin destruir ni pedir sustitución a Reverse", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions;
            p.Hand.Add(CardCatalog.Totem.Id); p.Skills.Add(CardCatalog.Reverse.Id); Stack(p, 2); var count = Count(g);
            Check(g.Equip(0, 1).Success && p.EquipmentStackedCopies == 2 && p.Memory.Count == 0 && p.ActionsLeft == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.State.Active.EquipmentStackedCopies == 2 && Count(g) == count && g.State.Phase == Phase.Actions);
            Check(DuelSnapshot.For(g, 1, 1).Players[0].EquipmentStackedCopies == 2);
        }),
        ("Tótem: Error anula solo la copia que se equipa y conserva la pila previa", () =>
        {
            foreach (var cancel in new[] { true, false })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; Stack(p, 2);
                p.Hand.Add(CardCatalog.Totem.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id); var count = Count(g);
                Check(g.Equip(0, 1).Success && g.State.Phase == Phase.ErrorResponse && p.EquipmentStackedCopies == 1);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check((cancel ? g.PlayCode(1, 1) : g.PassErrorResponse(1)).Success);
                Check(p.Equipment == CardCatalog.Totem.Id && p.EquipmentStackedCopies == (cancel ? 1 : 2));
                Check(p.ActionsLeft == (cancel ? 2 : 1) && Count(g) == count && p.Memory.Contains(CardCatalog.Totem.Id) == cancel);
            }
        }),
        ("Tótem: otro equipo sustituye toda la pila sin perder copias", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; Stack(p, 3);
            p.Hand.Add(CardCatalog.Pendant.Id); var count = Count(g);
            Check(g.Equip(0, 1).Success && p.Equipment == CardCatalog.Pendant.Id && p.EquipmentStackedCopies == 0);
            Check(p.Memory.Count(id => id == CardCatalog.Totem.Id) == 3 && Count(g) == count);
        }),
        ("Tótem: Reverse puede conservar una pila durante su sustitución tras destruir una copia", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; Stack(p, 3);
            p.Skills.Add(CardCatalog.Reverse.Id); p.Hand.Add(CardCatalog.Pendant.Id); var count = Count(g);
            Check(g.Equip(0, 1).Success && g.State.Phase == Phase.DestructionResponse);
            Check(g.ResolveDestructionProtection(0).Success && p.EquipmentStackedCopies == 1 && g.State.Phase == Phase.DestructionResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDestructionProtection(0, 1).Success && p.EquipmentStackedCopies == 1 && p.Equipment == CardCatalog.Totem.Id);
            Check(p.Memory.Contains(CardCatalog.Pendant.Id) && p.Memory.Count(id => id == CardCatalog.Totem.Id) == 1 && Count(g) == count);
        }),
        ("Tótem: una destrucción externa retira solo una copia y mantiene las restantes equipadas", () =>
        {
            var (g, _) = Fixture(); var target = g.State.Players[1]; Stack(target, 2); var count = Count(g);
            g.State.Phase = Phase.Actions; g.State.Active.Relikia = 200; g.State.Active.Hand.Add(CardCatalog.BlueEgg.Id); count++;
            Check(g.PlayCode(0, 1).Success && g.ResolveDestruction(0, 1, true).Success);
            Check(target.Equipment == CardCatalog.Totem.Id && target.EquipmentStackedCopies == 0 && target.Memory.Single() == CardCatalog.Totem.Id && Count(g) == count);
        }),
        ("Tótem: una copia conserva los fallos y no se consume con daño no letal", () =>
        {
            var (g, rng) = Fixture(); Stack(g.State.Active, 1); Hit(g, rng, 1);
            Check(g.State.Players[1].Health == 8 && g.State.Active.Relikia == 0 && g.State.Active.Equipment == CardCatalog.Totem.Id);
            (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1); Hit(g, rng, 5);
            Check(target.Health == 5 && target.Equipment == CardCatalog.Totem.Id && target.Memory.Count == 0);
        }),
        ("Tótem: todos los fallos apilados infligen daño normal, entregan premio y evitan penalización del uno", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            foreach (var die in new[] { 1, 2, 3 })
            {
                var (g, rng) = Fixture(actor); Stack(g.State.Active, 2); var target = g.State.Players[1 - actor];
                target.Equipment = CardCatalog.DiamondHelmet.Id;
                var visuals = DuelVisualEvents.Capture(g); Hit(g, rng, die);
                Check(target.Health == 7 && target.Equipment == CardCatalog.DiamondHelmet.Id && g.State.Active.Relikia == 600);
                Check(g.State.LastAttackDie == die && g.State.LastDie == die && g.State.Active.EquipmentStackedCopies == 1);
                Check(visuals.Single(c => c.Kind == DuelVisualKind.Hit) is { NormalFromMiss: true } hit && hit.AttackDie == die);
                Check(visuals.Any(c => c.Kind == DuelVisualKind.Attack));
            }
        }),
        ("Tótem: también convierte el fallo final de un relanzamiento sin ejecutar su penalización", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var target = g.State.Players[1];
            Stack(p, 2); p.AttackRerollUsedThisTurn = false; target.Equipment = CardCatalog.DiamondHelmet.Id;
            rng.EnqueueDie(4); rng.EnqueueDie(1);
            Check(g.Attack(0, 0, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
            Check(g.ResolveAttackReroll(0, true).Success && g.State.Phase == Phase.Combat && g.State.LastAttackDie == 1);
            Check(p.AttackRerollUsedThisTurn && target.Equipment == CardCatalog.DiamondHelmet.Id && target.Health == 7 && p.Relikia == 600);
        }),
        ("Tótem: conserva los resultados reducido, normal y crítico", () =>
        {
            foreach (var die in new[] { 4, 5, 6 })
            {
                var (g, rng) = Fixture(); Stack(g.State.Active, 2); Hit(g, rng, die);
                Check(g.State.Players[1].Health == 8 - GameEngine.DamageFor(die, 3, 5));
                Check(g.State.Active.EquipmentStackedCopies == 1 && g.State.LastAttackDie == die);
            }
        }),
        ("Tótem: Magnet lanza su daño normal al convertir un fallo y no activa efecto crítico", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Skills[0] = CardCatalog.Magnet.Id; Stack(g.State.Active, 2);
            rng.EnqueueDie(2); rng.EnqueueDie(4); Check(g.Attack(0, 0, 0).Success);
            Check(g.State.Players[1].Health == 4 && g.State.LastAttackDie == 2 && g.State.LastDie == 4);
            Check(g.State.Phase == Phase.Combat && g.State.Active.Relikia == 500 && g.State.Players[1].Hand.Count == 1);
        }),
        ("Tótem: cancelar daño crítico conserva el efecto de Magnet y difiere el premio", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var target = g.State.Players[1];
            p.Skills[0] = CardCatalog.Magnet.Id; Stack(target, 1); target.Health = 5;
            Hit(g, rng, 6); Check(g.State.Phase == Phase.TotemChoice && g.ResolveTotem(1, true).Success);
            Check(g.State.Phase == Phase.MagnetChoice && p.Relikia == 0 && target.Health == 5);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.ResolveMagnet(0, false, 0).Success && g.State.Active.Relikia == 500 && g.State.Players[1].Hand.Count == 0);
        }),
        ("Tótem: el daño no letal destruye una sola copia y deja de consumir al quedar una", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2); var count = Count(g);
            Hit(g, rng, 5); Check(target.Health == 5 && target.EquipmentStackedCopies == 0 && target.Memory.Count == 1);
            g.State.AttackedThisTurn = false; g.State.Active.Hand.Add(CardCatalog.Attack.Id); count++;
            Hit(g, rng, 5); Check(target.Health == 2 && target.Equipment == CardCatalog.Totem.Id && target.Memory.Count == 1 && Count(g) == count);
        }),
        ("Tótem: cancelar daño letal usa una sola copia incluso en una pila y tras duplicar el daño", () =>
        {
            foreach (var copies in new[] { 1, 2, 3 })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, copies); target.Health = 4; g.State.Active.StarEffects = 1;
                Hit(g, rng, 5); Check(g.State.Phase == Phase.TotemChoice && target.Health == 4 && g.State.Active.Relikia == 0);
                Check(g.State.PendingTotemDamage!.Amount == 6 && DuelOptions.For(g, 1).Count(o => o.Move.Kind == DuelMoveKind.Totem) == 2);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); target = g.State.Players[1];
                Check(new DuelMove { Kind = DuelMoveKind.Totem, Flag = true }.Apply(g, 1).Success);
                Check(target.Health == 4 && !target.HasReachedZero && target.Memory.Count == 1 && g.State.Active.Relikia == 600);
                Check(copies == 1 ? target.Equipment == null : target.Equipment == CardCatalog.Totem.Id && target.EquipmentStackedCopies == copies - 2);
                Check(g.State.PendingTotemDamage == null && g.State.PendingAttack == null && g.State.Phase == Phase.Combat);
            }
        }),
        ("Tótem: rechazar protección aplica daño y consume una copia solo si estaba apilado", () =>
        {
            foreach (var copies in new[] { 1, 2 })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, copies); target.Health = 3;
                Hit(g, rng, 5); Check(g.ResolveTotem(1, false).Success);
                Check(target.Health == 0 && target.HasReachedZero && !g.State.IsOver && target.Equipment == CardCatalog.Totem.Id && target.EquipmentStackedCopies == 0);
                Check(target.Memory.Count == copies - 1);
            }
        }),
        ("Tótem: protege estando ya a cero y rechazar entonces permite el daño definitivo", () =>
        {
            foreach (var cancel in new[] { true, false })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1); target.Health = 0; target.HasReachedZero = true;
                Hit(g, rng, 5); Check(g.ResolveTotem(1, cancel).Success);
                Check(cancel ? !g.State.IsOver && target.Health == 0 && target.Equipment == null : g.State.IsOver && g.State.Winner == 0);
            }
        }),
        ("Tótem: Destiny y Estrella cancelan el daño antes de consumir una copia", () =>
        {
            foreach (var star in new[] { true, false })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2);
                if (star) target.StarEffects = 1;
                else { target.Skills.Add(CardCatalog.Destiny.Id); target.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 2)); }
                Hit(g, rng, 5); if (!star) Check(g.ResolveDefense(1, 0, [0, 1, 2]).Success);
                Check(target.Health == 8 && target.EquipmentStackedCopies == 1 && g.State.PendingTotemDamage == null && g.State.Active.Relikia == 600);
            }
        }),
        ("Tótem: Reverse sustituye la destrucción del coste o del daño sin cambiar su resultado", () =>
        {
            foreach (var cancelLethal in new[] { true, false })
            {
                var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2); target.Skills.Add(CardCatalog.Reverse.Id);
                target.Health = cancelLethal ? 3 : 8; Hit(g, rng, 5);
                if (cancelLethal) Check(g.ResolveTotem(1, true).Success);
                Check(g.State.Phase == Phase.DestructionResponse && g.State.Active.Relikia == 0);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); target = g.State.Players[1];
                Check(g.ResolveDestructionProtection(1, 0).Success && target.EquipmentStackedCopies == 1 && target.Memory.Single() == CardCatalog.Reverse.Id);
                Check(target.Health == (cancelLethal ? 3 : 5) && g.State.Active.Relikia == 600 && g.State.PendingAttack == null);
            }
        }),
        ("Tótem: Jessica conserva el robo de su seis tras cancelar daño y reconectar", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active; var target = g.State.Players[1 - actor];
                p.Bonus = CardCatalog.Jessica.Id; Stack(target, 2); target.Health = 3; var deck = p.Deck.Count;
                rng.EnqueueDie(6); Check(g.UseFieldCode(actor, FieldZone.Bonus).Success && g.State.Phase == Phase.TotemChoice && p.Deck.Count == deck);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; target = g.State.Players[1 - actor];
                Check(g.ResolveTotem(1 - actor, true).Success && target.Health == 3 && target.EquipmentStackedCopies == 0 && p.Deck.Count == deck - 1);
                Check(g.State.PendingTotemDamage == null && g.State.Phase == Phase.Combat && g.FieldCombatTargets(actor).Count() == 0);
                Check(g.Attack(actor, 0, 0).Success);
            }
        }),
        ("Tótem: agotamiento inevitable no se cancela ni consume la pila", () =>
        {
            var (g, _) = Fixture(); var target = g.State.Players[1]; Stack(target, 2); target.Deck.Clear(); target.Recycled = true; target.Health = 1;
            g.State.Phase = Phase.Actions; Check(g.Advance(0).Success);
            Check(g.State.IsOver && g.State.Winner == 0 && target.Health == 0 && target.EquipmentStackedCopies == 1 && g.State.PendingTotemDamage == null);
        }),
        ("Tótem: abandonar durante la decisión conserva el resultado final y no consume copias", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 2); target.Health = 3;
            Hit(g, rng, 5); Check(g.Concede(1).Success && g.State.IsOver && g.State.Winner == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(DuelSnapshot.For(g, 1, 1).Stage == Phase.Finished && DuelOptions.For(g, 1).Count == 0 && g.State.Players[1].EquipmentStackedCopies == 1);
            Reject(g, () => g.ResolveTotem(1, true));
        }),
        ("Tótem: valida actor, etapa y exclusión de otros códigos mientras se elige protección", () =>
        {
            var (g, rng) = Fixture(); var target = g.State.Players[1]; Stack(target, 1); target.Health = 3;
            Reject(g, () => g.ResolveTotem(1, true)); target.Hand.Add(CardCatalog.PlumberStar.Id); target.Hand.Add(CardCatalog.LoadError.Id);
            // Star's defense window precedes the Tótem decision.
            Hit(g, rng, 5); Check(g.PassDefense(1).Success && g.State.Phase == Phase.TotemChoice);
            Reject(g, () => g.ResolveTotem(0, true)); Reject(g, () => g.Advance(0)); Reject(g, () => g.PlayCode(1, 1));
            Check(!g.CanPlayLoadError(1) && !DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.Totem));
            Check(g.ResolveTotem(1, true).Success); Reject(g, () => g.ResolveTotem(1, true));
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom();
        var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        g.State.Phase = Phase.Combat; g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        foreach (var p in g.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
        return (g, rng);
    }
    static void Stack(PlayerState p, int count) { p.Equipment = CardCatalog.Totem.Id; p.EquipmentStackedCopies = count - 1; }
    static void Hit(GameEngine g, ScriptedRandom rng, int die)
    {
        rng.EnqueueDie(die); Check(g.Attack(g.State.CurrentPlayer, 0, 0).Success);
        if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(g.State.CurrentPlayer, false).Success);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count +
        (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0) + (p.Bonus == null ? 0 : 1) + p.Exchange.Count + p.AttachedCards.Count);
    static void Reject(GameEngine g, Func<CommandResult> action) { var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot()); }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Tótem de Salvación incorrecta."); }
}
