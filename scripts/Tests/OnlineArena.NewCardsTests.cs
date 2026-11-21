using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckNewCardsLayout()
    {
        foreach (var card in new[] { CardCatalog.MagicTome, CardCatalog.CopyHelmet, CardCatalog.FutureEra, CardCatalog.Platforms,
            CardCatalog.MouseBike, CardCatalog.GlassFlute, CardCatalog.Compass16Bit, CardCatalog.ThiefGhost, CardCatalog.GhostDog })
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

        await CheckAreaAndDeckArrivals();
        CheckIronwingDieChoices();

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
        CheckCompassBonusGallery();
        GD.Print("PVP_NEW_CARDS_PASS");
    }

    private void CheckCompassBonusGallery()
    {
        var attack = CardCatalog.Attack.Id;
        var skill = CardCatalog.Game.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[CardCatalog.Compass16Bit.Id], [attack]],
            Decks = [[attack, skill], [attack, attack]], Bonuses = [attack, attack]
        }, new SeededRandom(26));
        game.State.Phase = Phase.Actions;
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se pudo activar Brújula 16 Bits.");

        ResetAnimations();
        PresentTestSnapshot(DuelSnapshot.For(game, 1, 1));
        if (ChoosingDeckGallery || _deckSearchOptions.Length != 0)
            throw new Exception("El rival no debe ver la búsqueda de la Brújula.");
        PresentTestSnapshot(DuelSnapshot.For(game, 0, 1));
        var index = Array.FindIndex(_deckSearchOptions, option => option.CardId == skill);
        if (!ChoosingDeckGallery || index < 0) throw new Exception("La galería de la Brújula debe incluir las habilidades.");
        _selected.Clear();
        SelectCard(index);
        var choice = _deckSearchOptions[index];
        if (PresentedButton(choice)?.Text != "Colocar como Bonus" || !choice.Move.Apply(game, 0).Success || game.State.Active.Bonus != skill)
            throw new Exception("La opción de la galería debe colocar la carta elegida como Bonus.");
        _selected.Clear();
    }

    private void CheckIronwingDieChoices()
    {
        var attack = CardCatalog.Attack.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[], []], Decks = [[attack, attack, attack, attack], [attack]],
            Bonuses = [null, null], Relikarios = [RelikarioCatalog.Ironwing.Id, RelikarioCatalog.Osamu.Id]
        }, new SeededRandom(26));
        game.State.Active.Dexterity = 0;
        game.State.Phase = Phase.AttackRerollChoice;

        void CheckCosts(params int[] expected)
        {
            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, 0, 14));
            var actual = _snapshot!.Options.Where(o => o.Move.Kind == DuelMoveKind.IronwingDie && PresentsOption(o))
                .Select(o => o.Move.Value);
            if (!actual.SequenceEqual(expected) || !PresentsKind(DuelMoveKind.AttackDie))
                throw new Exception("Modificar el dado debe ofrecer el coste mínimo de Ironwing para cada aumento y permitir conservarlo.");
            if (expected.Length == 0 && (_actionMenus.Count != 0 || _selection.Text != "Confirma el resultado del dado."))
                throw new Exception("El aviso ofrece modificar el dado de Ironwing sin un aumento disponible.");
        }

        game.State.PendingAttackRoll = new(0, CardCatalog.IronwingRoll.Id, 3);
        CheckCosts(2, 4);
        game.State.PendingAttackRoll = game.State.PendingAttackRoll with { Die = 4 };
        CheckCosts(2);
        game.State.Active.Deck.RemoveRange(1, 3);
        CheckCosts();
        game.State.Active.Relikario = RelikarioCatalog.IronwingLv2;
        CheckCosts(1);
    }

    private async Task CheckAreaAndDeckArrivals()
    {
        var attack = CardCatalog.Attack.Id;
        var area = CardCatalog.BlackHole.Id;
        var random = new DuelRandom();
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[area], []], Decks = [[attack, attack], [attack, attack]],
            Bonuses = [null, null], Relikarios = [RelikarioCatalog.Ironwing.Id, RelikarioCatalog.Osamu.Id]
        }, random);
        game.State.Phase = Phase.Actions;
        game.State.Active.Relikia = RelikarioCatalog.IronwingLv2.EvolutionCost;
        game.State.Active.Memory.AddRange([attack, attack]);
        foreach (var player in game.State.Players) player.Dexterity = 0;

        ResetAnimations();
        var before = DuelSnapshot.For(game, 0, 10);
        PresentTestSnapshot(before);
        var visuals = DuelVisualEvents.Capture(game);
        if (!game.ActivateArea(0, 0).Success) throw new Exception("No se activa Agujero Negro.");
        var after = DuelSnapshot.For(game, 0, 11);
        after.Visuals = visuals;
        var origins = CaptureOrigins(after, before);
        PresentTestSnapshot(after);
        Animate(visuals, origins);
        var slot = (ZoneCardButton)ZoneNode(0, CardZone.Area, -1)!;
        if (slot.Modulate.A != 1 || slot.PresentedCard != null)
            throw new Exception("El contorno del Área debe verse antes de que aterrice Agujero Negro.");
        ResetAnimations();
        if (slot.PresentedCard?.Id != area) throw new Exception("El Área queda oculta al interrumpir el vuelo.");

        random.Replay([5, 5]);
        if (!game.ResolveDecision(0, 0).Success || !game.ResolveDecision(0, 1).Success)
            throw new Exception("No se resuelve Agujero Negro.");
        random.Finish();
        before = DuelSnapshot.For(game, 0, 12);
        PresentTestSnapshot(before);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        visuals = DuelVisualEvents.Capture(game);
        random.Record(limit => limit - 1);
        if (!game.Evolve(0).Success) throw new Exception("Ironwing no devuelve sus Ataques al mazo.");
        after = DuelSnapshot.For(game, 0, 13);
        after.Visuals = visuals;
        origins = CaptureOrigins(after, before);
        PresentTestSnapshot(after);
        Animate(visuals, origins);
        var deck = (DeckStack)ZoneNode(0, CardZone.Deck, -1)!;
        if (deck.Modulate.A != 1 || deck.PresentedAmount != before.Players[0].DeckCount)
            throw new Exception("El mazo debe mostrar su contorno y sus cartas anteriores durante los vuelos.");
        for (var frame = 0; frame < 12 && IsAnimating; frame++) UpdateFlights(1.2);
        if (IsAnimating || deck.PresentedAmount != after.Players[0].DeckCount)
            throw new Exception("El mazo debe contar los dos Ataques al terminar las animaciones.");
    }
}
