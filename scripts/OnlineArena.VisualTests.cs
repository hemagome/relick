using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal void PresentTestSnapshot(DuelSnapshot snapshot, bool animateActions = false)
    {
        var previous = _snapshot;
        if (!animateActions) foreach (var pool in _actionPools) pool.Settle();
        _preparing = _choosingOpeningSkill = false;
        _snapshot = snapshot; ResetInteraction(); Render(animateActions, previous);
    }

    internal async Task CheckSuperAttackLayout()
    {
        if (BoardArt.LoadCardArtwork(CardCatalog.SuperAttack) == null)
            throw new Exception("Falta el arte de Súper Ataque.");
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], IncludeSuperAttack = true,
            Hands = [[CardCatalog.Attack.Id, CardCatalog.Attack.Id], []],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
            Bonuses = [CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id]
        }, new SeededRandom(27));
        game.State.Phase = Phase.Actions;
        game.State.Turn = 2;
        if (!game.Exchange(0, 0).Success || !game.Exchange(0, 0).Success)
            throw new Exception("No se prepara el canje de Súper Ataque.");
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        PresentTestSnapshot(DuelSnapshot.For(game, 0, 397));
        await Layout();
        if (_contextCost.Text != "Canje de dos Ataques" || !_moveButtons.Any(button => button.Text == "Crear Súper Ataque"))
            throw new Exception("Falta la creación de Súper Ataque en la interfaz.");
        if (!game.ResolveTrapCraft(0, true).Success) throw new Exception("No se añade Súper Ataque a la mano.");
        game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
        game.State.Phase = Phase.Combat;
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            PresentTestSnapshot(DuelSnapshot.For(game, 0, 398));
            await Layout();
            var hand = _handCards.Single(card => card.Card.Id == CardCatalog.SuperAttack.Id);
            SelectCard(_handCards.IndexOf(hand));
            await Layout();
            if (!hand.HasAttackArtwork || _inspector.ClassificationLabel.Text != "Código Trampa · Ataque" ||
                _contextCost.Text != "Consume Súper Ataque" || !PresentsKind(DuelMoveKind.Attack) || PresentsKind(DuelMoveKind.PlayCode))
                throw new Exception("Súper Ataque no muestra su arte y su opción de ataque.");
        }
        GD.Print("PVP_SUPER_ATTACK_PASS · receta, arte y ataque desde la mano.");
    }

    internal async Task CheckZahiraLayout()
    {
        if (BoardArt.LoadCardArtwork(CardCatalog.ZahiraFlair) == null)
            throw new Exception("Falta el arte de Zahira Flair.");
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = 0, IncludeZahiraFlair = true,
            Hands = [[CardCatalog.Jessica.Id, CardCatalog.Senshuni.Id], []],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
            Bonuses = [CardCatalog.Attack.Id, CardCatalog.Pendant.Id]
        }, new SeededRandom(12));
        game.State.Phase = Phase.Actions;
        if (!game.Exchange(0, 0).Success || !game.Exchange(0, 0).Success)
            throw new Exception("No se prepara el canje de Zahira Flair.");
        async Task Layout(int seat)
        {
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 391));
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await Layout(0);
        if (_contextCost.Text != "Canje de dos Personajes" || !_moveButtons.Any(button => button.Text == "Crear Zahira Flair"))
            throw new Exception("Falta la creación de Zahira Flair en la interfaz.");
        if (!game.ResolveTrapCraft(0, true).Success) throw new Exception("Falta la selección de destino.");
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            await Layout(1);
            if (PresentsKind(DuelMoveKind.ZahiraTarget) || !_selection.Text.Contains("rival"))
                throw new Exception("El rival puede elegir el destino de Zahira Flair.");
            await Layout(0);
            var target = _moveButtons.Single(button => PresentedOption(button)?.Move is
                { Kind: DuelMoveKind.ZahiraTarget, Target: 1, Value: (int)FieldZone.Relikario });
            _movesScroll.EnsureControlVisible(target);
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            target.Disabled = false;
            GuiInputTestDriver.Click(target);
        }
        if (!game.ResolveZahiraTarget(0, 1, FieldZone.Relikario, -1).Success)
            throw new Exception("No se coloca Zahira Flair.");
        foreach (var seat in new[] { 0, 1 })
        {
            await Layout(seat);
            var trap = _root!.FindChild(seat == 0 ? "Zahira0" : "Zahira1", true, false) as Button;
            if (trap?.Icon == null || !trap.Text.Contains("Beto"))
                throw new Exception("Falta el arte o el objetivo público de Zahira Flair.");
        }
        GD.Print("PVP_ZAHIRA_PASS · receta, selección privada, arte y objetivo público.");
    }

    internal async Task CheckSuperMagnetLayout()
    {
        const string artPath = "res://assets/cards/super-magnet-clean.png";
        if (!ResourceLoader.Exists(artPath) || GD.Load<Texture2D>(artPath) == null)
            throw new Exception("Falta el arte limpio de Súper Imán.");

        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = 1, Bonuses = [CardCatalog.Attack.Id, CardCatalog.Attack.Id],
            Hands = [[CardCatalog.TimeBomb.Id], [CardCatalog.Pendant.Id, CardCatalog.Attack.Id]],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()], SuperMagnetCopies = [1, 1]
        }, new SeededRandom(31));
        game.State.Phase = Phase.Actions;
        if (!game.Exchange(1, 0).Success || !game.Exchange(1, 0).Success)
            throw new Exception("No se prepara el canje de Súper Imán.");

        async Task Present(int seat)
        {
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 380));
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            await Present(0);
            if (PresentsKind(DuelMoveKind.TrapCraft) || !_selection.Text.Contains("rival"))
                throw new Exception("La creación se muestra como jugada del rival.");
            await Present(1);
            var choices = _moveButtons.Where(button => PresentedOption(button)?.Move.Kind == DuelMoveKind.TrapCraft).ToArray();
            if (!choices.Select(button => button.Text).SequenceEqual(["Crear Súper Imán", "Pasar sin crear"]) ||
                _contextTitle.Text != CardCatalog.SuperMagnet.Name || _contextCost.Text != "Canje de un Equipo y otra carta" ||
                !_inspector.ClassificationLabel.Text.Contains("Código Trampa"))
                throw new Exception("Falta la elección o la receta de Súper Imán.");
            choices[0].Disabled = false;
            GuiInputTestDriver.Click(choices[0]);
        }

        if (!game.ResolveTrapCraft(1, true).Success) throw new Exception("No se coloca Súper Imán.");
        game.State.CurrentPlayer = 0;
        if (!game.PlayCode(0, 0).Success || !game.ResolveDestruction(0, 1, FieldZone.Bonus).Success)
            throw new Exception("No se prueba la sustitución de Bomba de Tiempo.");
        foreach (var seat in new[] { 0, 1 })
        {
            await Present(seat);
            var equipment = (ZoneCardButton)ZoneNode(seat == 1 ? 0 : 1, CardZone.Equipment, -1)!;
            if (equipment.Card?.Id != CardCatalog.SuperMagnet.Id || equipment.StatusText != "1/3")
                throw new Exception("El equipo no muestra públicamente el contador de Súper Imán.");
            SelectEquipment(1);
            if (_inspector.CounterLabel.Text != "Contadores: 1/3")
                throw new Exception("El inspector omite los contadores de Súper Imán.");
        }
        GD.Print("PVP_SUPER_MAGNET_PASS · decisión privada, receta, arte y contadores públicos.");
    }

    internal async Task CheckGoldenTotemLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/golden-totem-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Tótem de Oro.");
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([4]);
            var hands = new List<string>[] { [CardCatalog.Attack.Id], [CardCatalog.Attack.Id] };
            hands[actor] = [CardCatalog.GoldenTotem.Id, CardCatalog.Totem.Id, CardCatalog.Totem.Id, CardCatalog.Attack.Id];
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = hands,
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            foreach (var p in game.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
            var owner = game.State.Active; owner.Equipment = CardCatalog.Pc.Id; owner.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Reverse.Id]);
            game.State.Phase = Phase.Actions;
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No empieza Tótem de Oro.");
            var cues = new List<DuelVisualEvent>(); string? previousSaved = null;
            for (var step = 0; step < 5; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random); owner = game.State.Players[actor];
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); DuelSnapshot? previous = null;
                    if (step == 2)
                    {
                        previous = DuelSnapshot.For(GameEngine.FromSnapshot(previousSaved!, random), seat, 269);
                        PresentTestSnapshot(previous);
                        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    var current = DuelSnapshot.For(game, seat, (ulong)(270 + step)); current.Visuals = cues;
                    if (previous != null && CaptureEquipmentInstall(current, previous) != null)
                        throw new Exception("Tótem de Oro duplica el vuelo de un equipo que ya tiene su propia animación.");
                    var origins = CaptureOrigins(current, previous); PresentTestSnapshot(current);
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var relative = seat == actor ? 0 : 1;
                    var cover = _root!.FindChild($"EquipmentCoverZone{relative}", true, false) as ZoneCardButton;
                    if ((cover != null) != (step is 2 or 3) || cover != null &&
                        (cover.Card?.Id != CardCatalog.GoldenTotem.Id || cover.CardRect.Size.Y < cover.Size.Y - 9))
                        throw new Exception("Tótem de Oro no muestra su cobertura pública independiente.");
                    if (step == 0 && seat == actor)
                    {
                        var choices = current.Options.Where(o => o.Move.Kind == DuelMoveKind.GoldenTotem).ToArray();
                        if (choices.Length != 3 || !_selection.Text.Contains("hasta dos")) throw new Exception("Oro no explica la selección de cero a dos copias.");
                        SelectCard(0); SelectCard(1);
                        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        if (_selected.Count != 2 || _moveButtons.Count(b => ReferenceEquals(PresentedOption(b), choices.Single(o => o.RequiredDiscards == 2))) != 1)
                            throw new Exception("No se pueden seleccionar dos Tótems distintos sin descartar.");
                    }
                    if (step == 1)
                    {
                        if (current.Players[actor].Hand[0] != CardCatalog.Totem.Id || current.Players[actor].Hand[1] != CardCatalog.Totem.Id ||
                            seat != actor && current.Players[actor].Hand[2] != null)
                            throw new Exception("Oro revela cartas ajenas a las copias mostradas.");
                    }
                    else if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Oro deja una mano revelada tras colocar los Tótems.");
                    if (step >= 2 && current.Players[actor].EquipmentStackedCopies != 1)
                        throw new Exception("La cobertura sustituida de Oro consume un Tótem de Salvación.");
                    if (step == 2)
                    {
                        if (cues.Count(c => c.Kind == DuelVisualKind.Pile && c.Destination == CardZone.Equipment) != 2 ||
                            cues.Count(c => c.Kind == DuelVisualKind.Pile && c.Destination == CardZone.EquipmentCover) != 1)
                            throw new Exception("Oro no anima las colocaciones elegidas.");
                        Animate(cues, origins);
                        for (var i = 0; i < 20; i++) { UpdateFlights(0.5); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                        if (IsAnimating) throw new Exception("Oro deja vuelos pendientes al colocar cartas.");
                    }
                    if (step == 4 && current.Players[actor].Health != 5) throw new Exception("Oro cancela un daño no letal que debía aplicarse.");
                    var minimum = _root.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Oro desborda {size}: {minimum}.");
                }
                previousSaved = game.SaveSnapshot(); cues = DuelVisualEvents.Capture(game);
                if (step == 0 && !game.ResolveGoldenTotem(actor, [0, 1]).Success) throw new Exception("No se muestran las dos copias con Oro.");
                if (step == 1 && !game.ResolveDestructionProtection(actor).Success) throw new Exception("No se sustituye el equipo anterior.");
                if (step == 2)
                {
                    game.State.CurrentPlayer = 1 - actor; game.State.Phase = Phase.Combat; game.State.Turn = 2;
                    game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
                    if (!game.Attack(1 - actor, 0, 0).Success) throw new Exception("No se ataca la pila protegida por Oro.");
                }
                if (step == 3 && !game.ResolveDestructionProtection(actor).Success) throw new Exception("No se destruye Oro en lugar de Salvación.");
            }
        }
        _selected.Clear();
        GD.Print("PVP_GOLDEN_TOTEM_LAYOUT_PASS · selección de hasta dos, revelación limitada, cobertura pública, sustitución, animaciones y reconexión en cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckTotemLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/salvation-totem-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Tótem de Salvación.");
        foreach (var owner in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([4, 4]); var attacker = 1 - owner;
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = attacker,
                Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            foreach (var p in game.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
            game.State.Turn = 2;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id);
            var target = game.State.Players[owner]; target.Equipment = CardCatalog.Totem.Id; target.EquipmentStackedCopies = 2; target.Health = 3;
            var cues = new List<DuelVisualEvent>();
            for (var step = 0; step < 4; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random); target = game.State.Players[owner];
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations();
                    var snapshot = DuelSnapshot.For(game, seat, (ulong)(250 + step)); snapshot.Visuals = cues; PresentTestSnapshot(snapshot);
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var relative = seat == owner ? 0 : 1;
                    var equipment = _root!.FindChild($"EquipmentZone{relative}", true, false) as ZoneCardButton;
                    var status = step < 2 ? "×3" : step == 2 ? "×2" : "";
                    if (equipment?.StatusText != status || snapshot.Stage != Phase.Combat || snapshot.Players[owner].Health != (step == 3 ? 5 : 3))
                        throw new Exception("Tótem pierde la pila, etapa o vida al reconstruir el tablero.");
                    if (step == 1)
                    {
                        var options = snapshot.Options.Where(o => o.Move.Kind == DuelMoveKind.Totem).ToArray();
                        if (snapshot.DecisionPlayer != owner || options.Length != (owner == seat ? 2 : 0) ||
                            options.Any(o => _moveButtons.Count(b => ReferenceEquals(PresentedOption(b), o)) != 1))
                            throw new Exception("Tótem no presenta la decisión opcional al dueño correcto.");
                        if (snapshot.Options.Any(o => o.Move.Kind == DuelMoveKind.Advance)) throw new Exception("Se permite saltar la protección del Tótem.");
                    }
                    if (step is 2 or 3 && (cues.Count(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.Equipment) != 1 ||
                        cues.Count(c => c.Kind == DuelVisualKind.Hit) != (step == 3 ? 1 : 0)))
                        throw new Exception("La protección del Tótem duplica la destrucción o anima daño cancelado.");
                    var minimum = _root.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Tótem desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Tótem revela una mano privada.");
                }
                cues = DuelVisualEvents.Capture(game);
                if (step == 0 && !game.Attack(attacker, 0, 0).Success) throw new Exception("No comienza el ataque al Tótem.");
                if (step == 1 && !game.ResolveTotem(owner, true).Success) throw new Exception("No se cancela el daño con Tótem.");
                if (step == 2)
                {
                    target.Health = 8; game.State.AttackedThisTurn = false; game.State.Active.Hand.Add(CardCatalog.Attack.Id);
                    if (!game.Attack(attacker, 0, 0).Success) throw new Exception("No se aplica el daño no letal a la pila.");
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_TOTEM_LAYOUT_PASS · pila pública, elección opcional, consumo de una copia, daño y cancelación, reconexión, privacidad y cuatro tamaños para ambos jugadores/asientos.");
    }

    internal async Task CheckTimeBenderLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/time-bender-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Doblador de Tiempo.");
        foreach (var actor in new[] { 0, 1 })
        foreach (var stage in new[] { Phase.Draw, Phase.Combat, Phase.Actions, Phase.Final })
        {
            var random = new SeededRandom(89);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            game.State.Phase = stage;
            game.State.Active.Hand.AddRange([CardCatalog.TimeBender.Id, CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id, CardCatalog.Attack.Id]);
            game.State.AttackedThisTurn = true; game.State.LearnedThisTurn = true;
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No empieza Doblador de Tiempo.");
            for (var step = 0; step < 4; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(230 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (_snapshot!.Stage != stage) throw new Exception("Doblador presenta una etapa incorrecta.");
                    if (step < 2)
                    {
                        if (_snapshot!.ActivationDiscardsRemaining != 2 - step || _snapshot.DiscardActivationCard != CardCatalog.TimeBender.Id)
                            throw new Exception("Doblador pierde la cantidad o el origen del coste.");
                        if (seat == actor)
                        {
                            if (!_selection.Text.Contains((2 - step).ToString()) || !_contextTitle.Text.Contains(CardCatalog.TimeBender.Name))
                                throw new Exception("Doblador no explica los descartes pendientes.");
                            SelectCard(0);
                            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                            var label = _snapshot.Options.Single(o => o.Move.Kind == DuelMoveKind.TurnBuffDiscard && o.Move.Index == 0).Label;
                            if (_moveButtons.Count(b => PresentedOption(b)?.Label == label) != 1 || _snapshot.Options.Any(o => o.Move.Kind == DuelMoveKind.Advance))
                                throw new Exception("Doblador permite saltar el coste o selecciona otra carta.");
                        }
                        else if (_snapshot.Options.Any(o => o.Move.Kind == DuelMoveKind.TurnBuffDiscard))
                            throw new Exception("El rival puede pagar el coste de Doblador.");
                    }
                    else if (seat == actor && !_snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Advance))
                        throw new Exception("No se presenta el avance de la etapa repetida.");
                    var minimum = _root!.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Doblador desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Doblador revela cartas privadas.");
                }
                if (step < 2 && !game.ResolveTurnBuffDiscard(actor, 0).Success) throw new Exception("No se paga un descarte de Doblador.");
                if (step == 2 && stage != Phase.Final && !game.Advance(actor).Success) throw new Exception("No empieza la etapa repetida.");
                if (step == 3 && (game.State.Phase != stage || game.State.CurrentPlayer != actor || game.State.Turn != 1))
                    throw new Exception("La repetición cambia de etapa, turno o jugador.");
            }
        }
        _selected.Clear();
        GD.Print("PVP_TIME_BENDER_LAYOUT_PASS · dos descartes, cuatro etapas, repetición, reconexión, privacidad, ambos jugadores y cuatro tamaños.");
    }

    internal async Task CheckBlueEggDrawLayout()
    {
        var game = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[CardCatalog.BlueEgg.Id, CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()],
            Bonuses = [CardCatalog.Pendant.Id, CardCatalog.Senshuni.Id]
        }, new SeededRandom(7));
        game.State.Phase = Phase.Actions; game.State.Turn = 3; game.State.Players[0].Relikia = 300;
        if (!game.PlayCode(0, 0).Success || !game.ResolveDestruction(0, 1, FieldZone.Bonus, 0).Success || game.State.Phase != Phase.BlueEggDrawChoice)
            throw new Exception("No se prepara el robo opcional de Huevo Azul Explosivo.");
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size;
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 520));
            for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var choices = _moveButtons.Where(b => PresentedOption(b)?.Move.Kind == DuelMoveKind.BlueEggDraw).ToArray();
            if (seat == 1)
            {
                if (choices.Length != 0) throw new Exception("El rival puede decidir el robo de Huevo Azul Explosivo.");
                continue;
            }
            var view = _movesScroll.GetGlobalRect().Grow(1);
            if (choices.Length != 2 || choices.Any(b => !b.IsVisibleInTree() || !view.Encloses(b.GetGlobalRect())))
                throw new Exception($"Huevo Azul Explosivo no muestra a la vez pagar y continuar en {size}.");
            var minimum = _root!.GetCombinedMinimumSize();
            if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                throw new Exception($"Huevo Azul Explosivo desborda {size}: {minimum}.");
            if (directory != null && DisplayServer.GetName() != "headless")
            {
                System.IO.Directory.CreateDirectory(directory);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"blue-egg-draw-{size.X}x{size.Y}.png"));
            }
        }
        GD.Print("PVP_BLUE_EGG_DRAW_LAYOUT_PASS · pagar y continuar visibles a la vez, solo para quien decide, en cuatro tamaños.");
    }

    internal async Task CheckReloadCartridgeLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/reload-cartridge-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Cartucho de Re-carga.");
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new SeededRandom(81); var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            game.State.Phase = Phase.Actions; game.State.Turn = 2;
            foreach (var p in game.State.Players) p.Dexterity = 0;
            game.State.Active.Hand.AddRange([CardCatalog.ReloadCartridge.Id, CardCatalog.Sniper.Id, CardCatalog.Senshuni.Id]);
            game.State.Players[1 - actor].Hand.AddRange([CardCatalog.Sniper.Id, CardCatalog.Jessica.Id]);
            string? previousSaved = null; var cues = new List<DuelVisualEvent>();
            for (var step = 0; step < 4; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations();
                    DuelSnapshot? previous = null;
                    if (step == 1)
                    {
                        previous = DuelSnapshot.For(GameEngine.FromSnapshot(previousSaved!, random), seat, 209);
                        PresentTestSnapshot(previous);
                        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }
                    var current = DuelSnapshot.For(game, seat, (ulong)(210 + step)); current.Visuals = cues;
                    var origins = CaptureOrigins(current, previous);
                    PresentTestSnapshot(current);
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (current.Stage != Phase.Actions) throw new Exception("Cartucho presenta una etapa ajena a acciones.");
                    if (step is 1 or 2)
                    {
                        var decision = step == 1 ? actor : 1 - actor;
                        if (current.DecisionPlayer != decision || _handCards.Count != 0 ||
                            _moveButtons.Count(b => PresentedOption(b)?.Move is { Kind: DuelMoveKind.Sniper, Flag: true }) != (seat == decision ? 1 : 0))
                            throw new Exception("Cartucho no conserva el dueño del descarte o roba antes de las Miras.");
                    }
                    if (step == 1)
                    {
                        var packets = origins.Where(p => p.Key.Kind == DuelVisualKind.HandDiscard).ToArray();
                        if (packets.Length != 2 || packets.Any(p => p.Value.Face != null))
                            throw new Exception("El descarte de manos pierde un origen o revela identidades ocultas en la animación.");
                    }
                    if (step is 1 or 3)
                    {
                        // Both hands empty and refill at the same time: each flight launches with its rival's mirror.
                        Animate(cues, origins);
                        var pairs = new HashSet<object>();
                        for (var i = 0; i < 24; i++)
                        {
                            UpdateFlights(0.3);
                            if (_activeFlight != null && _companion is { } pair && pair.Flight.Event.Kind == _activeFlight.Event.Kind &&
                                pair.Flight.Event.Player != _activeFlight.Event.Player) pairs.Add(pair);
                            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        }
                        if (IsAnimating) throw new Exception("El descarte o el robo de Cartucho deja vuelos pendientes.");
                        if (size == new Vector2I(1440, 1024) && pairs.Count != (step == 1 ? 1 : 4))
                            throw new Exception($"Cartucho anima a los jugadores por turnos en lugar de a la vez ({pairs.Count} pares, paso {step}).");
                    }
                    if (step == 3 && (_handCards.Count != 4 || current.Players.Any(p => p.HandCount != 4)))
                        throw new Exception("Cartucho no presenta las cuatro cartas nuevas de cada jugador.");
                    var minimum = _root!.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Cartucho desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Cartucho revela las manos nuevas al adversario.");
                }
                if (step < 3)
                {
                    previousSaved = game.SaveSnapshot(); cues = DuelVisualEvents.Capture(game);
                    var result = step == 0 ? game.PlayCode(actor, 0) : game.ResolveSniper(step == 1 ? actor : 1 - actor, true);
                    if (!result.Success) throw new Exception("No se resuelve un paso de Cartucho: " + result.Message);
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_RELOAD_CARTRIDGE_LAYOUT_PASS · descarte de ambas manos, animación sin identidades, disparadores, reconexión y cuatro robos privados para ambos asientos y cuatro tamaños.");
    }

    internal async Task CheckQuickLoadLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/quick-load-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Carga Rápida.");
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([1]);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [["attack"], ["attack"]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            foreach (var p in game.State.Players) p.Dexterity = 0;
            game.State.Phase = Phase.Actions; var owner = game.State.Active;
            owner.Hand[0] = CardCatalog.QuickLoad.Id; owner.Deck[1] = CardCatalog.Senshuni.Id; owner.Equipment = CardCatalog.Pendant.Id;
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No empieza Carga Rápida.");
            for (var step = 0; step < 4; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(180 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (step == 0 && (_snapshot!.Die != 2 || !_snapshot.DieCaption.Contains("Carga Rápida") || game.State.Players[actor].Hand.Count != 2))
                        throw new Exception("Carga Rápida pierde el dado o no presenta los dos robos iniciales.");
                    if (step is 1 or 2 && actor == seat)
                    {
                        if (_snapshot!.DiscardsRemaining != 3 - step || !_selection.Text.Contains((3 - step).ToString()))
                            throw new Exception("La interfaz no presenta los descartes pendientes.");
                        var index = _handCards.Count - 1; SelectCard(index);
                        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        var label = _snapshot.Options.Single(o => o.Move.Kind == DuelMoveKind.Discard && o.Move.Index == index).Label;
                        if (_moveButtons.Count(b => PresentedOption(b)?.Label == label) != 1 || _snapshot.Options.Any(o => o.Move.Kind == DuelMoveKind.Advance))
                            throw new Exception("La selección de descarte no corresponde a la carta o permite saltar el efecto.");
                    }
                    var minimum = _root!.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Carga Rápida desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Carga Rápida revela los robos al adversario.");
                }
                if (step == 0 && !game.ResolveDieReroll(actor, false).Success) throw new Exception("No se acepta el dado de Carga Rápida.");
                if (step is 1 or 2 && !game.Discard(actor, game.State.Active.Hand.Count - 1).Success)
                    throw new Exception("No se resuelve el descarte de Carga Rápida.");
            }
        }
        _selected.Clear();
        GD.Print("PVP_QUICK_LOAD_LAYOUT_PASS · robo, dado y descartes elegidos, cantidades pendientes, reconexión y privacidad en cuatro tamaños y ambos asientos.");
    }

    internal async Task CheckJessicaLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/jessica-lee-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Jessica Lee.");
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            foreach (var p in game.State.Players) p.Dexterity = 0;
            var owner = game.State.Active; owner.Bonus = CardCatalog.Jessica.Id; owner.Exchange.Add(CardCatalog.Jessica.Id);
            var rival = game.State.Players[1 - actor]; rival.Bonus = CardCatalog.Magnet.Id; rival.Exchange.Add(CardCatalog.Magnet.Id);
            owner.Equipment = CardCatalog.Pendant.Id; owner.ActionsLeft = 0;
            for (var step = 0; step < 4; step++)
            {
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(150 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var relative = actor == seat ? 0 : 1;
                    var track = _root!.FindChild($"BonusTrack{relative}", true, false) as ExchangeTrack;
                    if (track == null || track.BonusButton.EffectUsed != (step is 1 or 2) ||
                        track.ExchangeButtons[0].EffectUsed != (step == 2))
                        throw new Exception("Jessica no presenta correctamente el uso de cada copia pública.");
                    var exchange = track.ExchangeButtons[0];
                    var otherTrack = (ExchangeTrack)_root.FindChild($"BonusTrack{1 - relative}", true, false);
                    foreach (var card in new[] { exchange, otherTrack.ExchangeButtons[0] })
                    {
                        var art = card.CardRect;
                        if (art.Size.X <= art.Size.Y || art.Size.Y <= card.Size.Y - 40 ||
                            !new Rect2(Vector2.Zero, card.Size).Encloses(art))
                            throw new Exception($"La carta de canje {card.Card!.Name} no muestra una miniatura horizontal ampliada en {size}.");
                    }
                    foreach (var bonus in new[] { track.BonusButton, otherTrack.BonusButton })
                    {
                        var art = bonus.CardRect;
                        if (art.Size.X >= art.Size.Y || art.Size.Y < bonus.Size.Y - 9 || !new Rect2(Vector2.Zero, bonus.Size).Encloses(art))
                            throw new Exception($"El Bonus {bonus.Card!.Name} no ocupa el espacio vertical disponible en {size}.");
                    }
                    if (!track.ExchangeButtons[1].Disabled)
                        throw new Exception("Una ranura de canje vacía se puede seleccionar.");
                    if ((track.BonusButton.Targetable ? 1 : 0) + (track.ExchangeButtons[0].Targetable ? 1 : 0) != (step == 0 && actor == seat ? 2 : 0))
                        throw new Exception("Jessica no ilumina sus zonas al dueño en su combate, o permite repetir el efecto.");
                    if (step == 0 && actor == seat)
                    {
                        SelectField(actor, FieldZone.Bonus, -1);
                        if (!_moveButtons.Any(b => PresentedOption(b)?.Move is { Kind: DuelMoveKind.FieldCode, Value: (int)FieldZone.Bonus }))
                            throw new Exception("Jessica no presenta la activación de la copia tocada.");
                        ClearContext();
                        _arenaScroll?.EnsureControlVisible(exchange);
                        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                        GuiInputTestDriver.Click(exchange);
                        if (!_moveButtons.Any(b => PresentedOption(b)?.Move is { Kind: DuelMoveKind.FieldCode, Value: (int)FieldZone.Exchange, Index: 0 }))
                            throw new Exception("Jessica no presenta la activación desde su miniatura horizontal de canje.");
                        ClearContext();
                    }
                    if (step == 1 && _snapshot!.Die != 5) throw new Exception("La reconexión pierde el dado pendiente de Jessica.");
                    var minimum = _root.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Jessica desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Jessica revela la carta robada al adversario.");
                }
                if (step == 0)
                {
                    random.Replay([4]);
                    if (!game.UseFieldCode(actor, FieldZone.Bonus).Success) throw new Exception("No se activa Jessica desde Bonus.");
                }
                if (step == 1)
                {
                    if (!game.ResolveDieReroll(actor, false).Success) throw new Exception("No se conserva el cinco de Jessica.");
                    random.Replay([5]);
                    if (!game.UseFieldCode(actor, FieldZone.Exchange, 0).Success || !game.ResolveDieReroll(actor, false).Success)
                        throw new Exception("No se activa la segunda copia de Jessica.");
                }
                if (step == 2 && (!game.Advance(actor).Success || !game.Advance(actor).Success))
                    throw new Exception("No se reinician los usos de Jessica al pasar de turno.");
            }
        }
        _selected.Clear();
        GD.Print("PVP_JESSICA_LAYOUT_PASS · activaciones desde canje y Bonus, dado y usos públicos, reconexión, privacidad y cuatro tamaños para ambos asientos.");
    }

    internal async Task CheckExchangeAnimation()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        static GameEngine Fixture(int actor)
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = actor,
                Hands = [[CardCatalog.Sniper.Id, CardCatalog.Magnet.Id], [CardCatalog.Sniper.Id, CardCatalog.Magnet.Id]],
                Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 4).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 4).ToList()],
                Bonuses = [CardCatalog.Jessica.Id, CardCatalog.Jessica.Id]
            }, new SeededRandom(31));
            game.State.Phase = Phase.Actions;
            return game;
        }

        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        foreach (var actor in new[] { 0, 1 })
        {
            var game = Fixture(actor);
            GetWindow().ContentScaleSize = size;
            await Layout();
            var relative = actor == seat ? 0 : 1;
            for (var index = 0; index < 2; index++)
            {
                ResetAnimations();
                var before = DuelSnapshot.For(game, seat, (ulong)(870 + index));
                PresentTestSnapshot(before);
                if (actor == seat) SelectCard(0);
                await Layout();
                var source = actor == seat ? ZoneNode(0, CardZone.Hand, 0) : null;
                var face = game.State.Active.Hand[0];
                var cues = DuelVisualEvents.Capture(game);
                Check(new DuelMove { Kind = DuelMoveKind.Exchange, Index = 0 }.Apply(game, actor).Success, "No se canjea la carta de prueba.");
                var after = DuelSnapshot.For(game, seat, before.Revision + 1);
                after.Visuals = cues;
                var origins = CaptureOrigins(after, before);
                var placement = CaptureExchangePlacement(after, before, origins);
                Check(placement?.Face == face && placement.Event.Source == CardZone.Hand && placement.Event.Value == index,
                    "El canje no prepara el vuelo desde la mano hasta la ranura correcta.");
                if (source != null && RectVisible(source, ZoneRect(source)))
                    Check(placement!.Origin == ZoneRect(source), "El vuelo de canje comienza en otra carta de la mano.");

                PresentTestSnapshot(after);
                Animate(cues, origins, placement);
                var tile = (ZoneCardButton)ZoneNode(relative, CardZone.Exchange, index)!;
                var first = (ZoneCardButton)ZoneNode(relative, CardZone.Exchange, 0)!;
                var bonus = index == 1 ? ZoneNode(relative, CardZone.Hand, 0) : null;
                Check(tile.AwaitingExchange && _flights.Peek() == placement && (index == 0 || first.HasExchangePreview),
                    "El tablero adelanta el resultado antes de colocar la carta de canje.");
                Check(bonus == null || bonus.Modulate.A == 0, "El Bonus aparece en la mano antes de completar el canje.");
                await Layout();
                UpdateFlights(0); UpdateFlights(0); UpdateFlights(0.04);
                Check(_activeFlight == placement && _flightTarget == tile && _flightFront?.Visible == (actor == seat),
                    "La colocación pierde su destino o adelanta la cara de la carta rival.");
                if (size.X == 1440)
                    Check(!_placementPreview && (index == 1 || _flightDestination == PortraitFootprint(ZoneRect(tile))),
                        "El vuelo de escritorio no llega al espacio horizontal de canje.");
                var scroll = _arenaScroll?.ScrollVertical;
                UpdateFlights(0.49);
                Check(_flightFront!.Visible && Math.Abs(_flight!.Rotation - Mathf.Pi / 2) < 0.12f && tile.AwaitingExchange,
                    "La carta no gira a horizontal antes de aterrizar.");
                UpdateFlights(0.9);
                Check(_activeFlight == null && !tile.AwaitingExchange && _arenaScroll?.ScrollVertical == scroll,
                    "La carta no aterriza o desplaza el tablero.");
                if (index == 0) Check(tile.Card?.Id == face && !IsAnimating, "No aparece la primera carta en el canje.");
                else
                {
                    Check(tile.HasExchangePreview && _flights.Peek().LeavesExchange,
                        "La segunda carta desaparece antes de la salida hacia memoria.");
                    for (var frame = 0; IsAnimating && frame < 16; frame++) UpdateFlights(0.5);
                    Check(!first.HasExchangePreview && !tile.HasExchangePreview && !IsAnimating && (bonus == null || bonus.Modulate.A == 1),
                        "El canje no retira ambas cartas o no entrega el Bonus.");
                    Animate(cues, origins, placement);
                    UpdateFlights(0); UpdateFlights(0); UpdateFlights(0.04);
                    ResetAnimations();
                    Check(!tile.AwaitingExchange && !first.HasExchangePreview && !IsAnimating && (bonus == null || bonus.Modulate.A == 1),
                        "Interrumpir el canje deja una carta oculta o una animación pendiente.");
                }
            }
        }

        var bonusGame = Fixture(0);
        bonusGame.State.Active.Relikia = 400;
        var bonusBefore = DuelSnapshot.For(bonusGame, 0, 880);
        Check(bonusGame.UseRelikiaEffect(0, RelikiaEffect.ChangeBonus).Success, "No se prepara el cambio de Bonus.");
        var bonusFlight = CaptureExchangePlacement(DuelSnapshot.For(bonusGame, 0, 881), bonusBefore, []);
        Check(bonusFlight?.Event.Source == CardZone.Bonus && bonusFlight.Face == CardCatalog.Jessica.Id,
            "La carta de Relikia no sale del Bonus.");
        Check(bonusGame.UseRelikiaEffect(0, RelikiaEffect.DrawAndPlace).Success, "No se prepara el robo de Relikia.");
        var relikiaBefore = DuelSnapshot.For(bonusGame, 1, 882);
        var relikiaCues = DuelVisualEvents.Capture(bonusGame);
        Check(new DuelMove { Kind = DuelMoveKind.Placement, Index = 0, Value = (int)CardDestination.Exchange }.Apply(bonusGame, 0).Success,
            "La carta robada no completa el canje de Relikia.");
        var relikiaAfter = DuelSnapshot.For(bonusGame, 1, 883);
        relikiaAfter.Visuals = relikiaCues;
        var relikiaFlight = CaptureExchangePlacement(relikiaAfter, relikiaBefore, []);
        Check(relikiaFlight?.Event.Source == CardZone.Hand && relikiaFlight.Face == CardCatalog.Sniper.Id,
            "El rival no puede ver la carta pública que completa el canje de Relikia.");

        var sniper = Fixture(0);
        sniper.State.Phase = Phase.Discard;
        sniper.State.DiscardPlayer = 0;
        sniper.State.DiscardsRemaining = 1;
        sniper.State.DiscardResumePhase = Phase.Actions;
        Check(sniper.Discard(0, 0).Success, "No se prepara Mira de Sniper en memoria.");
        var sniperBefore = DuelSnapshot.For(sniper, 1, 882);
        Check(sniper.ResolveSniper(0, false).Success, "No se canjea Mira de Sniper.");
        var sniperFlight = CaptureExchangePlacement(DuelSnapshot.For(sniper, 1, 883), sniperBefore, []);
        Check(sniperFlight?.Event.Source == CardZone.Memory && sniperFlight.Face == CardCatalog.Sniper.Id,
            "Mira de Sniper no sale de memoria al canje.");
        GD.Print("PVP_EXCHANGE_ANIMATION_PASS · mano, giro, privacidad, segundo canje, Bonus, memoria y cuatro tamaños desde ambos asientos.");
    }

    internal async Task CheckPlumberStarLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/plumber-star-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Estrella del Fontanero.");
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([4]);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = 1 - actor,
                Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 20).ToList(), Enumerable.Repeat("attack", 20).ToList()], Bonuses = [null, null]
            }, random);
            foreach (var p in game.State.Players) { p.Dexterity = 0; p.AttackRerollUsedThisTurn = true; }
            game.State.Turn = 2;
            game.State.Active.Skills.Add(CardCatalog.Destiny.Id); game.State.Active.Hand.Add(CardCatalog.LoadError.Id);
            var owner = game.State.Players[actor]; owner.Hand.Clear();
            owner.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]);
            if (!game.Attack(1 - actor, 0, 0).Success || !game.PlayCode(actor, 0).Success)
                throw new Exception("No comienza Estrella como defensa COMBO.");
            for (var step = 0; step < 4; step++)
            {
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(120 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var relative = actor == seat ? 0 : 1;
                    var badge = _root!.FindChild($"StarEffects{relative}", true, false) as Label;
                    if ((badge != null) != (step == 2) || (badge != null && !badge.Text.Contains("×2")))
                        throw new Exception("Estrella no presenta su efecto público o conserva la marca después de vencer.");
                    if (step == 1 && actor == seat)
                    {
                        if (_handCards.Count != 2) throw new Exception("El coste no muestra la mano restante.");
                        SelectCard(0);
                        var label = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.TurnBuffDiscard && o.Move.Index == 0).Label;
                        if (_moveButtons.Count(b => PresentedOption(b)?.Label == label) != 1 || !_selection.Text.Contains(CardCatalog.Senshuni.Name))
                            throw new Exception("La selección de descarte no corresponde a su opción autorizada.");
                    }
                    var minimum = _root.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Estrella desborda {size}: {minimum}.");
                    if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                        throw new Exception("Estrella revela una mano privada.");
                }
                if (step == 0 && !game.PassErrorResponse(1 - actor).Success) throw new Exception("No termina la respuesta de Error.");
                if (step == 1 && !game.ResolveTurnBuffDiscard(actor, 0).Success) throw new Exception("No se paga el descarte de Estrella.");
                if (step == 2 && (!game.PassDefense(actor).Success || !game.Advance(1 - actor).Success || !game.Advance(1 - actor).Success))
                    throw new Exception("No vence Estrella al acabar el turno rival.");
            }
        }
        _selected.Clear();
        GD.Print("PVP_PLUMBER_STAR_LAYOUT_PASS · coste, respuesta, inmunidad y multiplicador públicos, vencimiento, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckMemoryRecoveryLayout()
    {
        if (GD.Load<Texture2D>("res://assets/cards/floppy-disk-clean.png") == null)
            throw new Exception("Falta la ilustración limpia de Disquete.");
        foreach (var actor in new[] { 0, 1 })
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
                Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
                Decks = [Enumerable.Repeat("attack", 12).ToList(), Enumerable.Repeat("attack", 12).ToList()], Bonuses = [null, null]
            }, new SeededRandom(43));
            game.State.Phase = Phase.Actions; game.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
            game.State.Active.Memory.AddRange([CardCatalog.FloppyDisk.Id, CardCatalog.Destiny.Id]);
            game.State.Players[1 - actor].Memory.AddRange([CardCatalog.FloppyDisk.Id, CardCatalog.Bonfire.Id]);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No comienza la recuperación de Disquete.");
            var saved = game.SaveSnapshot();
            foreach (var size in new[] { new Vector2I(1820, 1024), new(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                game = GameEngine.FromSnapshot(saved, new SeededRandom(43));
                var rivalMemory = game.State.Players[1 - actor].Memory;
                if (size.X == 1820) rivalMemory.InsertRange(1, Enumerable.Repeat(CardCatalog.Attack.Id, 24));
                var targetSlot = rivalMemory.Count - 1;
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); var before = DuelSnapshot.For(game, seat, 100); PresentTestSnapshot(before);
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var recovery = new DuelMove { Kind = DuelMoveKind.RecoverMemory, Target = 1 - actor, Index = targetSlot };
                if (seat == actor)
                {
                    if (_handCards.Count != 2 + rivalMemory.Count || _deckSearchOptions.Count(o => o.CardId == CardCatalog.FloppyDisk.Id) != 2 ||
                        _deckSearchOptions.Any(o => o.Move.Target == actor && o.Move.Index == 2))
                        throw new Exception("Disquete no distingue las otras copias de su copia activa.");
                    var titles = _hand!.FindChildren("HandSectionTitle*", "Label", true, false).OfType<Label>().ToArray();
                    if (titles.Length != 2 || !titles.Any(t => t.Text.Contains("TU MEMORIA")) || !titles.Any(t => t.Text.Contains("MEMORIA DE")))
                        throw new Exception("Disquete no separa visualmente las dos memorias.");
                    if (_handCards.Count(c => c.OriginalOwner?.Seat == actor) != 2 || _handCards.Count(c => c.OriginalOwner?.Seat == 1 - actor) != rivalMemory.Count ||
                        _handCards.Single(c => c.Card.Id == CardCatalog.Bonfire.Id).OriginalOwner?.Seat != 1 - actor)
                        throw new Exception("Las cartas de memoria no muestran a su dueño original.");
                    if (_handCards.Any(c => c.ShowOwnerBadge))
                        throw new Exception("Los bloques de memoria repiten el distintivo de rival.");
                    Rect2? footerBounds = null;
                    if (Desktop)
                    {
                        var footer = _moves.GetChildren().OfType<Button>().SingleOrDefault(b => b.Text == "Recuperar");
                        if (footer is not { Disabled: true } || !RectVisible(footer, footer.GetGlobalRect()) ||
                            !new Rect2(GlobalPosition, Size).Grow(1).Encloses(footer.GetGlobalRect()))
                            throw new Exception($"Disquete no muestra el botón de confirmación al abrir el selector en {size}.");
                        footerBounds = footer.GetGlobalRect();
                    }
                    var index = Array.FindIndex(_deckSearchOptions, o => o.Move.Target == 1 - actor && o.Move.Index == targetSlot);
                    var option = _deckSearchOptions[index];
                    var card = _handCards.Single(c => c.Name == $"HandCard{index}");
                    _hand.EnsureControlVisible(card);
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (size.X == 1820 && _hand.ScrollVertical == 0)
                        throw new Exception("La prueba de Disquete no desplaza la memoria extensa.");
                    card.Disabled = false;
                    GuiInputTestDriver.Click(card);
                    // Fixed, wrapping headers settle through Godot's deferred container layout.
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    if (_moveButtons.Count(b => ReferenceEquals(PresentedOption(b), option)) != 1 || !_contextTitle.Text.Contains(CardCatalog.Bonfire.Name))
                        throw new Exception("La selección de memoria no corresponde a su opción autorizada.");
                    var confirm = PresentedButton(option)!;
                    if (!RectVisible(confirm, confirm.GetGlobalRect()) || !new Rect2(GlobalPosition, Size).Grow(1).Encloses(confirm.GetGlobalRect()))
                        throw new Exception($"Recuperar queda fuera de pantalla en {size}: botón {confirm.GetGlobalRect()}, galería {_hand.GetGlobalRect()}, pie {_decisionBar.GetGlobalRect()}.");
                    if (footerBounds is { } bounds && (!bounds.IsEqualApprox(confirm.GetGlobalRect()) ||
                        !_decisionBar.GetGlobalRect().Grow(1).Encloses(confirm.GetGlobalRect())))
                        throw new Exception("Recuperar se desplaza al elegir una carta de Disquete.");
                    confirm.Disabled = false;
                    GuiInputTestDriver.Click(confirm);
                    recovery = PresentedOption(confirm)!.Move;
                }
                else if (_handCards.Count != 1 || _deckSearchOptions.Length != 0 || _handCards[0].Card.Id != CardCatalog.Attack.Id)
                    throw new Exception("Disquete reemplaza la mano del rival por opciones ajenas.");
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Disquete desborda {size}: {minimum}.");
                var cues = DuelVisualEvents.Capture(game);
                if (!recovery.Apply(game, actor).Success) throw new Exception("No se recupera la carta elegida.");
                var after = DuelSnapshot.For(game, seat, 101); after.Visuals = cues;
                var origins = CaptureOrigins(after, before);
                if (origins.Single(p => p.Key.Kind == DuelVisualKind.MemoryClaim).Value.Face != CardCatalog.Bonfire.Id)
                    throw new Exception("La animación no toma la carta de la memoria de origen.");
                _selected.Clear(); PresentTestSnapshot(after); Animate(cues, origins);
                for (var i = 0; i < 12; i++) { UpdateFlights(0.5); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
                if (IsAnimating || FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                    throw new Exception("La recuperación deja una animación pendiente o revela la mano.");
                if (seat == actor)
                {
                    var recovered = _handCards.SingleOrDefault(c => c.Card.Id == CardCatalog.Bonfire.Id);
                    if (recovered?.OriginalOwner?.Seat != 1 - actor || !recovered.TooltipText.Contains("Dueño original"))
                        throw new Exception("La mano no identifica el dueño original de la carta recuperada.");
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_MEMORY_RECOVERY_LAYOUT_PASS · ambas memorias, copias distintas, confirmación visible y animación a mano, cinco tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckReverseLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([0]);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
                Hands = [[CardCatalog.Reverse.Id], [CardCatalog.Reverse.Id]],
                Decks = [Enumerable.Repeat("attack", 12).ToList(), Enumerable.Repeat("attack", 12).ToList()], Bonuses = [null, null]
            }, random);
            var player = game.State.Active; player.Dexterity = 0; player.Relikia = 500;
            player.Skills.AddRange([CardCatalog.Destiny.Id, CardCatalog.Magnet.Id]);
            if (!game.Learn(actor, 0).Success) throw new Exception("No empieza el aprendizaje de Reverse.");
            for (var step = 0; step < 3; step++)
            {
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(80 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var relative = actor == seat ? 0 : 1;
                    var skill = _root!.FindChild($"Skill{relative}_2", true, false) as SkillTile;
                    if (skill is not { HasSkillArtwork: true } || skill.Skill?.Id != CardCatalog.Reverse.Id || skill.Counters != (step == 0 ? 0 : 1))
                        throw new Exception("Reverse no presenta la ilustración o los aumentos públicos de esta copia.");
                    if (step == 0 && PresentsKind(DuelMoveKind.LearningDie) != (actor == seat))
                        throw new Exception("El aprendizaje no ofrece su relanzamiento al dueño correcto.");
                    if (step == 2 && _snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.ProtectDestruction && o.Move.Index >= 0 && PresentsOption(o)) != (actor == seat))
                        throw new Exception("La protección no ofrece la elección al dueño correcto.");
                    var minimum = _root.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                        throw new Exception($"Reverse desborda {size}: {minimum}.");
                }
                if (step == 0)
                {
                    random.Replay([5]);
                    if (!game.RerollLearningDie(actor).Success || !game.ResolveDieReroll(actor, false).Success)
                        throw new Exception("No se resuelven el relanzamiento y robo de Reverse.");
                }
                else if (step == 1)
                {
                    game.Advance(actor); player.Hand.Clear(); player.Hand.Add(CardCatalog.BlueEgg.Id); player.Relikia = 200; player.Equipment = CardCatalog.Pc.Id;
                    if (!game.PlayCode(actor, 0).Success || !game.ResolveDestruction(actor, actor, FieldZone.Equipment).Success)
                        throw new Exception("No se ofrece la protección de Reverse.");
                }
                else
                {
                    var cues = DuelVisualEvents.Capture(game);
                    if (!game.ResolveDestructionProtection(actor, 2).Success || player.Skills.Count != 2 || player.Equipment != CardCatalog.Pc.Id ||
                        cues.Count(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.Skill) != 1)
                        throw new Exception("El sacrificio no conserva el equipo ni mueve la habilidad a memoria.");
                }
            }
        }
        await CheckReverseVacantSlotLayout();
        GD.Print("PVP_REVERSE_LAYOUT_PASS · ilustración, aumentos, aprendizaje, protección y espacios libres, cuatro tamaños y ambos jugadores/asientos.");
    }

    private async Task CheckReverseVacantSlotLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(17); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Combat; game.State.Turn = 2;
            var p = game.State.Active; p.Skills.Add(CardCatalog.Destiny.Id); p.SkillPositions = new() { [0] = 1 }; p.SkillCounters[0] = 1;
            p.Hand.Add("attack");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 90));
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var relative = actor == seat ? 0 : 1;
                var empty = _root!.FindChild($"Skill{relative}_0", true, false) as SkillTile;
                var remaining = _root.FindChild($"Skill{relative}_1", true, false) as SkillTile;
                if (empty?.Skill != null || remaining?.Skill?.Id != CardCatalog.Destiny.Id || remaining.Counters != 1 || remaining.SkillIndex != 0)
                    throw new Exception("Sacrificar Reverse desplaza otra habilidad o pierde sus contadores.");
                if (seat == actor)
                {
                    remaining.EmitSignal(Button.SignalName.Pressed); SelectCard(0);
                    if (!_moveButtons.Any(b => PresentedOption(b)?.Move is { Kind: DuelMoveKind.Attack, Value: 0 }))
                        throw new Exception("El espacio físico no selecciona su habilidad para atacar.");
                }
            }
        }
    }

    internal async Task CheckYunoEvolutionLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var random = new DuelRandom(); random.Replay([3]);
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
                Hands = [["attack"], ["attack"]], Decks = [["attack"], ["attack"]], Bonuses = [null, null]
            }, random);
            game.State.Turn = 2;
            var p = game.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.AttackRerollUsedThisTurn = true; p.Dexterity = 0;
            p.Skills.Add(CardCatalog.Destiny.Id);
            var cues = DuelVisualEvents.Capture(game);
            if (!game.Attack(actor, 0, 0).Success) throw new Exception("No se resuelve el ataque de Yuno Lv 2.");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 70)); Animate(cues, []);
                if (!_die.IsRolling || _die.LatestResult != 5) throw new Exception("El +1 oculta o sustituye la tirada original.");
                var frames = 0;
                while (IsAnimating && frames++ < 30)
                {
                    _die.AdvancePlayback(0.5); UpdateFlights(0.5);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (IsAnimating || _die.DisplayedValue != 5 || !_die.RollCaption.Contains("+1"))
                    throw new Exception($"El dado de Yuno no presenta el aumento: animando={IsAnimating}, dado={_die.DisplayedValue}, texto={_die.RollCaption}, fotogramas={frames}.");
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Yuno Lv 2 desborda {size}: {minimum}.");
            }
        }
        GD.Print("PVP_YUNO_EVOLUTION_LAYOUT_PASS · tirada seguida del +1, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckDexterityLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(19); game.State.CurrentPlayer = actor; game.State.Turn = 1;
            game.State.Phase = Phase.AttackRerollChoice;
            game.State.Active.AttackRerollUsedThisTurn = true;
            game.State.PendingAttackRoll = new(actor, CardCatalog.Destiny.Id, 3);
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 60));
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Destreza desborda {size}: {minimum}.");
                foreach (var relative in new[] { 0, 1 })
                    if ((_root.FindChild($"Dexterity{relative}", true, false) as DexterityBadge)?.Amount != 1)
                        throw new Exception("Falta el contador público de destreza.");
                if (PresentsKind(DuelMoveKind.Dexterity) != (seat == actor))
                    throw new Exception("La opción de destreza no corresponde al dueño del dado.");
            }
            var cues = DuelVisualEvents.Capture(game);
            if (!game.UseDexterity(actor).Success) throw new Exception("No se puede usar destreza.");
            foreach (var seat in new[] { 0, 1 })
            {
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 61)); Animate(cues, []);
                var relative = actor == seat ? 0 : 1;
                if ((_root!.FindChild($"Dexterity{relative}", true, false) as DexterityBadge)?.Amount != 0 ||
                    _die.IsRolling || _die.DisplayedValue != 4 || !_die.RollCaption.Contains("Destreza"))
                    throw new Exception("Gastar destreza no actualiza la ficha y el dado sin relanzarlo.");
            }
        }
        GD.Print("PVP_DEXTERITY_LAYOUT_PASS · fichas públicas, opción del dueño y dado modificado sin relanzar, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckHandLimitLayout()
    {
        async Task Layout()
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(7); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Actions;
            game.State.Active.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 9));
            if (!game.Advance(actor).Success) throw new Exception("No se inicia el descarte final.");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                await Layout();
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 50));
                await Layout();
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Descarte final desborda {size}: {minimum}.");
                if (seat == actor)
                {
                    if (!_selection.Text.Contains("Descarta 2")) throw new Exception("Falta la indicación del límite de mano.");
                    var card = _handCards[0];
                    _hand!.EnsureControlVisible(card); await Layout();
                    card.Disabled = false;
                    GuiInputTestDriver.Click(card); await Layout();
                    var discard = _snapshot!.Options.Single(o => o.Move.Kind == DuelMoveKind.Discard && o.Move.Index == 0);
                    if (PresentedButton(discard) is not { Text: "Descartar" } button ||
                        button.GetParent() != _cardActions || !button.IsVisibleInTree() || _cardActionAnchor != card ||
                        !new Rect2(GlobalPosition, Size).Grow(1).Encloses(button.GetGlobalRect()))
                        throw new Exception($"Descartar no está visible junto a la carta elegida en {size}, asiento {seat}.");
                }
                else if (_snapshot!.Options.Any(o => o.Move.Kind == DuelMoveKind.Discard))
                    throw new Exception("El rival puede descartar la mano activa.");
            }
        }
        GD.Print("PVP_HAND_LIMIT_LAYOUT_PASS · descarte final, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckMindResetLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(91); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Actions;
            foreach (var player in game.State.Players)
            {
                player.Hand.AddRange([CardCatalog.Senshuni.Id, CardCatalog.Destiny.Id]);
                player.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Pendant.Id, CardCatalog.Magnet.Id]);
            }
            game.State.Active.Hand.Insert(0, CardCatalog.MindReset.Id);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("Resetear no se activa.");
            for (var step = 0; step < 4; step++)
            {
                foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
                foreach (var seat in new[] { 0, 1 })
                {
                    GetWindow().ContentScaleSize = size;
                    _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, (ulong)(30 + step)));
                    for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    var minimum = _root!.GetCombinedMinimumSize();
                    if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1) throw new Exception($"Resetear desborda {size}: {minimum}.");
                    if (seat == actor && step > 0)
                    {
                        if (!ChoosingResetCards || _handCards.Count == 0) throw new Exception("Faltan las cartas seleccionables de Resetear.");
                        var titles = _hand!.FindChildren("HandSectionTitle*", "Label", true, false).OfType<Label>().ToArray();
                        if (step < 3 && (titles.Length != 2 || !titles.Any(t => t.Text.Contains("TU MEMORIA")) || !titles.Any(t => t.Text.Contains("MEMORIA DE"))))
                            throw new Exception("Resetear no separa visualmente las dos memorias.");
                        if (step == 3 && titles.Length != 0)
                            throw new Exception("Resetear agrupa como memoria una carta de mano.");
                        SelectCard(0);
                        if (!_moves.GetChildren().OfType<Button>().Any(b => PresentedOption(b)?.Move.Kind == (step < 3 ? DuelMoveKind.MindResetMemory : DuelMoveKind.MindResetHand)))
                            throw new Exception("Falta el botón para confirmar la carta de Resetear.");
                    }
                    if (seat != actor && ChoosingResetCards) throw new Exception("El rival recibe las selecciones privadas de Resetear.");
                }
                var result = step switch
                {
                    0 => game.ChooseMindResetTarget(actor, 1 - actor),
                    1 => game.ChooseMindResetMemory(actor, 0, 0),
                    2 => game.ChooseMindResetMemory(actor, 1, 0),
                    _ => game.ResolveMindReset(actor, 0)
                };
                if (!result.Success) throw new Exception(result.Message);
            }
        }
        GD.Print("PVP_MIND_RESET_LAYOUT_PASS · objetivo, dos memorias y mano privada, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckMindSwapLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        foreach (var (ownCount, otherCount) in new[] { (0, 3), (4, 0), (5, 8) })
        {
            var game = new GameEngine(43); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Actions; game.State.Turn = 2;
            game.State.Active.Hand.Add(CardCatalog.MindSwap.Id);
            game.State.Active.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, ownCount));
            game.State.Players[1 - actor].Hand.AddRange(Enumerable.Repeat(CardCatalog.ExtraLife.Id, otherCount));
            var previous = new[] { DuelSnapshot.For(game, 0, 20), DuelSnapshot.For(game, 1, 20) };
            var cues = DuelVisualEvents.Capture(game);
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No se resuelve Cambio de Mentes.");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(previous[seat]);
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var snapshot = DuelSnapshot.For(game, seat, 21); snapshot.Visuals = cues;
                var origins = CaptureOrigins(snapshot, previous[seat]); PresentTestSnapshot(snapshot); Animate(cues, origins);
                var frames = 0;
                while (IsAnimating && frames++ < 30)
                {
                    _die.AdvancePlayback(0.5); UpdateFlights(0.5);
                    if (_activeFlight?.Event.Kind == DuelVisualKind.HandTransfer)
                    {
                        if (_flightFront != null) throw new Exception("La transferencia de manos expone una identidad en la animación.");
                        var prefix = _activeFlight.Event.Target == seat ? "HandCard" : "OpponentHandCard";
                        if (_flightTarget != null && !_flightTarget.Name.ToString().StartsWith(prefix))
                            throw new Exception("La mano viaja al jugador equivocado.");
                    }
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (IsAnimating || !_handCards.Select(c => c.Card.Id).SequenceEqual(game.State.Players[seat].Hand))
                    throw new Exception("La mano intercambiada no se presenta o no termina su animación.");
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1) throw new Exception($"Cambio de Mentes desborda {size}: {minimum}.");
                if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                    throw new Exception("El intercambio revela cartas que estaban ocultas.");
                var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
                if (directory != null && ownCount == 5 && otherCount == 8 && seat == actor && size.X is 1440 or 844)
                {
                    DirAccess.MakeDirRecursiveAbsolute(directory);
                    GetViewport().GetTexture().GetImage().SavePng($"{directory}/mind-swap-{actor}-{size.X}.png");
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_MIND_SWAP_LAYOUT_PASS · manos vacías y desiguales, transferencias sin identidades, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckDeckSearchLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(43); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Actions;
            foreach (var p in game.State.Players) { p.Hand.Clear(); p.Hand.Add(CardCatalog.Attack.Id); }
            var player = game.State.Active; player.Hand.Clear(); player.Hand.Add(CardCatalog.KonamyCode.Id); player.Deck.Clear();
            player.Deck.AddRange(new[] { CardCatalog.Magnet.Id, CardCatalog.Attack.Id, CardCatalog.DiamondHelmet.Id, CardCatalog.ExtraLife.Id,
                CardCatalog.PointsIncrease.Id, CardCatalog.BlueEgg.Id, CardCatalog.TimeBomb.Id, CardCatalog.Reroll.Id, CardCatalog.LoadError.Id,
                CardCatalog.KonamyCode.Id, CardCatalog.CardboardBox.Id, CardCatalog.Pendant.Id, CardCatalog.Senshuni.Id, CardCatalog.Destiny.Id });
            if (!game.PlayCode(actor, 0).Success) throw new Exception("No comienza la búsqueda en el mazo.");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 13));
                if (seat == actor)
                {
                    if (_handCards.Count != 12 || _handCards.Any(c => c.Card.Kind == CardKind.Skill))
                        throw new Exception("La búsqueda no filtra las habilidades del mazo.");
                    SelectCard(2);
                    var label = _deckSearchOptions[2].Label;
                    if (_selected.Single() != 2 || _moveButtons.Count(b => PresentedOption(b)?.Label == label) != 1 ||
                        _deckSearchOptions[2].Move.Index != 3 || !_contextTitle.Text.Contains(CardCatalog.ExtraLife.Name) ||
                        _moves.GetChildren().OfType<Button>().First().Text != "Añadir a mano")
                        throw new Exception("La carta ilustrada no corresponde a la opción autorizada del mazo.");
                }
                else if (_handCards.Count != 1 || _deckSearchOptions.Length != 0 || _handCards[0].Card.Id != CardCatalog.Attack.Id)
                    throw new Exception("La búsqueda expone el mazo al rival o sustituye su mano.");
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1) throw new Exception($"Búsqueda desborda {size}: {minimum}.");
                if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                    throw new Exception("La búsqueda revela cartas ocultas del rival.");
                var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
                if (directory != null && seat == actor && size.X is 1440 or 844)
                {
                    DirAccess.MakeDirRecursiveAbsolute(directory);
                    GetViewport().GetTexture().GetImage().SavePng($"{directory}/deck-search-{actor}-{size.X}.png");
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_DECK_SEARCH_LAYOUT_PASS · doce objetivos privados ilustrados, filtro de habilidades, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckRecyclingLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(43); game.State.CurrentPlayer = actor;
            game.State.Phase = Phase.RecycleChoice;
            game.State.PendingDieRoll = new(actor, DiePurpose.Recycling, 6, Phase.Combat);
            foreach (var p in game.State.Players) { p.Hand.Clear(); p.Hand.Add(CardCatalog.Attack.Id); }
            game.State.Players[actor].Memory.AddRange(new[] { CardCatalog.ExtraLife.Id, CardCatalog.Destiny.Id,
                CardCatalog.DiamondHelmet.Id, CardCatalog.Magnet.Id, CardCatalog.Pendant.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id });
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 12));
                if (seat == actor)
                {
                    if (_handCards.Count != 7 || _handCards[0].Card.Id != CardCatalog.ExtraLife.Id)
                        throw new Exception("El reciclaje no muestra las cartas de memoria.");
                    foreach (var index in Enumerable.Range(1, 6)) SelectCard(index);
                    if (_selected.Count != 6 || !_selection.Text.Contains("6/6") || _moveButtons.All(b => (int)b.GetMeta("discards") != 6))
                        throw new Exception("No se pueden elegir seis cartas de memoria.");
                }
                else if (_handCards.Count != 1 || _handCards[0].Card.Id != CardCatalog.Attack.Id)
                    throw new Exception("El espectador del reciclaje pierde su propia mano.");
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Reciclaje desborda {size}: {minimum}.");
                if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                    throw new Exception("Reciclaje revela la mano rival.");
                var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
                if (directory != null && seat == actor && size.X is 1440 or 844)
                {
                    DirAccess.MakeDirRecursiveAbsolute(directory);
                    GetViewport().GetTexture().GetImage().SavePng($"{directory}/recycling-{actor}-{size.X}.png");
                }
            }
        }
        _selected.Clear();
        GD.Print("PVP_RECYCLING_LAYOUT_PASS · selección de seis cartas de memoria, cuatro tamaños y ambos jugadores/asientos.");
    }

    internal async Task CheckTimeBombLayout()
    {
        var game = new GameEngine(43); game.State.Phase = Phase.Actions; game.State.CurrentPlayer = 0;
        foreach (var player in game.State.Players)
        {
            player.Hand.Clear(); player.Hand.Add(CardCatalog.Attack.Id);
            player.Equipment = CardCatalog.DiamondHelmet.Id; player.Bonus = CardCatalog.Magnet.Id; player.ActionsLeft = 2;
        }
        game.State.Active.Hand.Insert(0, CardCatalog.TimeBomb.Id);
        if (!game.PlayCode(0, 0).Success) throw new Exception("No se prepara la elección de Bomba.");
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().ContentScaleSize = size;
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var snapshot = DuelSnapshot.For(game, seat, 11); ResetAnimations(); PresentTestSnapshot(snapshot);
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var root = (Control)FindChild("DuelInterface", true, false);
            var minimum = root.GetCombinedMinimumSize();
            if (minimum.X > root.Size.X + 1 || minimum.Y > root.Size.Y + 1)
                throw new Exception($"Bomba desborda {size}: {minimum}.");
            if (snapshot.Options.Count(o => o.Move.Kind == DuelMoveKind.Destroy) != (seat == 0 ? 6 : 0))
                throw new Exception("Se pierden zonas de Bomba o se ofrecen al adversario.");
            if (FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().Any(c => !c.IsFaceDown))
                throw new Exception("Bomba revela la mano rival en la interfaz.");
        }
        GD.Print("PVP_BOMB_LAYOUT_PASS · seis zonas, cuatro tamaños, ambos asientos y mano rival oculta.");
    }

    internal async Task CheckVisualCues(DuelSnapshot snapshot)
    {
        ResetAnimations(); PresentTestSnapshot(snapshot);
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var events = new List<DuelVisualEvent>
        {
            new() { Kind = DuelVisualKind.Die, Player = -1, Value = 3 }, new() { Kind = DuelVisualKind.Die, Player = -1, Value = 3 },
            new() { Kind = DuelVisualKind.Draw, Player = 1, Value = 0 }, new() { Kind = DuelVisualKind.Return, Player = 0, Value = 0 },
            new() { Kind = DuelVisualKind.Shuffle, Player = 1 }, new() { Kind = DuelVisualKind.BonusPlace, Player = 1 },
            new() { Kind = DuelVisualKind.BonusClaim, Player = 0, Target = 1, Value = 0 },
            new() { Kind = DuelVisualKind.Pile, Player = 1, Value = 0, Source = CardZone.Hand, Destination = CardZone.Memory },
            new() { Kind = DuelVisualKind.Attack, Player = 1 }, new() { Kind = DuelVisualKind.Hit, Player = 0 }
        };
        snapshot.Visuals = events;
        var origins = CaptureOrigins(snapshot, snapshot);
        Animate(events, origins);
        var frames = 0;
        while (IsAnimating && frames++ < 100)
        {
            _die.AdvancePlayback(0.5); UpdateFlights(0.5);
            if (_flightFront != null && _activeFlight?.Event.Kind == DuelVisualKind.Return && _activeFlight.Event.Player == 0)
                throw new Exception("La devolución de una mano oculta revela una carta.");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        PlayPendingPortraits();
        if (IsAnimating || _die.TopFaceValue != 3 || _portraits[0].Motion != PortraitMotion.Attack || _portraits[1].Motion != PortraitMotion.Hit)
            throw new Exception("No termina la secuencia visual o se pierde un efecto.");
        foreach (var victim in new[] { 0, 1 })
        {
            ResetAnimations(); PresentTestSnapshot(snapshot);
            // A new revision animates before the rebuilt board has been laid out.
            Animate([new() { Kind = DuelVisualKind.Hit, Player = victim, Value = 2, AttackDie = 5 }], []);
            for (var i = 0; i < 4; i++)
            {
                UpdateFlights(0);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            var portrait = _portraits[victim == snapshot.MySeat ? 0 : 1];
            if (_impacts.LastNumberAt is not { } at || !portrait.GetGlobalRect().Grow(2).HasPoint(at))
                throw new Exception($"El número de daño queda fuera del relikario: {_impacts.LastNumberAt}, retrato {portrait.GetGlobalRect()}.");
        }
        GD.Print("SHARED_ARENA_VISUALS_PASS · dados repetidos, robo, devolución oculta, barajado, Bonus, memoria, ataque, daño y números sobre el relikario.");
    }

    internal async Task CheckHealingPortraits(GameEngine game)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var originalSnapshot = _snapshot!;
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        foreach (var portrait in _portraits) portrait.SetProcess(false);
        try
        {
            foreach (var seat in new[] { 0, 1 })
            foreach (var healedPlayer in new[] { 0, 1 })
            {
                var snapshot = DuelSnapshot.For(game, seat, 10);
                snapshot.Players[healedPlayer].Health = 3;
                ResetAnimations(); PresentTestSnapshot(snapshot);
                var relative = healedPlayer == seat ? 0 : 1;
                var portrait = _portraits[relative];
                snapshot.Visuals = [new() { Kind = DuelVisualKind.Die, Player = -1, Value = 3 },
                    new() { Kind = DuelVisualKind.Heal, Player = healedPlayer, Value = 2 }];
                Animate(snapshot.Visuals, []);
                Check(portrait.Motion == PortraitMotion.Idle, "La curación se reproduce antes de resolver el dado.");
                var frames = 0;
                while (IsAnimating && frames++ < 40)
                {
                    _die.AdvancePlayback(0.5); UpdateFlights(0.5);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                PlayPendingPortraits();
                Check(!IsAnimating && portrait.Motion == PortraitMotion.Heal && _portraits[1 - relative].Motion == PortraitMotion.Idle,
                    "La curación no se reproduce para el destinatario correcto.");
                portrait.AdvancePlayback(portrait.DurationSeconds / 2);
                var frame = portrait.CurrentFrame; PresentTestSnapshot(snapshot);
                Check(portrait.CurrentFrame == frame && frame > 0, "Reconstruir el tablero reinicia la curación.");
                if (directory != null && DisplayServer.GetName() != "headless")
                {
                    System.IO.Directory.CreateDirectory(directory);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,
                        $"healing-seat{seat}-player{healedPlayer}.png"));
                }
                portrait.AdvancePlayback(portrait.DurationSeconds);
                Check(portrait.Motion == PortraitMotion.Idle, "La curación no vuelve al reposo.");
                Animate([new() { Kind = DuelVisualKind.Heal, Player = healedPlayer, Value = 0 }], []);
                Check(portrait.Motion == PortraitMotion.Idle, "Una curación sin vidas recuperadas reproduce el efecto.");
                Animate([new() { Kind = DuelVisualKind.Heal, Player = healedPlayer, Value = 2 }], []);
                Check(portrait.Motion == PortraitMotion.Heal && portrait.CurrentFrame == 0, "Se pierde una curación consecutiva del mismo valor.");
                ResetAnimations(); PresentTestSnapshot(snapshot);
                Check(portrait.Motion == PortraitMotion.Idle, "Recuperar el estado vuelve a reproducir una curación pasada.");
            }
            GD.Print("PVP_HEALING_PASS · ambos jugadores/asientos, dado, destinatario, repetición, reposo y recuperación.");
        }
        finally
        {
            ResetAnimations(); PresentTestSnapshot(originalSnapshot);
            foreach (var portrait in _portraits) portrait.SetProcess(true);
        }
    }

    internal async Task CheckZeroHealthPortraits(GameEngine game)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var originalSnapshot = _snapshot!;
        var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        foreach (var portrait in _portraits) portrait.SetProcess(false);
        try
        {
            foreach (var seat in new[] { 0, 1 })
            foreach (var exhaustedPlayer in new[] { 0, 1 })
            {
                var snapshot = DuelSnapshot.For(game, seat, 9);
                snapshot.Winner = 1 - exhaustedPlayer;
                snapshot.Players[exhaustedPlayer].Health = 0;
                ResetAnimations(); PresentTestSnapshot(snapshot);
                var relative = exhaustedPlayer == seat ? 0 : 1;
                var portrait = _portraits[relative];
                Check(_portraits.All(p => p.AssetsReady), "No se cargan todos los clips del Relikario.");
                Check(portrait.Motion == PortraitMotion.ExhaustedIdle && !portrait.IsActionPlaying &&
                    _portraits[1 - relative].Motion == PortraitMotion.Idle,
                    "La vista a cero vidas no selecciona el reposo correcto para cada jugador.");

                foreach (var (die, motion) in new[] { (4, PortraitMotion.Hit), (5, PortraitMotion.HitNormal), (6, PortraitMotion.HitCritical) })
                {
                    snapshot.Players[exhaustedPlayer].Health = 1; PresentTestSnapshot(snapshot);
                    Check(portrait.Motion == PortraitMotion.Idle, "El reposo agotado persiste con vida positiva.");
                    // A secondary die and the amount of damage must not select the impact clip.
                    _die.Value = 6;
                    Animate([new() { Kind = DuelVisualKind.Hit, Player = exhaustedPlayer, Value = 6, AttackDie = die }], []);
                    Check(portrait.Motion == motion, $"El resultado {die} elige un impacto incorrecto.");
                    portrait.AdvancePlayback(portrait.DurationSeconds / 4);
                    snapshot.Players[exhaustedPlayer].Health = 0; PresentTestSnapshot(snapshot);
                    Check(portrait.Motion == motion && portrait.CurrentFrame > 0,
                        "Llegar a cero vidas interrumpe o reinicia la reacción al último golpe.");
                    if (directory != null && DisplayServer.GetName() != "headless")
                    {
                        System.IO.Directory.CreateDirectory(directory);
                        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,
                            $"impact-{die}-seat{seat}-player{exhaustedPlayer}.png"));
                    }
                    portrait.AdvancePlayback(RelikarioPortrait.DurationFor(motion));
                    Check(portrait.Motion == PortraitMotion.ExhaustedIdle, "El último golpe vuelve al reposo sano.");
                }
                portrait.AdvancePlayback(portrait.DurationSeconds * 2.5);
                Check(portrait.Motion == PortraitMotion.ExhaustedIdle && portrait.CurrentFrame == portrait.FrameCount / 2,
                    "El reposo agotado no se repite correctamente.");
                var frame = portrait.CurrentFrame;
                PresentTestSnapshot(snapshot);
                Check(portrait.CurrentFrame == frame, "Reconstruir la arena reinicia el bucle de respiración.");
                ResetAnimations(); PresentTestSnapshot(snapshot);
                Check(portrait.Motion == PortraitMotion.ExhaustedIdle, "Una recuperación visual pierde el estado a cero vidas.");

                if (directory != null && DisplayServer.GetName() != "headless")
                {
                    System.IO.Directory.CreateDirectory(directory);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory,
                        $"zero-health-seat{seat}-player{exhaustedPlayer}.png"));
                }
            }
            GD.Print("PVP_ZERO_HEALTH_PASS · ambos jugadores y asientos, último golpe, bucle, reconstrucción y recuperación.");
            GD.Print("PVP_IMPACT_PASS · 4 reducido, 5 normal, 6 crítico, ambos jugadores/asientos y transición a cero vidas.");
        }
        finally
        {
            ResetAnimations(); PresentTestSnapshot(originalSnapshot);
            foreach (var portrait in _portraits) portrait.SetProcess(true);
        }
    }
}
