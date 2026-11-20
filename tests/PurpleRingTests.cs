using Relick.Core;

static class PurpleRingTests
{
    private static readonly string Ring = CardCatalog.PurpleRing.Id;
    private static readonly string Helmet = CardCatalog.DiamondHelmet.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Anillo: dos equipos ligeros conviven con el normal; ganar 500 PR no gasta una acción adicional", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active;
            p.Equipment = Helmet; p.Hand.AddRange([Ring, Ring]);
            Check(g.Equip(0, 0).Success && g.Equip(0, 0).Success && p.ActionsLeft == 0, "Equipar cuesta una acción por copia.");
            Check(p.LightEquipment?.Count == 2 && p.Equipment == Helmet && p.Memory.Count == 0, "Los ligeros comparten campo con el equipo normal.");
            var option = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.UsePurpleRing && o.Move.Index == 1);
            Check(option.Field is { Zone: FieldZone.LightEquipment, Index: 1 } && !g.UsePurpleRing(1, 0).Success, "El uso solo se ofrece al jugador de turno y a su copia.");
            Check(option.Move.Apply(g, 0).Success && p.Relikia == 500 && p.ActionsLeft == 0 && p.LightEquipment?.Count == 1 && p.Memory.Single() == Ring,
                "El coste retira solo la copia elegida.");
            p.ActionsLeft = 1; p.Hand.Add(CardCatalog.Pendant.Id);
            Check(g.Equip(0, 0).Success && p.Equipment == CardCatalog.Pendant.Id && p.Memory.Contains(Helmet) && p.LightEquipment?.Count == 1,
                "Reemplazar el equipo normal no cambia los ligeros.");
        }),
        ("Anillo: Error de Carga anula la instalación y devuelve la acción", () =>
        {
            var (g, _) = Fixture();
            g.State.Active.Hand.Add(Ring); g.State.Active.Equipment = Helmet;
            g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.Equip(0, 0).Success && g.State.Phase == Phase.ErrorResponse && g.PlayCode(1, 0).Success, "Se puede responder a la instalación.");
            Check(g.State.Active.Equipment == Helmet && g.State.Active.LightEquipment == null &&
                g.State.Active.Memory.Single() == Ring && g.State.ActionsLeft == 2, "Anular conserva el equipo y devuelve la acción.");
        }),
        ("Anillo: una elección guardada reduce 5 a 2, conserva el premio del ataque y mantiene el dado del impacto", () =>
        {
            var (g, rng) = Fixture();
            g.State.Players[1].LightEquipment = [new(Ring)];
            Attack(g, rng, 6);
            Check(g.State.DecisionPlayer == 1 && g.State.Players[1].Health == 8 && g.State.Phase == Phase.Decision,
                "El daño espera antes de aplicarse.");
            Check(!DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.Choose), "La decisión es del defensor.");
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            var visuals = DuelVisualEvents.Capture(g);
            Choose(g, 1, 0, "Recibir 2 de daño");
            Check(g.State.Players[1].Health == 6 && g.State.Players[1].Memory.Single() == Ring && g.State.Active.Relikia == CardCatalog.Destiny.Reward,
                "La mitad impar es 2 y el ataque conserva su premio.");
            Check(visuals.Single(v => v.Kind == DuelVisualKind.Hit) is { Value: 2, AttackDie: 6 }, "El golpe usa el daño efectivo y el dado final.");
        }),
        ("Anillo: frente a daño puedes conservarlo o tomar los 500 PR y recibir el daño entero", () =>
        {
            foreach (var points in new[] { false, true })
            {
                var (g, rng) = Fixture();
                var defender = g.State.Players[1];
                defender.LightEquipment = [new(Ring)];
                Attack(g, rng, 5);
                if (points) Choose(g, 1, 0, "Ganar 500 PR");
                else Check(g.ResolveDecision(1, 0).Success, "El defensor puede conservar su Anillo.");
                Check(defender.Health == 5 && defender.Relikia == (points ? 500 : 0) &&
                    (defender.LightEquipment?.Count ?? 0) == (points ? 0 : 1), "La elección de PR no cambia el daño.");
            }
        }),
        ("Anillo: Reverse paga la destrucción, cada copia responde una vez por golpe y el Tótem ve el daño final", () =>
        {
            var (g, rng) = Fixture();
            var p = g.State.Players[1];
            p.Health = 2; p.Equipment = CardCatalog.Totem.Id;
            p.LightEquipment = [new(Ring), new(Ring)];
            p.Skills.Add(CardCatalog.Reverse.Id);
            Attack(g, rng, 6);
            Choose(g, 1, 1, "Recibir 2 de daño");
            Check(g.State.Phase == Phase.DestructionResponse && g.ResolveDestructionProtection(1, 0).Success,
                "Reverse puede pagar la destrucción.");
            Check(g.DecisionChoices(1).Where(c => c.Field != null).All(c => c.Field!.Index == 0), "La copia protegida ya respondió a este golpe.");
            Choose(g, 1, 0, "Recibir 1 de daño");
            Check(p.Health == 1 && p.Equipment == CardCatalog.Totem.Id && p.LightEquipment?.Count == 1 &&
                p.Memory.SequenceEqual([CardCatalog.Reverse.Id, Ring]) && g.State.Phase == Phase.Combat, "El Tótem recibe el daño ya reducido.");
        }),
        ("Anillo: un duelo guardado con la continuación anterior del golpe lo termina desde el módulo", () =>
        {
            var (g, rng) = Fixture();
            var p = g.State.Players[1];
            p.LightEquipment = [new(Ring)];
            p.Skills.Add(CardCatalog.Reverse.Id);
            Attack(g, rng, 6);
            Choose(g, 1, 0, "Recibir 2 de daño");
            Check(g.State.Phase == Phase.DestructionResponse && g.State.PendingDestruction!.Completed is { Step: PurpleRingEffect.Destroyed },
                "La destrucción del Anillo espera la respuesta de Reverse.");
            // The shape the engine saved before Anillo Morado's damage moved to its module.
            var legacy = g.State.PendingDestruction!.Completed!;
            legacy.Kind = ContinuationKind.PurpleRingDamage; legacy.Text = ""; legacy.Step = null;
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Players[1];
            Check(g.ResolveDestructionProtection(1, -1).Success && p.Health == 6 && p.LightEquipment == null && p.Memory.Single() == Ring,
                "El Anillo se destruye y el defensor recibe la mitad.");
        }),
        ("Anillo: también reduce daño de efectos y deja que se resuelva el robo posterior", () =>
        {
            var (g, rng) = Fixture();
            var p = g.State.Active;
            g.State.Phase = Phase.Combat;
            p.Bonus = CardCatalog.Jessica.Id;
            g.State.Players[1].LightEquipment = [new(Ring)];
            rng.EnqueueDie(6);
            Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Phase == Phase.Decision && p.Hand.Count == 0,
                "El efecto espera antes del robo.");
            Choose(g, 1, 0, "Recibir 1 de daño");
            Check(g.State.Players[1].Health == 7 && p.Hand.Count == 1 && g.State.Phase == Phase.Combat, "Reduce el daño de Jessica y finaliza su efecto.");
        }),
        ("Anillo: Bomba y Huevo pueden apuntar a una copia; al tomarla conserva al dueño y las marcas", () =>
        {
            var (g, _) = Fixture();
            var p = g.State.Active; var rival = g.State.Players[1];
            p.Equipment = Helmet; rival.Equipment = Helmet;
            p.LightEquipment = [new(Ring)];
            rival.LightEquipment = [new(Ring), new(Ring)];
            rival.MarkForCorruption(CardZone.LightEquipment, 0);
            p.Hand.Add(CardCatalog.TakeEquipment.Id);
            Check(g.PlayCode(0, 0).Success && g.DecisionChoices(0).Count(c => c.Field is { Zone: FieldZone.LightEquipment }) == 2,
                "Puede elegirse una copia rival.");
            Choose(g, 0, 0, "Tomar equipo");
            Check(p.Equipment == Helmet && rival.Equipment == Helmet && p.LightEquipment?.SequenceEqual([new(Ring), new(Ring, true)]) == true &&
                rival.LightEquipment?.Count == 1 && p.CorruptsOnMemory(CardZone.LightEquipment, 1), "El robo conserva la copia y las marcas.");
            p.Hand.Add(CardCatalog.TimeBomb.Id);
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 0, FieldZone.LightEquipment, 1).Success &&
                p.LightEquipment?.Single().Borrowed == false && rival.CorruptedMemory.Single() == Ring,
                "Bomba destruye el ligero ajeno, conserva el propio y la corrupción va al dueño.");
            p.ActionsLeft = 1; p.Hand.Add(CardCatalog.BlueEgg.Id);
            Check(g.PlayCode(0, 0).Success && g.ResolveDestruction(0, 1, FieldZone.LightEquipment, 0).Success &&
                rival.Memory.Single() == Ring, "Huevo destruye la otra copia.");
        }),
        ("Anillo: Misión Robo de Equipos equipa un ligero de su mazo junto al equipo normal", () =>
        {
            var (g, _) = Fixture();
            g.State.Active.Equipment = Helmet;
            g.State.Active.Hand.Add(CardCatalog.EquipmentRaid.Id);
            g.State.Active.Deck.Insert(0, Ring);
            Check(g.ActivateArea(0, 0).Success && g.ResolveDecision(0, 1).Success, "El jugador se asigna el lado B.");
            Check(g.State.PendingDecision == null &&
                g.State.Active.LightEquipment?.Single().CardId == Ring && g.State.Active.Equipment == Helmet, "El Área equipa el ligero desde el mazo.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var random = new ScriptedRandom();
        var attack = CardCatalog.Attack.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[], []], Decks = [[attack, attack, attack], [attack, attack]],
            Bonuses = [null, null]
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var p in game.State.Players) p.Dexterity = 0;
        return (game, random);
    }

    private static void Attack(GameEngine g, ScriptedRandom random, int die)
    {
        g.State.Phase = Phase.Combat;
        g.State.Active.AttackRerollUsedThisTurn = true;
        g.State.Active.Hand.Add(CardCatalog.Attack.Id);
        g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        random.EnqueueDie(die);
        Check(g.Attack(0, 0, 0).Success, "El ataque debe continuar hasta el Anillo.");
    }

    private static void Choose(GameEngine game, int actor, int slot, string button)
    {
        var choice = DuelOptions.For(game, actor).Single(o => o.Move.Kind == DuelMoveKind.Choose &&
            o.Field is { Zone: FieldZone.LightEquipment } field && field.Index == slot && o.Button == button);
        Check(choice.Move.Apply(game, actor).Success, "Debe aplicarse la elección autorizada.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
