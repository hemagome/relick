using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private async Task CheckTargetResponseLayout()
    {
        var card = CardCatalog.NoOptions;
        if (BoardArt.LoadCardArtwork(card) == null || BoardArt.CardType(card) != "CÓDIGO · JUGADA" || !card.IsCombo)
            throw new Exception("Falta el arte o la clasificación de Sin opciones.");

        var attack = CardCatalog.Attack.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[CardCatalog.TimeBomb.Id, attack, attack], [card.Id]],
            Decks = [[attack, attack], [attack, attack]], Bonuses = [null, null], StartingSeat = 0
        }, new SeededRandom(26));
        game.State.Phase = Phase.Actions;
        game.State.Players[1].Equipment = CardCatalog.Bonfire.Id;
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se pudo jugar Bomba de Tiempo.");
        var target = DuelOptions.For(game, 0).Single(option => option.Field is { Owner: 1, Zone: FieldZone.Equipment });
        if (!target.Move.Apply(game, 0).Success) throw new Exception("No se pudo seleccionar el equipo rival.");

        ResetAnimations();
        var response = DuelSnapshot.For(game, 1, 1);
        PresentTestSnapshot(response);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var combo = response.Options.Single(option => option.Move.Kind == DuelMoveKind.PlayCode);
        var pass = response.Options.Single(option => option.Move.Kind == DuelMoveKind.Choose);
        if (!PlayableHandIndices.SequenceEqual([0]) || !PresentsOption(pass))
            throw new Exception("Sin opciones debe iluminarse y permitir pasar.");
        SelectCard(combo.Move.Index);
        if (PresentedButton(combo) is not CardActionButton { Text: "Activar" } activate || activate.GetParent() != _cardActions)
            throw new Exception("Sin opciones debe ofrecer Activar junto a la carta.");

        if (!combo.Move.Apply(game, 1).Success) throw new Exception("No se pudo activar Sin opciones.");
        PresentTestSnapshot(DuelSnapshot.For(game, 1, 2));
        if (_opponentCards.Count != 1 || !_opponentCards[0].IsFaceDown || _snapshot!.Players[1].Equipment != null)
            throw new Exception("La carta al azar queda oculta y se resuelve Bomba de Tiempo.");
    }

    private async Task CheckDualityCassetteLayout()
    {
        var card = CardCatalog.DualityCassette;
        if (BoardArt.LoadCardArtwork(card) == null || BoardArt.CardType(card) != "CÓDIGO · OBJETO" || card.IsCombo)
            throw new Exception("Falta el arte o la clasificación de Casete de Dualidad.");

        var attack = CardCatalog.Attack.Id;
        var dodge = CardCatalog.Dodge.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[card.Id], []], Decks = [[attack, dodge, attack], [attack]],
            Bonuses = [null, null]
        }, new SeededRandom(1));
        game.State.Phase = Phase.Actions;
        game.State.Active.Dexterity = 0;
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se pudo jugar el Casete.");

        ResetAnimations();
        var before = DuelSnapshot.For(game, 0, 1);
        PresentTestSnapshot(before);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var choice = before.Options.Single(option => option.Move.Kind == DuelMoveKind.Choose && option.CardId == dodge);
        PrepareTestOption(before.Options.IndexOf(choice));
        if (!ChoosingMemoryRecovery || PresentedButton(choice) == null)
            throw new Exception("Las corruptas elegibles deben aparecer en la galería con un botón de recuperación.");

        var events = DuelVisualEvents.Capture(game);
        if (!choice.Move.Apply(game, 0).Success) throw new Exception("No se pudo recuperar la carta del Casete.");
        var after = DuelSnapshot.For(game, 0, 2);
        after.Visuals = events;
        var flight = CaptureOrigins(after, before).Values.Single(cue => cue.Event.Source == CardZone.Corrupted);
        if (flight.Face != dodge || flight.StartsFaceUp != false || flight.EndsFaceUp != true)
            throw new Exception("La carta recuperada se gira únicamente al llegar a su mano.");
    }

    private async Task CheckGalacticEnergyCoreLayout()
    {
        var card = CardCatalog.GalacticEnergyCore;
        if (BoardArt.LoadCardArtwork(card) == null || BoardArt.CardType(card) != "CÓDIGO · OBJETO" || card.IsCombo)
            throw new Exception("Falta el arte o la clasificación de Núcleo Energético Galáctico x3.");

        var attack = CardCatalog.Attack.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[card.Id], []], Decks = [[attack], [attack]], Bonuses = [null, null]
        }, new SeededRandom(26));
        game.State.Phase = Phase.Actions;
        game.State.Active.Memory.AddRange([attack, CardCatalog.Bonfire.Id, attack, attack, attack]);
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se pudo jugar el Núcleo.");

        ResetAnimations();
        var before = DuelSnapshot.For(game, 0, 1);
        PresentTestSnapshot(before);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!ChoosingMemoryRecovery || _deckSearchOptions.Length != 4 || _deckSearchOptions.Any(option => option.CardId != attack))
            throw new Exception("El Núcleo debe mostrar solo los cuatro Ataques de la memoria.");
        var first = _deckSearchOptions[0];
        SelectCard(0);
        if (!PresentsOption(first) || !first.Move.Apply(game, 0).Success || !game.ResolveDecision(0, 0).Success)
            throw new Exception("La galería debe permitir elegir las primeras dos copias.");
        var second = DuelSnapshot.For(game, 0, 2);
        PresentTestSnapshot(second);
        if (_deckSearchOptions.Length != 2 || !game.ResolveDecision(0, 0).Success)
            throw new Exception("Quedan dos candidatos para elegir el tercer Ataque.");
        PresentTestSnapshot(DuelSnapshot.For(game, 0, 3));
        if (ChoosingEffectCards || _handCards.Count != 2 || _snapshot!.Players[0].CorruptedCards?.Single() != card.Id)
            throw new Exception("El tablero vuelve a la mano con dos cartas y muestra el Núcleo en corruptas.");
    }

    private async Task CheckSecretRoomLayout()
    {
        var room = CardCatalog.SecretRoom;
        var attack = CardCatalog.Attack.Id;
        var trap = CardCatalog.SuperAttack.Id;
        if (BoardArt.LoadCardArtwork(room) == null || BoardArt.CardType(room) != "CÓDIGO · JUGADA")
            throw new Exception("Falta el arte o la clasificación de Habitación Secreta.");

        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[room.Id, attack], []], Decks = [[room.Id, attack], [attack]],
            Traps = [[trap, CardCatalog.SuperMagnet.Id], []], Bonuses = [attack, null]
        }, new SeededRandom(26));
        game.State.Phase = Phase.Actions;
        game.State.Active.Memory.AddRange([attack, CardCatalog.Dodge.Id]);
        if (!game.Exchange(0, 0).Success) throw new Exception("No se pudo canjear Habitación Secreta.");

        ResetAnimations();
        var recover = DuelSnapshot.For(game, 0, 1);
        PresentTestSnapshot(recover);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var first = _deckSearchOptions.Single(option => option.CardId == CardCatalog.Dodge.Id);
        SelectCard(Array.IndexOf(_deckSearchOptions, first));
        if (!ChoosingMemoryRecovery || !PresentsOption(first) || !first.Move.Apply(game, 0).Success || !game.Exchange(0, 0).Success)
            throw new Exception("El canje debe mostrar la recuperación y permitir el segundo canje al terminar.");

        PresentTestSnapshot(DuelSnapshot.For(game, 0, 2));
        var choice = _deckSearchOptions.Single(option => option.CardId == trap);
        SelectCard(Array.IndexOf(_deckSearchOptions, choice));
        if (PresentedButton(choice)?.Text != "Colocar trampa" || _snapshot!.Players[0].Pr != 3000 || !choice.Move.Apply(game, 0).Success)
            throw new Exception("El nuevo Bonus debe mostrar el premio y permitir colocar la Trampa.");
        PresentTestSnapshot(DuelSnapshot.For(game, 0, 3));
        if (ChoosingEffectCards || !_handCards.Any(card => card.Card.Id == trap) || _snapshot!.Players[0].Bonus != room.Id)
            throw new Exception("La Trampa debe quedar en mano y la Habitación en el Bonus.");
    }

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
            await CheckCopyHelmetTargets();

            var dog = new GameEngine(26);
            dog.State.Phase = Phase.Actions;
            var seat = dog.State.CurrentPlayer;
            var index = dog.State.Active.Hand.Count;
            dog.State.Active.Hand.Add(CardCatalog.GhostDog.Id);
            PresentTestSnapshot(DuelSnapshot.For(dog, seat, 4));
            SelectCard(index);
            var reveal = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.RevealGhostDog);
            if (PresentedButton(reveal) is not CardActionButton { Text: "Revelar", Symbol: GameIcon.Eye } button ||
                button.GetParent() != _cardActions || !button.TooltipText.Contains("próxima etapa de acciones"))
                throw new Exception("Perro Fantasma debe mostrar Revelar con un ojo y conservar el momento de activación en la ayuda.");
        }

        await CheckAreaAndDeckArrivals();
        await CheckTargetResponseLayout();
        await CheckDualityCassetteLayout();
        await CheckGalacticEnergyCoreLayout();
        await CheckSecretRoomLayout();
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

    private async Task CheckCopyHelmetTargets()
    {
        var source = CardCatalog.ExtraLife;
        var helmet = CardCatalog.CopyHelmet.Id;
        var counter = CardCatalog.LoadError.Id;
        var attack = CardCatalog.Attack.Id;
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], Hands = [[source.Id, helmet, counter], [helmet]],
            Decks = [[attack, attack], [attack, attack]], Bonuses = [null, null], StartingSeat = 0
        }, new SeededRandom(26));
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        var revision = 30UL;
        async Task Present(int seat)
        {
            ResetAnimations();
            PresentTestSnapshot(DuelSnapshot.For(game, seat, revision++));
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!game.PlayCode(0, 0).Success) throw new Exception("No se prepara el objetivo de Casco de Copias.");
        await Present(1);
        var reply = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.PlayCode);
        SelectCard(reply.Move.Index);
        if (PresentedButton(reply) is not CardActionButton { Text: "Activar" } activate ||
            activate.GetParent() != _cardActions || !activate.TooltipText.Contains(source.Name))
            throw new Exception("Casco de Copias debe ofrecer Activar junto a la carta e indicar su objetivo único.");
        if (!reply.Move.Apply(game, 1).Success || game.State.Phase != Phase.ErrorResponse)
            throw new Exception("El objetivo único debe copiarse directamente y devolver la prioridad.");

        await Present(0);
        reply = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.PlayCode && ArenaActionText.HandCard(o, _snapshot)?.Id == helmet);
        SelectCard(reply.Move.Index);
        var actions = game.State.Players[0].ActionsLeft;
        if (PresentedButton(reply) is not CardActionButton { Text: "Activar" } || !reply.Move.Apply(game, 0).Success ||
            game.State.Players[0].ActionsLeft != actions || !game.State.Players[0].Hand.Contains(helmet))
            throw new Exception("Con dos objetivos, abrir el selector debe conservar la acción y el Casco.");

        await Present(1);
        if (ChoosingCopyTarget || _deckSearchOptions.Length != 0)
            throw new Exception("El rival no debe ver el selector de copias.");
        await Present(0);
        if (!ChoosingCopyTarget || _handCards.Count != 2 || _moves.GetChildren().OfType<Button>().Single() is not { Text: "Copiar", Disabled: true })
            throw new Exception("La galería debe mostrar los dos eslabones y esperar una selección para copiar.");

        for (var seat = 0; seat < game.State.Players.Length; seat++)
        {
            SelectCard(seat);
            var choice = _deckSearchOptions[seat];
            var label = $"{source.Name} · {game.State.Players[seat].Name} · RAM {seat + 1}";
            if (PresentedButton(choice) is not { Text: "Copiar" } || _contextTitle.Text != source.Name ||
                !_selection.Text.Contains(label) || !_handCards[seat].TooltipText.StartsWith(label))
                throw new Exception("La confirmación debe identificar qué eslabón se copiará aunque ambos tengan la misma carta.");
        }
        if (!_deckSearchOptions[1].Move.Apply(game, 0).Success || game.State.Players[0].ActionsLeft != actions - 1)
            throw new Exception("Confirmar el eslabón elegido debe consumir la acción del Casco.");
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
