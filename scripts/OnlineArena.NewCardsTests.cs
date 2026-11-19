using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckNewCardsLayout()
    {
        foreach (var card in new[] { CardCatalog.MagicTome, CardCatalog.CopyHelmet, CardCatalog.FutureEra,
            CardCatalog.MouseBike, CardCatalog.GlassFlute, CardCatalog.ThiefGhost, CardCatalog.GhostDog })
            if (BoardArt.LoadCardArtwork(card) == null)
                throw new Exception($"No se puede cargar el arte de {card.Name}.");

        var attack = CardCatalog.Attack.Id;
        var tome = CardCatalog.MagicTome.Id;
        var senshuni = CardCatalog.Senshuni.Id;
        foreach (var size in new[] { new Vector2I(1440, 1024), new(700, 390) })
        {
            GetWindow().ContentScaleSize = size;
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], Hands = [[tome], []],
                Decks = [[senshuni, attack, attack, attack, attack], [attack, attack]],
                Bonuses = [null, null], StartingSeat = 0
            }, new SeededRandom(26));
            game.State.Phase = Phase.Actions;
            game.State.Active.Area = CardCatalog.FutureEra.Id;
            game.State.Active.AreaSideA = 0;
            ResetAnimations();
            var before = DuelSnapshot.For(game, 0, 1);
            PresentTestSnapshot(before);
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var visuals = DuelVisualEvents.Capture(game);
            if (!game.PlayCode(0, 0).Success) throw new Exception("No se pudo jugar Tomo de Magia.");
            var after = DuelSnapshot.For(game, 0, 2);
            after.Visuals = visuals;
            var origins = CaptureOrigins(after, before);
            var flight = origins.Values.Single(cue => cue.Event is { Source: CardZone.Activation, Destination: CardZone.Attached });
            if (flight.Face != tome) throw new Exception("El Tomo debe llegar al Relikario con su propia ilustración.");
            PresentTestSnapshot(after);
            if (ZoneNode(0, CardZone.Attached, -1) is not AttachmentStack { TopCardId: var top, TomeCardCount: 2 } stack || top != tome)
                throw new Exception("La pila del Relikario no cuenta las dos cartas del Tomo.");
            var loads = visuals.Where(cue => cue is { Source: CardZone.Deck, Destination: CardZone.TomeCard }).ToArray();
            if (!loads.Select(cue => origins[cue].Face).SequenceEqual([senshuni, attack]))
                throw new Exception("Las dos cartas públicas del Tomo deben mostrar su arte al llegar, en orden.");
            await CheckAttachmentLoadFlights(visuals, origins, loads, stack, () => stack.PresentedTomeCards, faceUp: true);
            ResetAnimations();
            var code = after.Options.Single(o => o.Move.Kind == DuelMoveKind.PlayCode && o.Field?.Zone == FieldZone.TomeCard);
            SelectField(0, FieldZone.TomeCard, code.Field!.Index);
            if (!PresentsOption(code)) throw new Exception("La carta enderezada del Tomo no ofrece su jugada.");
            if (_arenaMenu?.GetPopup() is not { } menu || !Enumerable.Range(0, menu.ItemCount).Any(i => menu.GetItemText(i).Contains("D10")))
                throw new Exception("Era Futurista debe ofrecer el D10 desde el menú del duelo.");

            if (!code.Move.Apply(game, 0).Success) throw new Exception("No se pudo activar el Código del Tomo.");
            var next = DuelSnapshot.For(game, 0, 3);
            PresentTestSnapshot(next);
            SelectFieldPile(0, FieldZone.Attached);
            var dialog = GetNodeOrNull<AcceptDialog>("CardCollectionDialog");
            if (dialog?.FindChild("CollectionCard1", true, false) is not Button inverted || !Mathf.IsEqualApprox(inverted.RotationDegrees, 180))
                throw new Exception("La carta usada se debe mostrar invertida en la galería del Tomo.");
            dialog.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        for (var result = 1; result <= 10; result++)
        {
            _die.Value = 0;
            PresentDieCue(new DuelVisualEvent { Kind = DuelVisualKind.Die, Value = result, DieIsD10 = true });
            _die.AdvancePlayback(DiceFace.RollSeconds);
            if (!_die.IsD10 || _die.TopFaceValue != result || _die.DisplayedValue != result)
                throw new Exception($"La cara física del D10 debe mostrar {result}.");
        }
        PresentDieCue(new DuelVisualEvent { Kind = DuelVisualKind.DieAdjustment, Value = 6, Caption = "Era Futurista · D10 convertido en 6" });
        _die.AdvancePlayback(DiceFace.TurnSeconds);
        if (!_die.IsD10 || _die.TopFaceValue != 6)
            throw new Exception("Convertir el resultado debe girar el mismo D10 hasta la cara 6.");

        _die.Value = _secondDie.Value = 0;
        PresentDieCue(new DuelVisualEvent { Kind = DuelVisualKind.Die, Value = 4, SecondDie = 3, SecondDieIsD10 = true });
        _die.AdvancePlayback(DiceFace.RollSeconds);
        _secondDie.AdvancePlayback(DiceFace.RollSeconds);
        if (_die.IsD10 || !_secondDie.IsD10 || _die.TopFaceValue != 4 || _secondDie.TopFaceValue != 3)
            throw new Exception("Cada dado de Turbo Overlap debe usar su propia geometría.");

        _die.Value = 0;
        _die.QueueRoll(3, d10: true);
        _die.QueueRoll(3);
        _die.AdvancePlayback(DiceFace.RollSeconds + DiceFace.ResultSeconds);
        _die.AdvancePlayback(DiceFace.RollSeconds);
        if (_die.IsD10 || _die.TopFaceValue != 3)
            throw new Exception("Una nueva tirada D6 debe recuperar el dado cúbico.");
        GD.Print("PVP_NEW_CARDS_PASS");
    }
}
