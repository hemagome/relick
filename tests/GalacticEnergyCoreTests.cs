using Relick.Core;

static class GalacticEnergyCoreTests
{
    private static readonly string Core = CardCatalog.GalacticEnergyCore.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Bonfire = CardCatalog.Bonfire.Id;
    private static readonly string ExtraLife = CardCatalog.ExtraLife.Id;
    private static readonly string Helmet = CardCatalog.CopyHelmet.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Núcleo: exige tres Ataques de la memoria propia antes de gastar la acción", () =>
        {
            var (game, _) = Fixture([Attack, Attack, CardCatalog.SuperAttack.Id, Bonfire]);
            game.State.Active.CorruptedMemory.Add(Attack);
            game.State.Players[1].Memory.Add(Attack);
            var before = game.SaveSnapshot();
            Check(!game.PlayCode(0, 0).Success && before == game.SaveSnapshot() &&
                DuelOptions.For(game, 0).All(option => option.Move.Kind != DuelMoveKind.PlayCode),
                "La trampa y los Ataques de corruptas o del rival no completan la activación.");
        }),
        ("Núcleo: devuelve tres Ataques, baraja, roba dos y corrompe la copia jugada en ese orden", () =>
        {
            var variant = CardCatalog.AttackYuno.Id;
            var (game, _) = Fixture([Attack, Core, variant, Bonfire, Attack]);
            var player = game.State.Active;
            player.MarkForCorruption(CardZone.Deck, 1);
            var cues = DuelVisualEvents.Capture(game);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.Actions && player.ActionsLeft == 1,
                "Con exactamente tres Ataques elegibles se resuelve automáticamente por una acción.");
            Check(player.Memory.SequenceEqual([Core, Bonfire]) && player.CorruptedMemory.SequenceEqual([Core]) &&
                player.Hand.Count == 2 && player.Deck.Count == 3 &&
                player.Deck.Concat(player.Hand).Order().SequenceEqual(new[] { Attack, Attack, variant, Bonfire, ExtraLife }.Order()),
                "Se conservan las copias y la otra copia del Núcleo sigue en la memoria normal.");
            var markedSlot = player.Deck.IndexOf(ExtraLife);
            Check(markedSlot >= 0 ? player.CorruptsOnMemory(CardZone.Deck, markedSlot)
                : player.CorruptsOnMemory(CardZone.Hand, player.Hand.IndexOf(ExtraLife)), "Barajar y robar conservan las marcas.");
            Check(cues.Take(3).All(cue => cue is { Kind: DuelVisualKind.Pile, Source: CardZone.Memory, Destination: CardZone.Deck }) &&
                cues.Select(cue => cue.Kind).SequenceEqual([DuelVisualKind.Pile, DuelVisualKind.Pile, DuelVisualKind.Pile,
                    DuelVisualKind.Shuffle, DuelVisualKind.Draw, DuelVisualKind.Draw, DuelVisualKind.Pile]) &&
                cues[^1].Destination == CardZone.Corrupted, "La copia se corrompe después de ambos robos.");
        }),
        ("Núcleo: selecciona copias distintas, reanuda tras reconectar y conserva al dueño de una copia prestada", () =>
        {
            var variant = CardCatalog.AttackYuno.Id;
            var (game, random) = Fixture([Attack, variant, Bonfire, Attack, variant]);
            game.State.Active.MarkForeignHandCard(0);
            game.State.Players[1].Memory.Add(Core);
            Check(game.PlayCode(0, 0).Success && game.State.PendingDecision?.Source == Core &&
                game.DecisionChoices(0).Select(choice => choice.Value).SequenceEqual([0, 1, 3, 4]),
                "La elección admite las ilustraciones de Ataque, con una opción por copia.");
            Check(Pick(game, 4).Success && !Pick(game, 4).Success && !game.ResolveDecision(1, 0).Success &&
                game.State.Active.Memory.Count == 5 && game.State.Active.Hand.Count == 0,
                "Los Ataques esperan en la memoria hasta confirmar la tercera copia.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(Pick(game, 3).Success && Pick(game, 0).Success && game.State.Phase == Phase.Actions &&
                game.State.Active.Memory.SequenceEqual([variant, Bonfire]) && game.State.Active.Hand.Count == 2 &&
                game.State.Active.CorruptedMemory.Count == 0 && game.State.Players[1].Memory.SequenceEqual([Core]) &&
                game.State.Players[1].CorruptedMemory.SequenceEqual([Core]), "Solo se corrompe la copia prestada en la memoria de su dueño.");
        }),
        ("Núcleo: Error de Carga y Caja de Cartón anulan antes de devolver los Ataques", () =>
        {
            foreach (var box in new[] { false, true })
            {
                var (game, random) = Fixture([Attack, Attack, Attack]);
                if (box) game.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
                else game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                Check(game.PlayCode(0, 0).Success && game.State.Phase == (box ? Phase.CodeResponse : Phase.ErrorResponse),
                    "La respuesta se ofrece antes del efecto.");
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                var result = box ? game.ResolveCodeResponse(1, true) : game.PlayCode(1, 0);
                var player = game.State.Active;
                Check(result.Success && game.State.Phase == Phase.Actions && player.ActionsLeft == 2 &&
                    player.Memory.SequenceEqual([Attack, Attack, Attack, Core]) && player.Deck.SequenceEqual([Bonfire, ExtraLife]) &&
                    player.Hand.Count == 0 && player.CorruptedMemory.Count == 0, "Al anular, el Código queda en memoria y se devuelve la acción.");
            }
        }),
        ("Núcleo: Relikia corrompe una sola vez y Tomo de Magia invierte su anexo", () =>
        {
            var (game, _) = Fixture([Core, Attack, Attack, Attack], hand: []);
            var player = game.State.Active;
            player.Relikia = 400;
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && player.CorruptedMemory.SequenceEqual([Core]) &&
                player.Hand.Count == 2 && player.Relikia == 0 && player.ActionsLeft == 0, "Relikia mantiene su coste y su corrupción habitual.");

            (game, _) = Fixture([Attack, Attack, Attack], hand: []);
            player = game.State.Active;
            var tome = CardCatalog.MagicTome.Id;
            player.AttachedCards.Add(tome);
            game.State.TomeCards = [new(1, tome, Core, 0, Marked: false)];
            var play = DuelOptions.For(game, 0).Single(option => option.Move.Kind == DuelMoveKind.PlayCode);
            Check(play.Move.Apply(game, 0).Success && player.Hand.Count == 2 && player.CorruptedMemory.Count == 0 &&
                game.State.TomeCards.Single() is { Inverted: true }, "El Tomo conserva e invierte la copia que se activó como anexo.");
        }),
        ("Núcleo: un clon de Casco de Copias corrompe el Casco físico y la carta original se resuelve después", () =>
        {
            var (game, _) = Fixture([Attack, Attack, Attack]);
            var rival = game.State.Players[1];
            rival.Memory.AddRange([Attack, Attack, Attack]);
            rival.Hand.Add(Helmet);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.ErrorResponse && game.PlayCode(1, 0).Success,
                "El rival puede copiar este Código Objeto.");
            Check(rival.CorruptedMemory.SequenceEqual([Helmet]) && game.State.Active.CorruptedMemory.SequenceEqual([Core]) &&
                game.State.Players.All(player => player.Hand.Count == 2 && player.ActionsLeft == 1) && game.State.Phase == Phase.Actions,
                "Cada efecto devuelve Ataques propios, roba dos y corrompe la carta física correspondiente.");
        })
    ];

    private static CommandResult Pick(GameEngine game, int slot) => game.ResolveDecision(0,
        game.DecisionChoices(0).ToList().FindIndex(choice => choice.Owner == 0 && choice.Value == slot));

    private static (GameEngine Game, ScriptedRandom Random) Fixture(List<string> memory, List<string>? hand = null)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [hand ?? [Core], []], Decks = [[Bonfire, ExtraLife], [Attack, Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Active.Memory.AddRange(memory);
        return (game, random);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
