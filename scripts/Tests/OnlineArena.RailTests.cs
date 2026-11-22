using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckAdaptiveRail()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            PositionCardActions(); PresentPlaymat();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }
        GameEngine Played()
        {
            for (var seed = 40; seed < 160; seed++)
            {
                var game = new GameEngine(seed);
                game.ChooseInitialSkill(0, CardCatalog.Magnet.Id); game.ChooseInitialSkill(1, CardCatalog.Destiny.Id); game.Start([]);
                for (var step = 0; step < 200 && !game.State.IsOver; step++)
                {
                    if (game.State.Turn >= 5 && game.State.Phase == Phase.Combat && game.State.CurrentPlayer == 0 && game.State.DecisionPlayer == 0)
                    {
                        if (game.PublicHistory?.Entries.Count >= 12 && game.State.Players[0].Hand.Count >= 3 && game.State.Players[0].Skills.Count > 0) return game;
                        break;
                    }
                    var option = DuelOptions.For(game, game.State.DecisionPlayer).FirstOrDefault(o => o.Move.Kind != DuelMoveKind.Concede && o.RequiredDiscards == 0);
                    if (option == null || !option.Move.Apply(game, game.State.DecisionPlayer).Success) break;
                }
            }
            throw new Exception("La prueba del riel no genera una partida de muestra.");
        }
        GameEngine Combat(string first) => GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [[first, CardCatalog.Attack.Id, CardCatalog.LoadError.Id], [CardCatalog.LoadError.Id]],
            Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()], Bonuses = [CardCatalog.Jessica.Id, CardCatalog.Pendant.Id]
        }, new SeededRandom(52));
        BoardCanvas Canvas() => (BoardCanvas)_root!.FindChild("DuelArena", true, false);

        var played = Played();
        var saved = (GetWindow().Size, GetWindow().ContentScaleSize, _tableView, _requestedTableView);
        var embedded = GetViewport().GuiEmbedSubwindows;
        try
        {
            foreach (var table in new[] { false, true })
            foreach (var size in new[] { new Vector2I(2280, 1024), new(1820, 1024), new(1807, 1024), new(1687, 1024), new(1638, 1024), new(1536, 1024), new(1440, 1024) })
            foreach (var seat in new[] { 0, 1 })
            {
                var where = $"{size}, asiento {seat}, mesa {table}";
                GetWindow().Size = size; GetWindow().ContentScaleSize = size;
                _tableView = _requestedTableView = table;
                await Layout();
                _historyOpen = false;
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(played, seat, 900)); await Layout();

                // The rail never costs the cards more than its strip.
                var free = size.X - (float)size.Y * BoardCanvas.DesignWidth / BoardCanvas.DesignHeight;
                var full = (float)size.Y / BoardCanvas.DesignHeight;
                var canvas = Canvas();
                Check(RailDocked == free >= DockFrom, $"El lector se fija en el modo equivocado en {where}.");
                var outer = size.X - canvas.Size.X;
                Check(RailDocked ? outer <= free + 1 && Math.Abs(canvas.Unit - full) < 0.002f : outer <= 100,
                    $"El riel encoge el tablero en {where}: ocupa {outer}, libre {free}, escala {canvas.Unit}.");
                Check(canvas.Unit >= Math.Min(full, (size.X - 100) / BoardCanvas.DesignWidth) - 0.002f, $"Las cartas no usan el ancho liberado en {where}.");
                if (size.X is 1687 or 1638) Check(canvas.Unit > 1.03f, $"Una pantalla 16:10 no llega al tamaño completo en {where}.");

                Check(_playTiles.Count == RecentGroups().Count(IsPlay) && _playTiles.Count > 2 && _playTiles[0].Newest &&
                    _playTiles.All(tile => tile.IsVisibleInTree() && tile.Size.X <= StripWidth + 1 && tile.TooltipText.Length > 0),
                    $"La franja pierde jugadas o su detalle en {where}.");
                Check(_comboResponsesToggle!.IsVisibleInTree() && _comboResponsesToggle.Size.X <= StripWidth + 1 &&
                    _comboResponsesToggle.Size.Y >= 48 && _arenaMenu!.IsVisibleInTree(), $"COMBO o el menú no caben en la franja en {where}.");
                if (RailDocked) Check(_lastPlay!.IsVisibleInTree(), $"El lector fijo queda vacío en {where}.");
                else Check(!_floatingReader!.Visible && !_historyDrawer!.Visible, $"Algo tapa el tablero sin pedirlo en {where}.");
                await Capture($"riel-{(table ? "mesa" : "clasica")}-{size.X}-asiento{seat}");

                var order = Enumerable.Range(0, _handCards.Count).OrderByDescending(i => _handCards[i].Card.Text.Length).ToArray();
                if (order.Length > 0)
                {
                    SelectCard(order[0]); await Layout(); SyncRail(); await Layout();
                    foreach (var card in _handCards) for (var i = 0; i < 40; i++) card._Process(0.05);
                    Check(_inspector.IsVisibleInTree() && _playTiles[0].IsVisibleInTree(), $"Seleccionar no abre el lector u oculta la franja en {where}.");
                    var reading = _inspector.ReadingViewport;
                    Check(_inspector.DescriptionLabel.Size.Y <= reading.Size.Y + 1, $"El lector corta el texto más largo de la mano en {where}.");
                    if (_floatingReader is { } floating)
                    {
                        var rect = floating.GetGlobalRect();
                        foreach (var relative in new[] { 0, 1 })
                        {
                            var zones = Enumerable.Range(0, 4).Select(slot => $"Skill{relative}_{slot}").Append($"Hero{relative}").Append($"MemoryButton{relative}");
                            foreach (var zone in zones.Select(name => (Control)_root!.FindChild(name, true, false)))
                                Check(!ZoneRect(zone).Grow(-3).Intersects(rect), $"El lector flotante tapa {zone.Name} en {where}.");
                        }
                        Check(!_turnCore!.GetGlobalRect().Intersects(rect) && new Rect2(GlobalPosition, Size).Encloses(rect),
                            $"El lector flotante tapa el núcleo o sale de pantalla en {where}.");
                    }
                    await Capture($"lectura-{(table ? "mesa" : "clasica")}-{size.X}-asiento{seat}");
                    ClearContext(); await Layout();
                }

                SetHistoryOpen(true); await Layout();
                Check(_historyFeed!.IsVisibleInTree() && _historyButton!.ButtonPressed, $"El historial no abre en {where}.");
                await Capture($"historial-{(table ? "mesa" : "clasica")}-{size.X}-asiento{seat}");
                SetHistoryOpen(false); await Layout();
                Check(!_historyFeed.IsVisibleInTree(), $"El historial no cierra en {where}.");
            }

            _tableView = _requestedTableView = false;
            foreach (var size in new[] { new Vector2I(1820, 1024), new(1687, 1024), new(1440, 1024) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();

                // Leaving a card gives the rail back; before, the history stayed hidden until a click on the empty field.
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(played, 0, 901)); await Layout();
                var hovered = _handCards[0];
                GetViewport().PushInput(new InputEventMouseMotion { Position = hovered.GetGlobalTransformWithCanvas() * new Vector2(hovered.Size.X / 2, 30) }, true);
                await Layout();
                Check(_inspectorPreview != null, $"Señalar una carta no la lee en {size}.");
                GetViewport().PushInput(new InputEventMouseMotion { Position = Canvas().GetGlobalRect().Position + new Vector2(220, 280) }, true);
                await Layout();
                Check(_previewLeftAt != null, $"Salir de la carta no empieza a cerrar su lectura en {size}.");
                _previewLeftAt = Time.GetTicksMsec() - Math.Min(Time.GetTicksMsec(), DockedGraceMilliseconds + 1);
                UpdateRail(); await Layout();
                Check(_inspectorPreview == null && !_inspector.HasCard && (!RailDocked || _lastPlay!.Visible),
                    $"El último hover deja el historial oculto en {size}.");

                // A play read from the strip stays until something replaces it.
                var tile = _playTiles.First(t => t.Card != null);
                GuiInputTestDriver.Click(tile); await Layout(); UpdateRail();
                Check(_inspectorPreview?.Card.Id == tile.Card!.Id && _previewPinned && _inspector.IsVisibleInTree(),
                    $"Pulsar una jugada no la deja abierta en {size}.");
                ClearContext();

                // Why a card has no play is read with it, and leaves the turn button and Space free.
                var blocked = Combat(CardCatalog.LoadError.Id);
                blocked.State.Phase = Phase.Combat;
                PresentTestSnapshot(DuelSnapshot.For(blocked, 0, 902)); await Layout();
                SelectCard(0); RefreshDecisionPrompt(); await Layout();
                _advanceButton!.Disabled = false;
                Check(_decisionHint.Length > 0 && _decisionPopup is { Visible: false } && _inspector.HintLabel.IsVisibleInTree() &&
                    _inspector.HintLabel.Text == _decisionHint && TurnShortcutTarget() == _advanceButton,
                    $"El motivo de una carta no jugable tapa el turno en {size}.");
                await Capture($"motivo-{size.X}");
                ClearContext(); await Layout();

                // The card to answer opens by itself.
                var answer = Combat(CardCatalog.FloppyDisk.Id);
                answer.State.Phase = Phase.Actions;
                answer.State.Players[1].Memory.Add(CardCatalog.Bonfire.Id);
                Check(answer.PlayCode(0, 0).Success && answer.State.Phase == Phase.ErrorResponse, "La prueba no abre una respuesta.");
                PresentTestSnapshot(DuelSnapshot.For(answer, 1, 903)); await Layout();
                Check(_inspector.IsVisibleInTree() && _contextTitle.Text == CardCatalog.FloppyDisk.Name && _showcase!.Visible,
                    $"La carta a responder no se abre sola en {size}.");
                await Capture($"respuesta-{size.X}");

                // The reader never covers a board target.
                var targeting = Combat(CardCatalog.Attack.Id);
                targeting.State.Phase = Phase.Combat;
                PresentTestSnapshot(DuelSnapshot.For(targeting, 0, 904)); await Layout();
                if (_floatingReader is { } yielding)
                {
                    SelectCard(0); await Layout();
                    var right = (Control)_root!.FindChild("BonusFrame1", true, false);
                    _pulsingTargets.Add(right); PositionReader();
                    Check(!yielding.Visible || !yielding.GetGlobalRect().Intersects(right.GetGlobalRect().Grow(-2)), $"El lector tapa un objetivo en {size}.");
                    var left = (Control)_root.FindChild("BonusFrame0", true, false);
                    _pulsingTargets.Add(left); PositionReader();
                    Check(!yielding.Visible, $"El lector tapa un objetivo cuando hay en ambos lados en {size}.");
                    _pulsingTargets.Remove(left); _pulsingTargets.Remove(right); ClearContext();
                }

                // A gallery reads inside its own panel.
                var gallery = Combat(CardCatalog.FloppyDisk.Id);
                gallery.State.Phase = Phase.Actions;
                gallery.State.Players[1].Hand.Clear();
                gallery.State.Players[0].Memory.AddRange([CardCatalog.Bonfire.Id, CardCatalog.Dodge.Id, CardCatalog.Attack.Id, CardCatalog.Reroll.Id]);
                gallery.State.Players[1].Memory.AddRange([CardCatalog.TimeBomb.Id, CardCatalog.Pendant.Id, CardCatalog.MindReset.Id]);
                Check(gallery.PlayCode(0, 0).Success, "La prueba no abre una galería.");
                PresentTestSnapshot(DuelSnapshot.For(gallery, 0, 905)); await Layout();
                SelectCard(0); await Layout();
                var chooser = (Control)_root!.FindChild("ChoicePanel", true, false);
                Check(_inspector.IsVisibleInTree() && _floatingReader == null &&
                    (RailDocked || chooser.IsAncestorOf(_inspector) && chooser.GetGlobalRect().Grow(1).Encloses(_inspector.GetGlobalRect()) &&
                        _handCards.All(card => !card.GetGlobalRect().Intersects(_inspector.GetGlobalRect()))),
                    $"El lector tapa las opciones de la galería en {size}.");
                Check(new Rect2(GlobalPosition, Size).Grow(1).Encloses(chooser.GetGlobalRect()), $"La galería sale de pantalla en {size}.");
                await Capture($"galeria-{size.X}");
            }

            // Results still on their way to the board are not told by the strip.
            GetWindow().Size = GetWindow().ContentScaleSize = new Vector2I(1820, 1024); await Layout();
            PresentTestSnapshot(DuelSnapshot.For(played, 0, 906)); await Layout();
            _die.RollTo(4); SyncRail();
            Check(_playTiles.All(tile => !tile.Latest || !tile.Settled) && !_lastPlay!.Visible,
                "La franja adelanta el resultado de una tirada en curso.");
            _die.AdvancePlayback(DiceFace.RollSeconds + 2); ResetAnimations(); SyncRail();
            Check(_playTiles.All(tile => tile.Settled) && _lastPlay.Visible, "La franja no muestra el resultado al terminar.");

            // Hovering the turn button shows a tooltip, which is a window: Space must still work under it.
            GetViewport().GuiEmbedSubwindows = true;
            PresentTestSnapshot(DuelSnapshot.For(Combat(CardCatalog.Attack.Id), 0, 907)); await Layout();
            _advanceButton!.Disabled = false;
            GetViewport().PushInput(new InputEventMouseMotion { Position = _advanceButton.GetGlobalRect().GetCenter() }, true);
            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            Check(GetViewport().GetEmbeddedSubwindows().Any(window => window.Visible), "La prueba no llega a mostrar el tooltip del botón de turno.");
            Check(TurnShortcutTarget() == _advanceButton, "El tooltip del botón anula Espacio.");
            GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One }, true);
            await Layout();
        }
        finally
        {
            _historyOpen = false;
            GetViewport().GuiEmbedSubwindows = embedded;
            (_tableView, _requestedTableView) = (saved.Item3, saved.Item4);
            GetWindow().Size = saved.Item1; GetWindow().ContentScaleSize = saved.Item2;
            ResetAnimations(); await Layout();
        }
        GD.Print("PVP_ADAPTIVE_RAIL_PASS · franja, lector fijo, flotante y en galería, objetivos libres, hover, motivo y respuesta.");
    }
}
