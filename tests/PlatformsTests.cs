using Relick.Core;

static class PlatformsTests
{
    private static readonly string Area = CardCatalog.Platforms.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Error = CardCatalog.LoadError.Id;
    private static readonly string Flute = CardCatalog.GlassFlute.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Escenario Plataformas: orienta el Área desde la mano y aplica cada lado en el turno correspondiente", () =>
        {
            foreach (var sideA in new[] { 0, 1 })
            {
                var game = Fixture();
                game.State.Phase = Phase.Actions;
                Check(game.ActivateArea(0, 0).Success && game.State.PendingDecision?.Source == Area &&
                    game.AvailableActions(0) == 1 && game.AvailableActions(1) == 2, "Debe pagar una acción antes de orientar.");
                Check(game.DecisionChoices(1).Count == 0 && !game.ResolveDecision(1, sideA).Success,
                    "La orientación pertenece al jugador que coloca el Área.");

                game = GameEngine.FromSnapshot(game.SaveSnapshot(), new ScriptedRandom());
                Check(game.ResolveDecision(0, sideA).Success && game.State.Players[0].AreaSideA == sideA &&
                    game.State.Players[0].Bonus == Attack, "Debe conservar el Bonus y aplicar el lado elegido al reconectar.");
                Check(game.AvailableActions(0) == (sideA == 0 ? 2 : 1) && game.AvailableActions(1) == (sideA == 0 ? 0 : 2),
                    "El turno inicial debe respetar la orientación.");
                Check(game.Advance(0).Success && game.State.CurrentPlayer == 1 &&
                    game.AvailableActions(0) == (sideA == 0 ? 2 : 0) && game.AvailableActions(1) == (sideA == 0 ? 2 : 3),
                    "El cambio de turno debe actualizar ambas asignaciones.");
            }
        }),
        ("Escenario Plataformas: desde el Bonus inicial concede una tercera acción y bloquea Error de Carga", () =>
        {
            var game = Fixture(initialArea: true);
            Check(game.State.PendingDecision?.Source == Area && game.ResolveDecision(0, 0).Success,
                "El Área del Bonus inicial debe pedir orientación.");
            var snapshot = DuelSnapshot.For(game, 1, 1);
            Check(snapshot.Players[0].Actions == 3 && snapshot.Players[1].Actions == 0,
                "El contador público debe mostrar las acciones utilizables.");
            Check(game.Advance(0).Success && game.State.Phase == Phase.Actions, "Debe entrar a la etapa de acciones.");
            for (var remaining = 2; remaining >= 0; remaining--)
                Check(game.Equip(0, 0).Success && game.State.Phase == Phase.Actions && game.AvailableActions(0) == remaining,
                    "Debe permitir gastar cada acción sin abrir una respuesta de Error de Carga para el lado B.");
            Check(!game.Equip(0, 0).Success && !game.PlayCode(1, 0).Success && game.State.Players[1].Hand[0] == Error,
                "Una cuarta acción y un COMBO del rival deben rechazarse.");
        }),
        ("Escenario Plataformas: los efectos se suspenden con Zahira y terminan al salir del Área", () =>
        {
            var game = Fixture(initialArea: true);
            Check(game.ResolveDecision(0, 1).Success && game.AvailableActions(0) == 2, "El lado B conserva acciones durante su turno.");
            Check(game.Advance(0).Success && game.Advance(0).Success && game.State.CurrentPlayer == 1,
                "Debe comenzar el turno del lado A.");
            var owner = game.State.Players[0];
            owner.ZahiraHost = new(0, FieldZone.Area, -1, Area);
            Check(game.AvailableActions(0) == 2 && game.AvailableActions(1) == 2, "Zahira debe suspender los dos lados.");
            owner.ZahiraHost = null;
            Check(game.AvailableActions(0) == 0 && game.AvailableActions(1) == 3, "Ambos efectos deben reanudarse.");
            Check(game.Advance(1).Success && game.PlayCode(1, 1).Success && game.State.PendingDecision?.Source == Flute,
                "El jugador debe poder apuntar a un Área con Flauta de Vidrio.");
            var target = DuelOptions.For(game, 1).Single(option => option.Field is { Zone: FieldZone.Area, CardId: var cardId } && cardId == Area);
            Check(target.Move.Apply(game, 1).Success && owner.Area == null && owner.Hand.Contains(Area) &&
                game.AvailableActions(0) == 2 && game.AvailableActions(1) == 1, "Al volver a la mano, ambos efectos deben cesar.");
        })
    ];

    private static GameEngine Fixture(bool initialArea = false)
    {
        var equipment = Enumerable.Repeat(CardCatalog.Bonfire.Id, 4);
        return GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [initialArea ? equipment.ToList() : [Area], [Error, Flute]],
            Decks = [Enumerable.Repeat(Attack, 8).ToList(), Enumerable.Repeat(Attack, 8).ToList()],
            Bonuses = [initialArea ? Area : Attack, Attack], StartingSeat = 0
        }, new ScriptedRandom());
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
