using Relick.Core;

static class ComboParryTests
{
    private static readonly string Parry = CardCatalog.ComboParry.Id;
    private static readonly string Attack = CardCatalog.Attack.Id;
    private static readonly string Dodge = CardCatalog.Dodge.Id;
    private static readonly string Error = CardCatalog.LoadError.Id;
    private static readonly string Agility = CardCatalog.Agility.Id;
    private static readonly string Bonfire = CardCatalog.Bonfire.Id;

    public static (string Name, Action Run)[] Cases =>
    [
        ("Combo Parry: revela la mano completa, bloquea respuestas y devuelve todos los COMBO", () =>
        {
            var (game, random) = Fixture([Parry], [Dodge, Bonfire, Error, Attack, Parry]);
            Check(Play(game, 0, 0).Success && game.State.PendingDecision?.Source == Parry &&
                game.State.Active.ActionsLeft == 1 && game.State.Active.Memory.Single() == Parry,
                "El Código cuesta una acción y pasa directamente a la revelación.");
            Check(DuelSnapshot.For(game, 0, 1).Players[1].Hand.SequenceEqual([Dodge, Bonfire, Error, Attack, Parry]) &&
                DuelSnapshot.For(game, -1, 1).Players[1].Hand.All(id => id == null) &&
                !game.CanPlayLoadError(1) && DuelOptions.For(game, 1).All(o => o.Move.Kind != DuelMoveKind.PlayCode) &&
                !Play(game, 1, 2).Success && !Play(game, 1, 4).Success && !game.ResolveDecision(1, 0).Success,
                "El activador ve la mano y ningún COMBO rival ni Error de Carga puede responder.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            var visuals = DuelVisualEvents.Capture(game);
            Confirm(game, 0);
            var rival = game.State.Players[1];
            Check(rival.Hand.SequenceEqual([Bonfire, Attack]) &&
                rival.Deck.Order().SequenceEqual(new[] { Attack, Attack, Dodge, Error, Parry }.Order()) &&
                rival.Memory.Count == 0 && rival.ActionsLeft == 2 && game.State.Phase == Phase.Actions,
                "Los COMBO entran al mazo sin activar ni descartar esas cartas.");
            Check(visuals.Count(v => v.Kind == DuelVisualKind.Return) == 3 &&
                visuals.Last() is { Kind: DuelVisualKind.Shuffle, Player: 1 } &&
                DuelSnapshot.For(game, 0, 2).Players[1].Hand.All(id => id == null),
                "La mano vuelve a ocultarse y el mazo se baraja.");
        }),
        ("Combo Parry: conserva dueños, marcas y visibilidad previa de las copias", () =>
        {
            var (game, _) = Fixture([Parry], [Dodge, Bonfire, Agility, Attack]);
            var rival = game.State.Players[1];
            rival.MarkForeignHandCard(2);
            rival.MarkForCorruption(CardZone.Hand, 2);
            rival.RevealHandCard(1);
            Check(Play(game, 0, 0).Success && DuelSnapshot.For(game, 0, 1).Players[1].HandOwners?[2] == 0,
                "El nombre y dueño de la carta prestada se muestran al revelar.");
            var cues = DuelVisualEvents.Capture(game);
            Confirm(game, 0);
            var p = game.State.Active;
            var marked = p.Deck.IndexOf(Agility);
            Check(marked >= 0 && p.CorruptsOnMemory(CardZone.Deck, marked) && rival.Deck.Contains(Dodge) &&
                !rival.Deck.Contains(Agility) && rival.RevealedHandSlots.SequenceEqual([0]) &&
                DuelSnapshot.For(game, 0, 2).Players[1].Hand.SequenceEqual([Bonfire, null]),
                "Las copias vuelven a sus dueños y la revelación previa no se pierde.");
            Check(cues.Any(v => v is { Kind: DuelVisualKind.Return, Player: 1, DestinationPlayer: 0 }) &&
                cues.Where(v => v.Kind == DuelVisualKind.Shuffle).Select(v => v.Player).SequenceEqual([1, 0]),
                "Se barajan los dos mazos si ambos reciben cartas.");
        }),
        ("Combo Parry: baraja con mano vacía o sin COMBO", () =>
        {
            var (empty, _) = Fixture([Parry], []);
            var emptyCues = DuelVisualEvents.Capture(empty);
            Check(Play(empty, 0, 0).Success && empty.State.Phase == Phase.Actions &&
                emptyCues.Single(v => v.Kind == DuelVisualKind.Shuffle).Player == 1, "La mano vacía no requiere confirmación para barajar.");
            var (game, _) = Fixture([Parry], [Bonfire]);
            var cues = DuelVisualEvents.Capture(game);
            Check(Play(game, 0, 0).Success && game.State.PendingDecision?.Source == Parry, "También se revela una mano sin COMBO.");
            Confirm(game, 0);
            Check(game.State.Players[1].Hand.Single() == Bonfire && cues.Single(v => v.Kind == DuelVisualKind.Shuffle).Player == 1 &&
                cues.All(v => v.Kind != DuelVisualKind.Return),
                "Se baraja sin mover la carta no-COMBO.");
        }),
        ("Combo Parry: abre una respuesta en combate, se puede usar en turno rival y el ataque continúa", () =>
        {
            var (game, random) = Fixture([Attack, Error], [Parry]);
            game.State.Phase = Phase.Combat;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            random.EnqueueDie(5);
            Check(game.Attack(0, 0, 0).Success && game.State.Phase == Phase.DefenseChoice &&
                DuelOptions.For(game, 1).Any(o => o.Move.Kind == DuelMoveKind.PlayCode),
                "El defensor puede responder aunque Combo Parry sea su única respuesta.");
            Check(Play(game, 1, 0).Success && DuelSnapshot.For(game, 1, 1).Stage == Phase.Combat, "La revelación conserva la etapa de combate.");
            Confirm(game, 1);
            Check(game.State.Phase == Phase.Combat && game.State.Players[1].Health == 5 &&
                game.State.Players[1].ActionsLeft == 1 && game.State.Active.Deck.Contains(Error) &&
                game.State.Active.Relikia == CardCatalog.Destiny.Reward, "El ataque continúa con su daño y premio.");
        }),
        ("Combo Parry: reanuda una búsqueda autorizada con las opciones del mazo actualizado", () =>
        {
            var (game, random) = Fixture([CardCatalog.KonamyCode.Id, Error], [Parry]);
            Check(Play(game, 0, 0).Success && game.PassErrorResponse(1).Success &&
                game.State.PendingDecision?.Source == CardCatalog.KonamyCode.Id, "Konamy inicia su búsqueda si el rival deja resolver.");
            Check(Play(game, 1, 0).Success && game.State.PendingDecision?.Suspended?.Source == CardCatalog.KonamyCode.Id &&
                DuelSnapshot.For(game, 1, 2).Stage == Phase.Actions, "El Código interrumpe una decisión que admite COMBO.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Confirm(game, 1);
            var returned = game.State.Active.Deck.IndexOf(Error);
            Check(returned >= 0 && game.State.PendingDecision?.Source == CardCatalog.KonamyCode.Id &&
                game.ResolveDeckSearch(0, returned).Success && game.State.Active.Hand.Single() == Error &&
                game.State.Phase == Phase.Actions, "La búsqueda reanudada valida el mazo barajado.");
        }),
        ("Combo Parry: respeta la ventana de Error de Carga y el ajuste de pasar COMBO", () =>
        {
            var (game, _) = Fixture([CardCatalog.ExtraLife.Id, Parry, Error], [Error]);
            Check(Play(game, 0, 0).Success && game.State.Phase == Phase.ErrorResponse && Play(game, 1, 0).Success &&
                DuelOptions.For(game, 0).All(o => o.Move.Kind != DuelMoveKind.PlayCode || o.Move.Index != 0) &&
                !Play(game, 0, 0).Success, "Combo Parry no responde a Error de Carga una vez añadido a la cadena.");
            (game, _) = Fixture([Bonfire], [Parry]);
            Check(game.SetComboResponses(1, false).Success && !Play(game, 1, 0).Success &&
                !DuelOptions.For(game, 1).Any(o => o.Move.Kind == DuelMoveKind.PlayCode), "Pasar COMBO excluye la respuesta en turno rival.");
        }),
        ("Combo Parry: responde a una activación inicial y luego la deja resolver", () =>
        {
            var (game, random) = Fixture([CardCatalog.Bonfire.Id, Error, Agility], [Parry]);
            Check(new DuelMove { Kind = DuelMoveKind.Equip, Index = 0 }.Apply(game, 0).Success &&
                game.State.Phase == Phase.ErrorResponse && Play(game, 1, 0).Success &&
                game.State.PendingDecision?.Source == Parry, "Parry abre una respuesta al equipo antes de que se instale.");
            game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
            Confirm(game, 1);
            Check(game.State.Active.Equipment == Bonfire && game.State.Active.Deck.Contains(Error) &&
                game.State.Active.Deck.Contains(Agility) && game.State.Phase == Phase.Actions, "La respuesta termina y el equipo original se instala una vez.");
        }),
        ("Combo Parry: se puede activar con Relikia y devuelve al efecto del dado tras interrumpirlo", () =>
        {
            var (game, random) = Fixture([], [Dodge]);
            var p = game.State.Active;
            p.Memory.Add(Parry);
            p.Relikia = 400;
            Check(game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.Relikia == 0 &&
                p.ActionsLeft == 0 && p.CorruptedMemory.Single() == Parry, "Relikia aplica los costes y corrupción habituales.");
            Confirm(game, 0);
            Check(game.State.Players[1].Deck.Contains(Dodge) && game.State.Phase == Phase.Actions, "La activación desde memoria resuelve el mismo efecto.");
            (game, random) = Fixture([CardCatalog.ExtraLife.Id, Parry], [Dodge]);
            p = game.State.Active;
            p.Health = 4;
            random.EnqueueDie(2);
            Check(Play(game, 0, 0).Success && game.State.Phase == Phase.DieRerollChoice && Play(game, 0, 0).Success &&
                game.State.PendingDecision?.Source == Parry, "Se puede usar el COMBO en la ventana del dado.");
            Confirm(game, 0);
            Check(p.Health == 6 && game.State.Phase == Phase.Actions, "El resultado del dado original continúa.");
        })
    ];

    private static (GameEngine, ScriptedRandom) Fixture(string[] hand, string[] rival)
    {
        var random = new ScriptedRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["A", "B"], Hands = [hand.ToList(), rival.ToList()], Decks = [[Attack, Attack], [Attack, Attack]],
            Bonuses = [null, null], StartingSeat = 0
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        foreach (var p in game.State.Players)
        {
            p.Dexterity = 0;
            p.AttackRerollUsedThisTurn = true;
        }
        return (game, random);
    }

    private static CommandResult Play(GameEngine game, int actor, int slot) =>
        new DuelMove { Kind = DuelMoveKind.PlayCode, Index = slot }.Apply(game, actor);

    private static void Confirm(GameEngine game, int actor)
    {
        var choice = DuelOptions.For(game, actor).Single(o => o.Move.Kind == DuelMoveKind.Choose);
        Check(choice.Move.Apply(game, actor).Success, "La confirmación debe resolver Combo Parry.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
