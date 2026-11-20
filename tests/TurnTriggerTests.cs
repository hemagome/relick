using Relick.Core;

/// <summary>Order and persistence of the cards that act at a moment of the turn (docs/CARD_TRIGGERS.md).</summary>
static class TurnTriggerTests
{
    private static readonly string Dog = CardCatalog.GhostDog.Id;
    private static readonly string Robotize = CardCatalog.Robotize.Id;
    private static readonly string Ellie = CardCatalog.Ellie.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Disparadores: cada momento del turno reúne sus cartas en el orden de las reglas", () =>
        {
            string[] Ids(TurnMoment moment) => CardEffects.TurnTriggers(moment).Select(t => t.CardId).ToArray();
            Check(Ids(TurnMoment.TurnStart).SequenceEqual([CardCatalog.MouseBike.Id]), "Inicio de turno: Moto-Ratón.");
            Check(Ids(TurnMoment.ActionStart).SequenceEqual([Dog, Robotize]), "Inicio de Acciones: Perro Fantasma y después Robotizar.");
            Check(Ids(TurnMoment.FinalStage).SequenceEqual([CardCatalog.ZahiraFlair.Id]), "Etapa final: Zahira Flair.");
        }),
        ("Disparadores: Robotizar es la carta que resta daño en su paso de la cadena", () =>
        {
            Check(CardEffects.DamageTriggers(DamageStep.Reduce).Select(t => t.CardId).SequenceEqual([Robotize]), "Paso Reduce: Robotizar.");
            Check(Enum.GetValues<DamageStep>().SequenceEqual([DamageStep.Amplify, DamageStep.Prevent, DamageStep.Guard, DamageStep.Respond,
                DamageStep.Reduce, DamageStep.Absorb, DamageStep.Shield, DamageStep.Counters]), "El orden de los pasos es el de las reglas.");
        }),
        ("Disparadores: al salir una carta del campo, Escudo Galáctico responde antes que Tótem de Oro", () =>
        {
            Check(CardEffects.LeaveReplacements.Select(r => r.CardId).SequenceEqual([CardCatalog.GalacticShield.Id, CardCatalog.GoldenTotem.Id]),
                "Orden de las sustituciones: Escudo Galáctico y después Tótem de Oro.");
        }),
        ("Disparadores: al abrir Acciones, Perro Fantasma se resuelve antes que Robotizar y la cadena termina limpia", () =>
        {
            var (game, random) = Fixture(Ellie, Attack, Attack);
            var p = game.State.Active;
            p.Hand.Add(Dog);
            p.AttachedCards.Add(Robotize);
            Check(game.RevealGhostDog(0, 0).Success, "Se revela Perro Fantasma.");
            game.State.ExtraActionStages = 1;
            Check(game.Advance(0).Success && game.State.PendingDecision?.Source == Dog && game.State.ActionStartActor == 0,
                "Perro Fantasma pregunta primero, con la cadena de inicio de Acciones abierta.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Pick(game, 0, choice => choice.Value == 1);
            Check(game.State.PendingDecision?.Source == Robotize && game.State.ActionStartActor == null,
                "Al terminar Perro Fantasma, Robotizar muestra su carta y la marca de reentrada ya no hace falta.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Pick(game, 0, choice => choice.Value == 1);
            p = game.State.Active;
            Check(game.State.Phase == Phase.Actions && game.State.PendingDecision == null && p.Hand.SequenceEqual([Ellie]) &&
                p.Memory.SequenceEqual([Dog]), "Robotizar termina y el jugador vuelve a sus acciones.");
            var log = game.State.Log;
            var dog = log.FindLastIndex(line => line.Contains("Perro Fantasma se resuelve"));
            var robot = log.FindLastIndex(line => line.Contains("con Robotizar"));
            Check(dog >= 0 && robot > dog, "El registro conserva el orden: Perro Fantasma y después Robotizar.");
        }),
        ("Disparadores: un duelo guardado con la continuación anterior de Robotizar sigue su cadena en el módulo", () =>
        {
            var (game, random) = Fixture(Ellie, Ellie, Attack, Attack);
            var p = game.State.Active;
            p.AttachedCards.AddRange([Robotize, Robotize]);
            game.State.Phase = Phase.Combat;
            Check(game.Advance(0).Success && game.State.PendingDecision is { Continuation.Count: 1 } decision && decision.Source == Robotize,
                "La primera copia pregunta y queda una pendiente.");
            // The shape the engine saved before Robotizar had a module.
            var legacy = game.State.PendingDecision!.Continuation!;
            legacy.Kind = ContinuationKind.RobotizeActionStart; legacy.Text = ""; legacy.Step = null;
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Pick(game, 0, choice => choice.Value == 1);
            Check(game.State.PendingDecision?.Source == Robotize, "La segunda copia pregunta tras reanudar la continuación anterior.");
            Pick(game, 0, choice => choice.Value != 1);
            p = game.State.Active;
            Check(game.State.Phase == Phase.Actions && game.State.PendingDecision == null &&
                p.Hand.SequenceEqual([Ellie]) && p.Exchange.SequenceEqual([Ellie]), "Ambas copias resuelven su carta.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(params string[] deck)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[], []], Decks = [deck.ToList(), [Attack, Attack]],
            Bonuses = [CardCatalog.Senshuni.Id, null], StartingSeat = 0
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
        if (index < 0) throw new Exception($"No se encontró la opción en {game.State.Phase}: {string.Join(", ", choices.Select(c => c.Label))}.");
        Check(game.ResolveDecision(actor, index).Success, "Se aplica la opción.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
