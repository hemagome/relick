using Relick.Core;

static class NoOptionsTests
{
    private static readonly string NoOptions = CardCatalog.NoOptions.Id;
    private static readonly string Bomb = CardCatalog.TimeBomb.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Sniper = CardCatalog.Sniper.Id;
    private static readonly string Error = CardCatalog.LoadError.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Sin opciones: responde al equipo controlado, conserva una carta al azar y reanuda el efecto", () =>
        {
            var (game, rng) = Fixture([Bomb, Attack, CardCatalog.Dodge.Id, CardCatalog.Senshuni.Id], [NoOptions]);
            var caster = game.State.Players[0];
            caster.MarkForeignHandCard(1);
            caster.MarkForCorruption(CardZone.Hand, 3);
            Check(!game.PlayCode(1, 0).Success && game.PlayCode(0, 0).Success);
            ChooseField(game, 1, FieldZone.Equipment);
            Check(DuelOptions.For(game, 1).Count(o => o.Move.Kind == DuelMoveKind.PlayCode) == 1 &&
                game.State.Players[1].Equipment == CardCatalog.Bonfire.Id && !game.ResolveDecision(0, 0).Success);
            Check(DuelSnapshot.For(game, 1, 2).Players[0].Hand.All(id => id == null));

            game = GameEngine.FromSnapshot(game.SaveSnapshot(), rng);
            rng.EnqueueDie(2);
            var visuals = DuelVisualEvents.Capture(game);
            Check(game.PlayCode(1, 0).Success);
            caster = game.State.Players[0];
            var responder = game.State.Players[1];
            Check(caster.Hand.SequenceEqual([CardCatalog.Dodge.Id]) &&
                caster.CorruptedMemory.SequenceEqual([CardCatalog.Senshuni.Id]) && responder.Memory.Contains(Attack));
            Check(responder.ActionsLeft == 1 && responder.Memory.Contains(NoOptions) && responder.Equipment == null && game.State.Phase == Phase.Actions);
            Check(visuals.Any(cue => cue is { Kind: DuelVisualKind.HandDiscard, Player: 0, Value: 2 }) &&
                DuelSnapshot.For(game, 1, 3).Players[0].Hand.SequenceEqual(new string?[] { null }));
        }),
        ("Sin opciones: objetivo jugador, pase automático y objetivo propio", () =>
        {
            var (game, rng) = Fixture([CardCatalog.PointsIncrease.Id, Attack], [NoOptions]);
            rng.EnqueueDie(6);
            Check(game.PlayCode(0, 0).Success);
            var own = game.DecisionChoices(0).ToList().FindIndex(choice => choice.TargetPlayer == 0 && choice.Value == 1);
            Check(game.ResolveDecision(0, own).Success && game.State.Phase == Phase.Actions && game.State.Players[1].Hand.Contains(NoOptions));

            (game, rng) = Fixture([CardCatalog.PointsIncrease.Id, Attack], [NoOptions]);
            rng.EnqueueDie(6);
            Check(game.PlayCode(0, 0).Success);
            var rival = game.DecisionChoices(0).ToList().FindIndex(choice => choice.TargetPlayer == 1 && choice.Value == 3);
            Check(game.ResolveDecision(0, rival).Success && game.State.DecisionPlayer == 1);
            var before = game.State.Players[1].Relikia;
            Check(game.SetComboResponses(1, false, out var resumed).Success && resumed && game.State.Phase == Phase.Actions &&
                game.State.Players[1].Relikia == before + 1500 && game.State.Players[1].Hand.Contains(NoOptions));
        }),
        ("Sin opciones: Error de Carga puede cancelarla y el efecto que la disparó continúa", () =>
        {
            var (game, rng) = Fixture([Bomb, Attack, Attack, Error], [NoOptions]);
            Check(game.PlayCode(0, 0).Success);
            ChooseField(game, 1, FieldZone.Equipment);
            Check(game.PlayCode(1, 0).Success && game.State.Phase == Phase.ErrorResponse);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), rng);
            Check(game.PlayCode(0, 2).Success && game.State.Phase == Phase.Actions &&
                game.State.Players[0].Hand.SequenceEqual([Attack, Attack]) && game.State.Players[1].Equipment == null &&
                game.State.Players[1].ActionsLeft == 2);
        }),
        ("Sin opciones: Mira de Sniper resuelve su descarte antes de reanudar", () =>
        {
            var (game, rng) = Fixture([Bomb, Attack, Sniper], [NoOptions]);
            Check(game.PlayCode(0, 0).Success);
            ChooseField(game, 1, FieldZone.Equipment);
            rng.EnqueueDie(1);
            Check(game.PlayCode(1, 0).Success && game.State.Phase == Phase.SniperChoice &&
                game.State.Players[1].Equipment == CardCatalog.Bonfire.Id);
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), rng);
            Check(game.ResolveSniper(0, equip: true).Success && game.State.Phase == Phase.Actions &&
                game.State.Players[0].Equipment == Sniper && game.State.Players[1].Equipment == null);
        }),
        ("Sin opciones: si Magnet seleccionó el propio COMBO, vuelve a elegir", () =>
        {
            var (game, _) = Fixture([Attack], [NoOptions, CardCatalog.Dodge.Id]);
            game.State.Phase = Phase.MagnetChoice;
            Check(game.ResolveMagnet(0, false, 0).Success && game.State.DecisionPlayer == 1);
            Check(game.PlayCode(1, 0).Success && game.State.Phase == Phase.MagnetChoice);
            Check(game.ResolveMagnet(0, false, 0).Success && game.State.Phase == Phase.Combat &&
                game.State.Players[0].Hand.SequenceEqual([Attack, CardCatalog.Dodge.Id]));
        }),
        ("Sin opciones: permite responder con mano rival vacía y respeta el presupuesto de acciones", () =>
        {
            var (game, _) = Fixture([Bomb], [NoOptions]);
            Check(game.PlayCode(0, 0).Success);
            ChooseField(game, 1, FieldZone.Equipment);
            Check(game.State.Phase == Phase.Decision && game.State.DecisionPlayer == 1);
            Check(game.PlayCode(1, 0).Success && game.State.Players[0].Hand.Count == 0 && game.State.Phase == Phase.Actions);

            (game, _) = Fixture([Bomb], [NoOptions]);
            game.State.Players[1].ActionsLeft = 0;
            Check(game.PlayCode(0, 0).Success);
            ChooseField(game, 1, FieldZone.Equipment);
            Check(game.State.Phase == Phase.Actions && game.State.Players[1].Equipment == null);
        })
    ];

    private static (GameEngine Game, ScriptedRandom Random) Fixture(List<string> caster, List<string> responder)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [caster, responder], Decks = [[Attack, Attack], [Attack, Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Players[1].Equipment = CardCatalog.Bonfire.Id;
        return (game, random);
    }

    private static void ChooseField(GameEngine game, int owner, FieldZone zone)
    {
        var index = game.DecisionChoices(0).ToList().FindIndex(choice => choice.Field is { } target && target.Owner == owner && target.Zone == zone);
        Check(game.ResolveDecision(0, index).Success);
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new Exception("Falló la resolución de Sin opciones.");
    }
}
