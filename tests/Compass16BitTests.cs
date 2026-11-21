using Relick.Core;

static class Compass16BitTests
{
    private static readonly string Compass = CardCatalog.Compass16Bit.Id;
    private static readonly string Area = CardCatalog.Platforms.Id;
    private static readonly string Bonus = CardCatalog.BlackHole.Id;
    private static readonly string Skill = CardCatalog.Game.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Brújula 16 Bits: baraja el Bonus y el Área y reanuda la elección privada tras reconectar", () =>
        {
            var game = Fixture([Skill, Attack, Attack]);
            var player = game.State.Active;
            player.Bonus = Bonus;
            player.Area = Area;
            player.AreaSideA = 0;
            player.InitialAreaWaitingInBonus = true;
            player.BonusEffectUsed = true;
            player.MarkForCorruption(CardZone.Bonus, -1);
            var cues = DuelVisualEvents.Capture(game);

            Check(!game.PlayCode(1, 0).Success && game.PlayCode(0, 0).Success, "El Código se juega en tu etapa de acciones.");
            Check(player.ActionsLeft == 1 && player.Memory.SequenceEqual([Compass]), "El Código Objeto debe costar una acción.");
            Check(player.Area == null && player.AreaSideA == null && player.Bonus == null && !player.BonusEffectUsed &&
                !player.InitialAreaWaitingInBonus && player.Deck.Order().SequenceEqual(new[] { Skill, Attack, Attack, Bonus, Area }.Order()),
                "Ambas cartas deben entrar al mazo antes de abrir la búsqueda.");
            Check(cues.Select(cue => cue.Kind).TakeLast(3).SequenceEqual([DuelVisualKind.Pile, DuelVisualKind.Pile, DuelVisualKind.Shuffle]),
                "Los dos traslados al mazo deben preceder al barajado.");

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), new ScriptedRandom());
            player = game.State.Active;
            Check(game.State.PendingDecision is { Source: var source, Kind: DecisionKind.ChooseCard, AllowsCombos: false } && source == Compass,
                "Debe recuperarse la búsqueda después del barajado.");
            Check(DuelSnapshot.For(game, 1, 1).Options.All(option => option.Move.Kind != DuelMoveKind.Choose) &&
                !game.ResolveDecision(1, 0).Success, "El rival no puede ver ni elegir las cartas del mazo.");
            var choices = DuelOptions.For(game, 0).Where(option => option.Move.Kind == DuelMoveKind.Choose).ToArray();
            Check(choices.Select(option => option.CardId).SequenceEqual(player.Deck.Order(StringComparer.Ordinal)) &&
                choices.Any(option => option.CardId == Skill), "La galería incluye habilidades y oculta el orden del mazo.");
            Check(choices.Single(option => option.CardId == Bonus).Move.Apply(game, 0).Success && player.Bonus == Bonus &&
                player.Area == null && !player.Deck.Contains(Bonus) && player.CorruptsOnMemory(CardZone.Bonus, -1) && game.State.Phase == Phase.Actions,
                "El Área elegida se coloca como Bonus y conserva su marca de corrupción.");
        }),
        ("Brújula 16 Bits: Error de Carga cancela antes de mover cartas y devuelve la acción", () =>
        {
            var game = Fixture([Skill, Attack]);
            var player = game.State.Active;
            player.Bonus = Attack;
            player.Area = Area;
            player.AreaSideA = 1;
            game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.ErrorResponse, "Falta la respuesta de Error de Carga.");
            Check(game.PlayCode(1, 0).Success && game.State.Phase == Phase.Actions && player.ActionsLeft == 2 &&
                player.Bonus == Attack && player.Area == Area && player.Deck.SequenceEqual([Skill, Attack]),
                "Cancelar deja las cartas donde estaban y devuelve el coste.");
        }),
        ("Brújula 16 Bits: devuelve los anexos y las cartas prestadas a los mazos de sus dueños", () =>
        {
            var game = Fixture([Attack, Attack]);
            var player = game.State.Active;
            var rival = game.State.Players[1];
            var host = $"{CardCatalog.MagicTome.Id}~{CardCatalog.CopyHelmet.Id}~1";
            player.Bonus = host;
            player.Area = Area;
            player.AreaSideA = 0;
            player.AreaIsBorrowed = true;
            game.State.TomeCards = [new(2, host, Skill, 0, Marked: true, Inverted: true)];
            rival.ZahiraHost = new(0, FieldZone.Bonus, -1, host);

            Check(game.PlayCode(0, 0).Success && player.Deck.Contains(CardCatalog.CopyHelmet.Id) && player.Deck.Contains(Skill) &&
                !player.Deck.Contains(host) && game.State.TomeCards == null && rival.ZahiraHost == null,
                "Deben volver la carta física del clon y sus anexos. Zahira sale del juego.");
            Check(rival.Deck.Contains(Area) && !player.Deck.Contains(Area) && !player.AreaIsBorrowed &&
                player.CorruptsOnMemory(CardZone.Deck, player.Deck.IndexOf(Skill)), "Deben conservarse dueño y marcas al barajar.");
        }),
        ("Brújula 16 Bits: colocar la última carta como Bonus agota el mazo una sola vez", () =>
        {
            var random = new ScriptedRandom();
            random.EnqueueDie(1);
            var game = Fixture([Skill], random);
            game.State.Active.Dexterity = 0;
            Check(game.PlayCode(0, 0).Success && game.State.Active.Bonus == Skill && game.State.Phase == Phase.RecycleChoice,
                "Con una sola carta se coloca directamente y se inicia el reciclaje.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.Active.Bonus == Skill && game.State.Active.Recycled &&
                game.State.Active.Deck.SequenceEqual([Compass]) && game.State.Phase == Phase.Actions,
                "Al reanudar el reciclaje debe conservarse el Bonus elegido.");
        })
    ];

    private static GameEngine Fixture(List<string> deck, ScriptedRandom? random = null)
    {
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [[Compass], []], Decks = [deck, [Attack, Attack]],
            Bonuses = [null, Attack], StartingSeat = 0
        }, random ?? new ScriptedRandom());
        game.State.Phase = Phase.Actions;
        return game;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
