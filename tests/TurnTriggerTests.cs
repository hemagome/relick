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
