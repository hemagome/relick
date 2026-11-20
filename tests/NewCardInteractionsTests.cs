using Relick.Core;

static class NewCardInteractionsTests
{
    private static readonly string Tome = CardCatalog.MagicTome.Id;
    private static readonly string Helmet = CardCatalog.CopyHelmet.Id;
    private static readonly string Flute = CardCatalog.GlassFlute.Id;
    private static readonly string Dog = CardCatalog.GhostDog.Id;
    private static readonly string Ghost = CardCatalog.ThiefGhost.Id;
    private static readonly string Bike = CardCatalog.MouseBike.Id;
    private static readonly string Era = CardCatalog.FutureEra.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Life = CardCatalog.ExtraLife.Id;
    private static readonly string Shield = CardCatalog.GalacticShield.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Tomo de Magia: la carta usada se invierte y deja de estar disponible", () =>
        {
            var (g, _) = Fixture([Tome], [], [CardCatalog.Senshuni.Id, Bike, Attack, Attack, Attack]);
            Check(g.PlayCode(0, 0).Success);
            var use = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.PlayCode);
            Check(use.Field is { Zone: FieldZone.TomeCard } && use.CardId == CardCatalog.Senshuni.Id);
            Check(use.Move.Apply(g, 0).Success);
            Check(g.State.Active.Memory.Count == 0 && g.State.Active.Hand.Count == 2);
            Check(g.State.TomeCards!.Single(c => c.CardId == CardCatalog.Senshuni.Id).Inverted);
            Check(!use.Move.Apply(g, 0).Success);
            Check(DuelSnapshot.For(g, 1, 1).Players[0].TomeCards!.Any(c => c.Inverted));
        }),
        ("Tomo de Magia: se puede equipar una carta anexada por el procedimiento normal", () =>
        {
            var (g, _) = Fixture([Tome], [], [Bike, Attack, Attack]);
            Check(g.PlayCode(0, 0).Success);
            var equip = DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.Equip);
            Check(equip.Move.Apply(g, 0).Success && g.State.Active.Equipment == Bike);
            Check(g.State.TomeCards!.Single().CardId == Attack && g.State.Active.Memory.Count == 0);
        }),
        ("Tomo de Magia: los anexos enderezados pagan descartes de Destiny y se invierten", () =>
        {
            var (g, _) = Fixture([Tome, Attack], [], [Life, Dog, Attack, Attack]);
            var p = g.State.Active;
            Check(g.PlayCode(0, 0).Success);
            p.Skills.Add(CardCatalog.Destiny.Id);
            g.State.CurrentPlayer = 1;
            g.State.PendingAttack = new(1, CardCatalog.Destiny.Id, 3, false);
            g.State.Phase = Phase.DefenseChoice;
            var publicBoard = DuelSnapshot.For(g, -1, 1);
            var ownView = DuelSnapshot.For(g, 0, 1);
            Check(publicBoard.Players[0].TomeCards!.All(child => !child.HandIndex.HasValue));
            var combined = DuelPackets.Combine(new DuelBoardPacket { Board = publicBoard }, new DuelPrivateData
            {
                MySeat = 0, OwnHand = ownView.Players[0].Hand,
                TomeHandIndices = ownView.Players[0].TomeCards!.ToDictionary(child => child.Slot, child => child.HandIndex!.Value)
            }, ownView.Options);
            var defense = combined.Options.Single(o => o.Move.Kind == DuelMoveKind.DestinyDefense);
            defense.Move.Discards = [0, .. combined.Players[0].TomeCards!.Select(child => child.HandIndex!.Value)];
            Check(defense.Move.Apply(g, 0).Success);
            Check(p.Hand.Count == 0 && p.Memory.SequenceEqual([Attack]) && g.State.TomeCards!.All(child => child.Inverted));
        }),
        ("Casco de Copias: un clon temporal sale de RAM antes de la carta original y cuesta una sola acción", () =>
        {
            var (g, random) = Fixture([Life], [Helmet]);
            g.State.Players[0].Health = 2;
            g.State.Players[1].Health = 2;
            random.EnqueueDie(2);
            random.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Check(g.PlayCode(1, 0).Success);
            Check(g.State.Players[0].Health == 6 && g.State.Players[1].Health == 4);
            Check(g.State.Players[1].Memory.SequenceEqual([Helmet]) && g.State.Players[1].ActionsLeft == 1);
        }),
        ("Casco de Copias: cada respuesta consecutiva consume un Casco de la mano", () =>
        {
            foreach (var secondHelmet in new[] { false, true })
            {
                var (g, random) = Fixture([Life, Flute], [Helmet, secondHelmet ? Helmet : Attack]);
                g.State.Players[0].Equipment = Bike;
                g.State.Players[1].Equipment = CardCatalog.DiamondHelmet.Id;
                random.EnqueueDie(2);
                random.EnqueueDie(2);

                Check(g.PlayCode(0, 0).Success);
                var first = DuelSnapshot.For(g, 1, 1).Options.Single(option =>
                    option.Move is { Kind: DuelMoveKind.PlayCode, Index: 0 });
                Check(first.Move.Apply(g, 1).Success);
                if (secondHelmet)
                    Check(g.State.Phase == Phase.ErrorResponse && g.State.ErrorAfterDie && g.PassErrorResponse(1).Success);
                Check(g.State.Phase == Phase.Actions && g.State.Players[1].Memory.SequenceEqual([Helmet]) &&
                    g.State.Players[1].ActionsLeft == 1);

                g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
                Check(g.PlayCode(0, 0).Success);
                var second = DuelSnapshot.For(g, 1, 2).Options.SingleOrDefault(option => option.Move.Kind == DuelMoveKind.PlayCode);
                if (!secondHelmet)
                {
                    Check(second == null && !first.Move.Apply(g, 1).Success);
                    Check(g.State.DecisionPlayer == 0 && g.State.Players[1].Memory.SequenceEqual([Helmet]));
                    continue;
                }

                Check(second != null && second.Move.Apply(g, 1).Success);
                Pick(g, 1, choice => choice.Field is { Owner: 1, Zone: FieldZone.Equipment });
                var player = g.State.Players[1];
                Check(player.Memory.SequenceEqual([Helmet, Helmet]) && player.ActionsLeft == 0);
                Check(player.Hand.SequenceEqual([CardCatalog.DiamondHelmet.Id]) && g.State.Phase == Phase.Actions);
            }
        }),
        ("Casco de Copias: el clon de Tomo conserva sus anexos en canje y recupera su identidad al salir", () =>
        {
            var move = CardCatalog.VictoriousManualControl.Id;
            var (g, random) = Fixture([Tome, Flute], [Helmet, move], [Bike, Attack, Attack, Attack]);
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            var p = g.State.Players[1];
            Check(CardCatalog.Get(p.AttachedCards.Single()).Id == Tome && p.Memory.Count == 0);
            g.State.CurrentPlayer = 1;
            Check(g.PlayCode(1, 0).Success);
            Pick(g, 1, choice => choice.Field?.Zone == FieldZone.Attached);
            Pick(g, 1, choice => choice.Label == "Canje 1");
            var view = DuelSnapshot.For(g, 0, 1).Players[1];
            Check(view.Exchange.Single() == Tome && view.TomeCards!.All(child => child.HostZone == FieldZone.Exchange));
            var children = view.TomeCards!.Select(child => child.CardId).ToArray();
            g.State.CurrentPlayer = 0;
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Check(g.PlayCode(0, 0).Success);
            Check(g.State.DecisionPlayer == 0 && DuelSnapshot.For(g, 1, 1).Options.All(option => option.Move.Kind != DuelMoveKind.PlayCode));
            Pick(g, 0, choice => choice.Field is { Owner: 1, Zone: FieldZone.Exchange });
            Check(g.State.Players[1].Hand.SequenceEqual(new[] { Helmet }.Concat(children)));
            Check(g.State.Players[1].Exchange.Count == 0 && g.State.Players[1].Memory.SequenceEqual([move]));
        }),
        ("Casco de Copias: Error de Carga cancela el eslabón del clon y mantiene el original", () =>
        {
            var (g, random) = Fixture([Life, CardCatalog.LoadError.Id], [Helmet]);
            g.State.Players[0].Health = 2;
            g.State.Players[1].Health = 2;
            random.EnqueueDie(2);
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(g.State.ActivationChain.Count == 2 && g.State.ErrorResponder == 0);
            Check(g.PlayCode(0, 0).Success);
            Check(g.State.Players[1].Memory.SequenceEqual([Helmet]) && g.State.Players[1].ActionsLeft == 2);
            Check(g.State.Players[0].Health == 4 && g.State.Players[1].Health == 2);
        }),
        ("Casco de Copias: guarda la resolución del clon mientras el original espera en RAM", () =>
        {
            var (g, random) = Fixture([Flute], [Helmet]);
            g.State.Players[0].Equipment = Bike;
            g.State.Players[1].Equipment = CardCatalog.Pendant.Id;
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success && g.State.DecisionPlayer == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Pick(g, 1, choice => choice.Field is { Owner: 1, Zone: FieldZone.Equipment });
            Check(g.State.Players[1].Memory.SequenceEqual([Helmet]));
            Check(g.State.Players[0].Hand.SequenceEqual([Bike]) && g.State.Players[1].Hand.SequenceEqual([CardCatalog.Pendant.Id]));
            Check(g.State.Phase == Phase.Actions);
        }),
        ("Casco de Copias: la copia puede tomar el único objetivo y la Flauta original termina sin bloquear la RAM", () =>
        {
            var (g, _) = Fixture([Flute], [Helmet]);
            g.State.Players[0].Equipment = Bike;
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success);
            Check(g.State.Phase == Phase.Actions && g.State.Players[0].Hand.SequenceEqual([Bike]));
            Check(g.State.Players[0].Memory.SequenceEqual([Flute]) && g.State.Players[1].Memory.SequenceEqual([Helmet]));
        }),
        ("Casco de Copias: puede responder tras el dado y conserva el resultado original al reanudar", () =>
        {
            var (g, random) = Fixture([Life], [Helmet]);
            g.State.Players[0].Health = 2;
            g.State.Players[1].Health = 2;
            random.EnqueueDie(2);
            random.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && g.PassErrorResponse(1).Success && g.State.ErrorAfterDie);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Actions);
            Check(g.State.Players[0].Health == 4 && g.State.Players[1].Health == 6);
        }),
        ("Casco de Copias: clona Estrella del Fontanero y cada jugador paga su descarte por separado", () =>
        {
            var (g, _) = Fixture([CardCatalog.PlumberStar.Id, Attack], [Helmet, Attack]);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.TurnBuffDiscard && g.State.DecisionPlayer == 1);
            Check(g.ResolveTurnBuffDiscard(1, 0).Success && g.State.Phase == Phase.TurnBuffDiscard && g.State.DecisionPlayer == 0);
            Check(g.ResolveTurnBuffDiscard(0, 0).Success && g.State.Phase == Phase.Actions);
            Check(g.State.Players.All(player => player.StarEffects == 1 && player.ActionsLeft == 1));
        }),
        ("Mover por efecto: colocar Fantasma Ladrón en Equipo desde canje no revela la mano", () =>
        {
            var (g, _) = Fixture([CardCatalog.VictoriousManualControl.Id, Flute], []);
            g.State.Active.Exchange.Add(Ghost);
            Check(g.PlayCode(0, 0).Success);
            Pick(g, 0, choice => choice.Label == "Equipo");
            Check(g.State.Phase == Phase.Actions && g.State.Active.Equipment == Ghost && g.State.Active.Hand.Single() == Flute);
        }),
        ("Flauta de Vidrio: devuelve un Equipo al dueño y no activa protecciones de destrucción", () =>
        {
            var (g, _) = Fixture([Ghost, Flute], [], [Attack, Attack, Attack]);
            Check(g.Equip(0, 0).Success);
            Pick(g, 1, _ => true);
            g.State.Players[1].Skills.Add(CardCatalog.Reverse.Id);
            Check(g.PlayCode(0, 0).Success);
            Pick(g, 0, choice => choice.Field?.Zone == FieldZone.Equipment);
            Check(g.State.Players[1].Equipment == null && g.State.Players[1].Skills.Count == 1);
            Check(g.State.Players[0].Hand.SequenceEqual([Ghost]) && g.State.Phase == Phase.Actions);
        }),
        ("Moto-Ratón: elige carta o acción antes del robo y la elección sobrevive a la reconexión", () =>
        {
            foreach (var draw in new[] { true, false })
            {
                var (g, random) = Fixture([], [Attack]);
                g.State.Players[1].Equipment = Bike;
                Check(g.Advance(0).Success && g.State.PendingDecision?.Source == Bike);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
                Pick(g, 1, choice => choice.Value == (draw ? 1 : 0));
                Check(g.State.Active.Hand.Count == (draw ? 3 : 2) && g.State.ActionsLeft == (draw ? 2 : 3));
            }
        }),
        ("Era Futurista: orienta el Área, activa Robotizar fuera del mazo y permite un único D10 por jugador", () =>
        {
            var (g, random) = Fixture([Era, Life], []);
            Check(g.ActivateArea(0, 0).Success);
            Pick(g, 0, choice => choice.Value == 1);
            Check(CardCatalog.Get(g.State.Players[1].AttachedCards.Single()).Id == CardCatalog.Robotize.Id);
            Check(g.PrepareD10(0, true).Success && g.PrepareD10(1, true).Success);
            random.EnqueueDie(9);
            Check(g.PlayCode(0, 0).Success && g.State.LastDie == 9 && !g.CanPrepareD10(0) && g.CanPrepareD10(1));
            Check(g.ConvertD10(0).Success && g.State.LastDie == 6);
        }),
        ("Era Futurista: el jugador puede ver el 9 y decidir si lo convierte después de tirar", () =>
        {
            var (g, random) = Fixture([Life], []);
            g.State.Active.Health = 2;
            g.State.Active.Area = Era;
            g.State.Active.AreaSideA = 1;
            Check(g.PrepareD10(0, true).Success);
            random.EnqueueDie(9);
            Check(g.PlayCode(0, 0).Success && g.State.LastDie == 9 && g.CanConvertD10(0));
            Check(DuelOptions.For(g, 0).Single(o => o.Move.Kind == DuelMoveKind.ConvertD10).Move.Apply(g, 0).Success);
            Check(g.State.LastDie == 6 && g.State.Active.Health == 6 && !g.State.Active.HasNaturalSix);
        }),
        ("Era Futurista: una tirada baja conserva el tipo de dado al enviarla, ajustar el valor y reconectar", () =>
        {
            var (g, random) = Fixture([Life], []);
            g.State.Active.Area = Era;
            g.State.Active.AreaSideA = 1;
            g.State.Active.Dexterity = 1;
            Check(g.PrepareD10(0, true).Success);
            var visuals = DuelVisualEvents.Capture(g);
            random.EnqueueDie(3);
            Check(g.PlayCode(0, 0).Success && visuals.Single(cue => cue.Kind == DuelVisualKind.Die) is { Value: 3, DieIsD10: true });
            Check(g.UseDexterity(0).Success && g.State.LastDie == 4);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Check(DuelSnapshot.For(g, 0, 1) is { Die: 4, DieIsD10: true });
        }),
        ("Fantasma Ladrón: fuerza un Objeto al equipar y al confirmar un dado propio bajo", () =>
        {
            var (g, random) = Fixture([Ghost], [Flute, Tome, Attack]);
            Check(g.Equip(0, 0).Success);
            Check(g.State.DecisionPlayer == 1 && DuelSnapshot.For(g, 0, 1).Players[1].Hand.All(id => id != null));
            Pick(g, 1, choice => choice.CardId == Tome);
            Check(g.State.Players[0].Hand.SequenceEqual([Tome]));
            g.State.CurrentPlayer = 1;
            g.State.Phase = Phase.Combat;
            g.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            random.EnqueueDie(2);
            Check(g.Attack(1, 1, 0).Success);
            if (g.State.Phase == Phase.AttackRerollChoice) Check(g.ResolveAttackReroll(1, false).Success);
            Check(g.State.PendingDecision?.Source == Ghost);
            Pick(g, 1, choice => choice.CardId == Flute);
            Check(g.State.Players[0].Hand.SequenceEqual([Tome, Flute]) && g.State.Active.Hand.Count == 0);
        }),
        ("Perro Fantasma: la revelación sigue a la copia, se activa gratis y bloquea la recuperada por el turno", () =>
        {
            var (g, random) = Fixture([Attack, Dog], []);
            g.State.Players[1].CorruptedMemory.Add(Flute);
            Check(g.RevealGhostDog(0, 1).Success);
            Check(DuelSnapshot.For(g, 1, 1).Players[0].Hand[1] == Dog);
            g.State.Active.RemoveHandCard(0);
            g.State.ExtraActionStages = 1;
            Check(g.Advance(0).Success);
            Check(g.State.PendingDecision?.Source == Dog && g.State.ActionsLeft == 2);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), random);
            Pick(g, 0, choice => choice.Value == 5);
            Check(g.State.Active.Hand.SequenceEqual([Flute]) && g.State.Active.IsForeignHandCard(0));
            Check(!g.PlayCode(0, 0).Success && !g.Exchange(0, 0).Success);
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(string[] first, string[] second, string[]? deck = null)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [first.ToList(), second.ToList()],
            Decks = [(deck ?? [Attack, Attack, Attack, Attack, Attack]).ToList(), [Attack, Attack, Attack, Attack, Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var player in game.State.Players) player.Dexterity = 0;
        return (game, random);
    }

    private static void Pick(GameEngine game, int actor, Func<DecisionChoice, bool> matches)
    {
        var choices = game.DecisionChoices(actor).ToList();
        var index = choices.FindIndex(choice => matches(choice));
        if (index < 0) throw new Exception($"No se encontró la opción en {game.State.Phase}. Opciones: {string.Join(", ", choices.Select(c => c.Label))}.");
        Check(new DuelMove { Kind = DuelMoveKind.Choose, Index = index }.Apply(game, actor).Success);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("No se cumple la interacción esperada.");
    }
}
