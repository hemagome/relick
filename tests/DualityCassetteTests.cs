using Relick.Core;

static class DualityCassetteTests
{
    private static readonly string Cassette = CardCatalog.DualityCassette.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Spare = CardCatalog.Bonfire.Id;
    private static readonly string[] Deck = [Attack, CardCatalog.Dodge.Id, CardCatalog.Sniper.Id,
        CardCatalog.Senshuni.Id, CardCatalog.Magnet.Id, CardCatalog.Ellie.Id, Spare, Attack];

    public static (string Name, Action Run)[] Cases =>
    [
        ("Casete: los seis resultados corrompen el tope y recuperan la mitad redondeada arriba", () =>
        {
            for (var die = 1; die <= 6; die++)
            {
                var (game, _) = Fixture(die);
                var player = game.State.Active;
                var visuals = DuelVisualEvents.Capture(game);
                player.MarkForCorruption(CardZone.Deck, 6);
                Check(game.PlayCode(0, 0).Success, "El Casete se activa desde la mano.");
                while (game.State.Phase == Phase.Decision)
                    Check(game.ResolveDecision(0, 0).Success, "Cada carta elegible se puede añadir a la mano.");
                var recovered = (die + 1) / 2 + (die == 6 ? 1 : 0);
                Check(player.Hand.SequenceEqual(Deck.Take(recovered)) && player.CorruptedMemory.SequenceEqual(Deck[recovered..die]) &&
                    player.Deck.SequenceEqual(Deck[die..]) && player.CorruptsOnMemory(CardZone.Deck, 6 - die),
                    $"El resultado {die} conserva el orden y las marcas del mazo restante.");
                Check(player.ActionsLeft == 1 && player.Memory.Single() == Cassette && game.State.Phase == Phase.Actions &&
                    visuals.Count(cue => cue is { Source: CardZone.Deck, Destination: CardZone.Corrupted }) == die &&
                    visuals.Count(cue => cue is { Source: CardZone.Corrupted, Destination: CardZone.Hand }) == recovered,
                    "Se cobra una acción y cada movimiento queda registrado sin usar el robo.");
            }
        }),
        ("Casete: guarda la selección de copias y el seis admite corruptas previas de ambos jugadores", () =>
        {
            var (game, random) = Fixture(6);
            game.State.Active.CorruptedMemory.Add(Spare);
            game.State.Players[1].CorruptedMemory.Add(CardCatalog.ExtraLife.Id);
            Check(game.PlayCode(0, 0).Success && game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual(Deck[..6]),
                "La primera mitad solo se elige del lote recién corrompido.");
            Check(!game.ResolveDecision(1, 0).Success && DuelOptions.For(game, 1).All(option => option.Move.Kind != DuelMoveKind.Choose),
                "Solo el activador decide qué recuperar.");
            Check(game.ResolveDecision(0, 2).Success, "Puede elegir una carta distinta del tope.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.State.Active.Hand.Single() == Deck[2] && game.DecisionChoices(0).Select(choice => choice.CardId)
                .SequenceEqual(Deck[..6].Where((_, slot) => slot != 2)), "La reconexión conserva el lote y las copias que quedan.");
            Check(game.ResolveDecision(0, 1).Success && game.ResolveDecision(0, 2).Success &&
                game.DecisionChoices(0).Any(choice => choice.CardId == Spare && choice.Owner == 0),
                "La última elección también ofrece cartas propias que ya estaban corrompidas.");
            var rival = game.DecisionChoices(0).ToList().FindIndex(choice => choice.Owner == 1);
            Check(game.ResolveDecision(0, rival).Success && game.State.Active.Hand.Count == 4 &&
                game.State.Active.Hand[3] == CardCatalog.ExtraLife.Id && game.State.Active.IsForeignHandCard(3) &&
                game.State.Players[1].CorruptedMemory.Count == 0 && game.State.Phase == Phase.Actions &&
                DuelSnapshot.For(game, 1, 1).Players[0].Hand.All(id => id == null),
                "La carta recuperada conserva su dueño original y pasa a una mano privada.");
        }),
        ("Casete: toma el dado modificado y admite Error de Carga después de tirar", () =>
        {
            var (game, _) = Fixture(3);
            game.State.Active.Dexterity = 1;
            Check(game.PlayCode(0, 0).Success && game.State.PendingDieRoll?.Purpose == DiePurpose.DualityCassette &&
                game.State.Active.Deck.SequenceEqual(Deck) && game.UseDexterity(0).Success,
                "Se puede modificar el dado antes de tocar el mazo.");
            if (game.State.Phase == Phase.DieRerollChoice) Check(game.ResolveDieReroll(0, false).Success, "Se conserva el dado final.");
            Check(game.State.Active.CorruptedMemory.SequenceEqual(Deck[..4]), "Las cuatro cartas corresponden al dado modificado.");

            (game, _) = Fixture(6);
            game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(game.PlayCode(0, 0).Success && game.PassErrorResponse(1).Success && game.State.ErrorAfterDie &&
                game.State.Active.Deck.SequenceEqual(Deck) && game.PlayCode(1, 0).Success &&
                game.State.Active.ActionsLeft == 2 && game.State.Active.CorruptedMemory.Count == 0 && game.State.Phase == Phase.Actions,
                "Cancelar tras el dado devuelve la acción y evita corromper cartas.");
        }),
        ("Casete: Caja de Cartón cancela antes del dado y Relikia corrompe la copia activada", () =>
        {
            var (game, _) = Fixture(2);
            game.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.CodeResponse &&
                game.ResolveCodeResponse(1, true).Success && game.State.LastDie == 0 && game.State.Active.Deck.SequenceEqual(Deck) &&
                game.State.Active.ActionsLeft == 2, "Caja de Cartón cancela el Código Objeto antes de lanzar.");
            game.State.Active.Relikia = 400;
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && game.State.Active.ActionsLeft == 0 &&
                game.State.Active.Relikia == 0 && game.State.Active.CorruptedMemory.SequenceEqual([Cassette, .. Deck[..2]]) &&
                game.DecisionChoices(0).All(choice => choice.CardId != Cassette),
                "La copia activada desde memoria se corrompe antes y queda fuera del lote del dado.");
        }),
        ("Casete: un mazo corto conserva el lote al reciclar y respeta el segundo agotamiento", () =>
        {
            var (game, random) = Fixture(6, Attack, Spare);
            game.State.Active.Memory.Add(CardCatalog.Senshuni.Id);
            random.EnqueueDie(1);
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.RecycleChoice &&
                game.State.Active.CorruptedMemory.SequenceEqual([Attack, Spare]), "Se agota el mazo antes de elegir cartas corruptas.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Check(game.ResolveRecycling(0, [0]).Success && game.State.Active.Deck.SequenceEqual([CardCatalog.Senshuni.Id]) &&
                game.DecisionChoices(0).Select(choice => choice.CardId).SequenceEqual([Attack, Spare]),
                "Tras reciclar, el lote original sigue siendo el único objetivo de la primera mitad.");
            Check(game.ResolveDecision(0, 0).Success && game.State.Active.Hand.SequenceEqual([Attack, Spare]) &&
                game.State.Active.Deck.Count == 1 && game.State.Phase == Phase.Actions,
                "Se recupera una de las dos cartas y otra por el seis aunque el mazo original se haya agotado.");

            (game, _) = Fixture(4, Attack);
            game.State.Active.Recycled = true;
            Check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.Finished && game.State.Active.Hand.Count == 0 &&
                game.State.Active.CorruptedMemory.Single() == Attack, "El segundo agotamiento termina la partida antes de recuperar.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(int die, params string[] deck)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[Cassette], []], Decks = [(deck.Length == 0 ? Deck : deck).ToList(), [Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Active.Dexterity = 0;
        random.EnqueueDie(die);
        return (game, random);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
