using System.Text.Json;
using Relick.Core;

static class VictoriousManualControlTests
{
    private static readonly string Control = CardCatalog.VictoriousManualControl.Id;
    private static readonly string Bonus = CardCatalog.Senshuni.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Control Manual: mueve Hoguera desde Bonus y paga vida sin activar el equipo", () =>
        {
            var (game, _) = Fixture();
            var p = game.State.Active;
            p.Bonus = CardCatalog.Bonfire.Id;
            p.Health = 1;
            p.StarEffects = 1;
            var visuals = DuelVisualEvents.Capture(game);
            Check(game.PlayCode(0, 0).Success && p.Health == 1, "El pago se espera hasta resolver el movimiento.");
            Destination(game, "Equipo");
            Check(p.Equipment == CardCatalog.Bonfire.Id && p.Bonus == CardCatalog.Attack.Id && p.Deck.Count == 7,
                "La Hoguera queda en el equipo vacío y se repone el Bonus.");
            Check(p.Health == 0 && p.HasReachedZero && !game.State.IsOver && !p.EquipmentExpiresAtTurnStart &&
                p.ActionsLeft == 1 && visuals.All(cue => cue.Kind is not (DuelVisualKind.Hit or DuelVisualKind.Heal)),
                "Pagar la última vida por primera vez es válido y mover Hoguera no dispara su activación.");
        }),
        ("Control Manual: mueve una habilidad a una casilla libre y conserva sus contadores y la carta anexada", () =>
        {
            var (game, random) = Fixture();
            var p = game.State.Active;
            p.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id]);
            p.SkillPositions = new() { [1] = 3 };
            p.SkillCounters[0] = 2;
            p.MarkForCorruption(CardZone.Skill, 0);
            var zahira = new FieldTarget(0, FieldZone.Skill, 0, CardCatalog.Reverse.Id);
            game.State.Players[1].ZahiraHost = zahira;
            Check(game.PlayCode(0, 0).Success, "El Código puede mover una habilidad.");
            Source(game, FieldZone.Skill, 0);
            Check(!game.ResolveDecision(1, 0).Success && !game.DecisionChoices(0).Any(c => c.Label is "Habilidad 1" or "Habilidad 4"),
                "El rival no decide y las casillas ocupadas no son elegibles.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Destination(game, "Habilidad 3");
            p = game.State.Active;
            Check(p.SkillPosition(0) == 2 && p.SkillPosition(1) == 3 && p.CountersOnSkill(0) == 2 &&
                p.CorruptsOnMemory(CardZone.Skill, 0) && game.State.Players[1].ZahiraHost == zahira &&
                p.Relikia == 0 && !game.State.LearnedThisTurn, "Mover la misma copia conserva su estado y no equivale a enseñarla.");
        }),
        ("Control Manual: conserva el dueño, el dado y la marca de un equipo movido a canje y de vuelta", () =>
        {
            var (game, random) = Fixture();
            var p = game.State.Active;
            p.Equipment = CardCatalog.Pc.Id;
            p.EquipmentDie = 4;
            p.MarkForeignFieldCard(CardCatalog.Pc.Id);
            p.MarkForCorruption(CardZone.Equipment, 0);
            p.Hand.Add(Control);
            Check(game.PlayCode(0, 0).Success, "Se activa el primer Control.");
            Source(game, FieldZone.Equipment, 0);
            Check(p.Equipment == null && p.Exchange.Single() == CardCatalog.Pc.Id &&
                p.CorruptsOnMemory(CardZone.Exchange, 0) && p.MovedFieldCards?[0].Borrowed == true, "Se mueve la copia rival marcada al canje.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            var cues = DuelVisualEvents.Capture(game);
            Check(game.PlayCode(0, 0).Success, "Se activa el segundo Control.");
            Source(game, FieldZone.Exchange, 0);
            p = game.State.Active;
            Check(p.Equipment == CardCatalog.Pc.Id && p.EquipmentDie == 4 && p.Exchange.Count == 0 && p.Health == 6 &&
                p.ForeignFieldCards?.Single() == CardCatalog.Pc.Id && p.CorruptsOnMemory(CardZone.Equipment, 0) &&
                cues.All(cue => cue.Kind != DuelVisualKind.Die), "La copia vuelve con el mismo dado sin volver a tirarlo.");
        }),
        ("Control Manual: mueve Bonus al canje, conserva el uso de Jessica y completa canjes al reponer", () =>
        {
            foreach (var hasFirst in new[] { false, true })
            {
                var (game, _) = Fixture();
                var p = game.State.Active;
                p.Bonus = CardCatalog.Jessica.Id;
                p.BonusEffectUsed = true;
                p.BonusPlacedTurn = 1;
                if (hasFirst) p.Exchange.Add(CardCatalog.DiamondHelmet.Id);
                Check(game.PlayCode(0, 0).Success, "Se puede mover el Bonus.");
                if (hasFirst) Source(game, FieldZone.Bonus);
                Check(p.Health == 7, "Resuelto el único destino, se paga la vida.");
                if (hasFirst)
                    Check(p.Exchange.Count == 0 && p.Hand.Single() == CardCatalog.Attack.Id && p.Deck.Count == 6 &&
                        p.Memory.Contains(CardCatalog.Jessica.Id), "Tras reponer Bonus, el canje completo usa ambas cartas y vuelve a reponer.");
                else
                    Check(p.Exchange.Single() == CardCatalog.Jessica.Id && p.ExchangeEffectsUsed?.Single() == 0 &&
                        p.ExchangePlacementTurns?.GetValueOrDefault(0) == 1 && p.Deck.Count == 7, "Jessica conserva su turno y su uso.");
            }
        }),
        ("Control Manual: orienta un Área movida sin disparar sus efectos de entrada", () =>
        {
            var (game, _) = Fixture();
            var p = game.State.Active;
            p.Exchange.Add(CardCatalog.EquipmentRaid.Id);
            p.Memory.Add(CardCatalog.TakeEquipment.Id);
            game.State.Players[1].Memory.Add(CardCatalog.Attack.Id);
            Check(game.PlayCode(0, 0).Success, "Se puede mover el Área desde canje.");
            Source(game, FieldZone.Exchange, 0);
            Check(p.Area == CardCatalog.EquipmentRaid.Id && game.State.PendingDecision?.Source == CardCatalog.EquipmentRaid.Id,
                "Queda pendiente orientar el Área.");
            Check(game.ResolveDecision(0, 0).Success && p.AreaSideA == 0 && game.State.Phase == Phase.Actions &&
                p.AttachedCards.Count == 0 && p.Hand.Count == 0 && game.State.Players[1].Equipment == null,
                "Orientar no dispara la colocación de Ataque, búsqueda ni equipo de entrada.");
        }),
        ("Control Manual: un Bonus vacío puede recibir una carta del campo", () =>
        {
            var (game, _) = Fixture();
            var p = game.State.Active;
            p.Bonus = null;
            p.Exchange.Add(CardCatalog.Pc.Id);
            var visuals = DuelVisualEvents.Capture(game);
            Check(game.PlayCode(0, 0).Success, "Un Bonus libre admite un traslado.");
            Destination(game, "Bonus");
            Check(p.Bonus == CardCatalog.Pc.Id && p.Exchange.Count == 0 && p.Deck.Count == 8 && p.Health == 7 &&
                visuals.All(cue => cue.Kind != DuelVisualKind.BonusPlace), "El Bonus se ocupa con la carta movida desde el campo.");
        }),
        ("Control Manual: el Escudo conserva sus cartas ocultas en canje y salen al destruirlo", () =>
        {
            var (game, random) = Fixture();
            var p = game.State.Active;
            var shield = CardCatalog.GalacticShield.Id;
            var hidden = CardCatalog.QuickLoad.Id;
            p.GalacticShields = [new() { Cards = [new(hidden, false)] }];
            Check(game.PlayCode(0, 0).Success, "El Escudo puede moverse con sus cartas anexadas.");
            Source(game, FieldZone.GalacticShield, 0);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            p = game.State.Active;
            var view = DuelSnapshot.For(game, 1, 2);
            Check(p.GalacticShields == null && p.Exchange.Single() == shield &&
                view.Players[0].MovedShieldCharges?[0] == 1 &&
                !JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot).Contains(hidden), "La vista solo muestra cuántas cartas quedan.");
            game.State.CurrentPlayer = 1;
            game.State.Active.Hand.Add(CardCatalog.BlueEgg.Id);
            Check(game.PlayCode(1, 0).Success && game.ResolveDestruction(1, 0, FieldZone.Exchange, 0).Success &&
                p.Memory.Contains(hidden) && p.Memory.Contains(shield) && p.MovedFieldCards == null, "La carta y su anexo llegan a memoria.");
        }),
        ("Control Manual: Error y Caja anulan antes del pago, y a 0 vidas no se puede activar", () =>
        {
            foreach (var useBox in new[] { false, true })
            {
                var (game, _) = Fixture();
                var p = game.State.Active;
                var rival = game.State.Players[1];
                if (useBox) rival.Equipment = CardCatalog.CardboardBox.Id;
                else rival.Hand.Add(CardCatalog.LoadError.Id);
                Check(game.PlayCode(0, 0).Success && (useBox ? game.ResolveCodeResponse(1, true) : game.PlayCode(1, 0)).Success &&
                    p.Health == 8 && p.ActionsLeft == 2 && p.Bonus == Bonus && p.Exchange.Count == 0, "Anular evita el pago y el traslado.");
                p.Hand.Add(Control);
                p.Health = 0;
                p.Relikia = 400;
                Check(!game.PlayCode(0, 0).Success && !game.CanReplayMemoryCode(0, 0), "El coste requiere al menos 1 vida.");
            }
        }),
        ("Control Manual: Relikia aplica el mismo movimiento y respeta el espacio ocupado", () =>
        {
            var (game, _) = Fixture();
            var p = game.State.Active;
            p.Hand.Clear();
            p.Memory.Add(Control);
            p.Relikia = 400;
            p.Bonus = CardCatalog.Pendant.Id;
            p.Equipment = CardCatalog.Pc.Id;
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success, "El Código admite Relikia.");
            Source(game, FieldZone.Bonus);
            Check(p.Equipment == CardCatalog.Pc.Id && p.Exchange.Single() == CardCatalog.Pendant.Id && p.Health == 7 &&
                p.Relikia == 0 && p.ActionsLeft == 0 && p.CorruptedMemory.Single() == Control, "Relikia corrompe la copia y el equipo ocupado queda intacto.");
        }),
        ("Control Manual: pagar y volver a 0 vidas termina el duelo antes del traslado", () =>
        {
            var (game, _) = Fixture();
            var p = game.State.Active;
            p.Health = 1;
            p.HasReachedZero = true;
            Check(game.PlayCode(0, 0).Success && game.State.Winner == 1 && p.Health == 0 &&
                p.Bonus == Bonus && p.Exchange.Count == 0, "Al pagar se aplica la regla de volver a 0.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture()
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[Control], []],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 8).ToList(), [CardCatalog.Attack.Id]],
            Bonuses = [Bonus, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        return (game, random);
    }

    private static void Source(GameEngine game, FieldZone zone, int slot = -1)
    {
        var option = DuelOptions.For(game, 0).Single(o => o.Field is { } field && field.Zone == zone && field.Index == slot);
        Check(option.Move.Apply(game, 0).Success, "El origen debe ser seleccionable.");
    }

    private static void Destination(GameEngine game, string name)
    {
        var option = DuelOptions.For(game, 0).Single(o => o.Move.Kind == DuelMoveKind.Choose && o.Label == name);
        Check(option.Move.Apply(game, 0).Success, "El destino debe ser seleccionable.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
