using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal void PresentTestSnapshot(DuelSnapshot snapshot) { _snapshot = snapshot; Render(); }

    internal async Task CheckHandLimitLayout()
    {
        foreach (var actor in new[] { 0, 1 })
        {
            var game = new GameEngine(7); game.State.CurrentPlayer = actor; game.State.Phase = Phase.Actions;
            game.State.Active.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 9));
            if (!game.Advance(actor).Success) throw new Exception("No se inicia el descarte final.");
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().ContentScaleSize = size;
                _selected.Clear(); ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 50));
                for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var minimum = _root!.GetCombinedMinimumSize();
                if (minimum.X > _root.Size.X + 1 || minimum.Y > _root.Size.Y + 1)
                    throw new Exception($"Descarte final desborda {size}: {minimum}.");
                if (seat == actor)
                {
                    if (!_selection.Text.Contains("Descarta 2")) throw new Exception("Falta la indicación del límite de mano.");
                    SelectCard(0);
                    if (!_moves.GetChildren().OfType<Button>().Any(b => b.Text.StartsWith("Descartar ")))
                        throw new Exception("Falta la opción para descartar la carta elegida.");
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
                        SelectCard(0);
                        if (!_moves.GetChildren().OfType<Button>().Any(b => b.Text.StartsWith(step < 3 ? "Memoria" : "Devolver")))
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
                    if (_selected.Single() != 2 || _moveButtons.Count(b => b.Text == label) != 1 ||
                        _deckSearchOptions[2].Move.Index != 3 || !_selection.Text.Contains(CardCatalog.ExtraLife.Name) ||
                        _moves.GetChildren().OfType<Button>().First().Text != label)
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
        GD.Print("SHARED_ARENA_VISUALS_PASS · dados repetidos, robo, devolución oculta, barajado, Bonus, memoria, ataque y daño.");
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
                Check(_portraits.All(p => p.AssetsReady), "No se cargan todos los clips de Yuno.");
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
                    portrait.AdvancePlayback(YunoPortrait.DurationFor(motion));
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
