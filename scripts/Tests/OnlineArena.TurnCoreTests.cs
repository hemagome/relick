using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private const long TestSecond = 1_000_000;

    internal async Task CheckTurnCore()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            PresentPlaymat();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }
        GameEngine Game()
        {
            var game = GameEngine.FromOpening(new DuelOpening
            {
                Names = ["Ana", "Beto"], StartingSeat = 0,
                Hands = [[CardCatalog.Attack.Id, CardCatalog.Bonfire.Id, CardCatalog.Attack.Id], [CardCatalog.Attack.Id, CardCatalog.LoadError.Id]],
                Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()], Bonuses = [CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id]
            }, new SeededRandom(77));
            game.State.Phase = Phase.Combat;
            foreach (var player in game.State.Players)
            {
                player.Memory.AddRange([CardCatalog.Bonfire.Id, CardCatalog.Reroll.Id]);
                player.Equipment = CardCatalog.Totem.Id;
                player.Skills.Add(CardCatalog.Destiny.Id);
            }
            return game;
        }
        DuelSnapshot View(GameEngine game, int seat, ulong revision = 700, long spent = 0)
        {
            var view = DuelSnapshot.For(game, seat, revision);
            view.Clock = TurnClock.Next(null, view.Turn, view.DecisionPlayer, 0);
            view.Clock.SpentThisTurn[view.DecisionPlayer] = spent;
            foreach (var player in view.Players) player.LightEquipment = [CardCatalog.PurpleRing.Id];
            return view;
        }
        Rect2 Bounds(Control control) =>
            ProjectedTable.Of(control) is { } table ? table.ScreenBounds(control, new Rect2(Vector2.Zero, control.Size)) : control.GetGlobalRect();

        var saved = (GetWindow().Size, GetWindow().ContentScaleSize, _tableView, _requestedTableView);
        try
        {
            foreach (var table in new[] { false, true })
            foreach (var size in new[] { new Vector2I(2280, 1024), new(1820, 1024), new(1638, 1024), new(1440, 1024) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size;
                _tableView = _requestedTableView = table;
                await Layout();
                var game = Game();
                var now = 25 * TestSecond;
                TestServerNow = () => now;
                ResetAnimations(); PresentTestSnapshot(View(game, seat)); await Layout();
                var where = $"{size}, asiento {seat}, mesa {table}";
                var canvas = (BoardCanvas)_root!.FindChild("DuelArena", true, false);
                var plate = _turnCore!.GetGlobalRect();
                Check(_turnCore.GetCombinedMinimumSize() is var minimum && minimum.X <= TurnCore.PlateWidth && minimum.Y <= TurnCore.PlateHeight,
                    $"El contenido agranda el núcleo a {_turnCore.GetCombinedMinimumSize()} en {where}.");
                Check(canvas.GetGlobalRect().Encloses(plate), $"El núcleo sale del tablero en {where}.");
                foreach (var relative in new[] { 0, 1 })
                {
                    Control Zone(string name) => (Control)_root.FindChild(name + relative, true, false);
                    var zones = new List<Control> { Zone("Hero"), Zone("MemoryButton"), Zone("LightEquipmentZone"), Zone("DeckStack") };
                    zones.AddRange(Enumerable.Range(0, 4).Select(slot => (Control)_root.FindChild($"Skill{relative}_{slot}", true, false)));
                    foreach (var zone in zones)
                        Check(!Bounds(zone).Grow(-1).Intersects(plate), $"El núcleo tapa {zone.Name} en {where}: {plate}, {Bounds(zone)}.");
                    var light = Bounds(Zone("LightEquipmentZone")).Grow(-1);
                    var neighbours = zones.Where(zone => !zone.Name.ToString().StartsWith("LightEquipmentZone")).ToList();
                    neighbours.AddRange(new[] { "BonusFrame0", "BonusFrame1", "EquipmentZone" + relative }
                        .Select(name => (Control)_root.FindChild(name, true, false)));
                    // Projected bounding boxes of neighbours overlap at their slanted edges, so compare on the flat board.
                    if (!table)
                        foreach (var zone in neighbours)
                            Check(!light.Intersects(Bounds(zone).Grow(-1)), $"Los equipos ligeros tapan {zone.Name} en {where}.");
                }
                if (size.X >= 1820) Check(canvas.Unit > 1.03f, $"El tablero no aprovecha el alto liberado en {where}: {canvas.Unit}.");

                var label = _turnCore.Time;
                var mine = game.State.DecisionPlayer == seat;
                RefreshTurnClock();
                Check(label is { Visible: true, Text: "1:05" } && label.GetThemeColor("font_color") == (mine ? ArenaPresentation.Mint : ArenaPresentation.Coral),
                    $"El reloj no muestra a quien decide en {where}: {label.Text}.");
                Check(Math.Abs(_turnCore.Remaining!.Value - 65f / 90) < .002f, "El contorno no coincide con el tiempo.");
                await Capture($"nucleo-{(table ? "mesa" : "clasica")}-{size.X}-asiento{seat}");
                now = 78 * TestSecond; RefreshTurnClock();
                Check(label.Text == "0:12" && label.GetThemeColor("font_color") == BoardArt.Gold, "El tramo final del reloj no avisa.");
                now = 102 * TestSecond; RefreshTurnClock();
                Check(label.Text == "+0:12" && _turnCore.Remaining == 0 && game.State.Phase == Phase.Combat, "El tiempo extra no es solo informativo.");

                // The longest status beside the longest clock still fits.
                var status = (Label)_root.FindChild("StageStatus", true, false);
                status.Text = "JUGADA RECHAZADA"; label.Text = "+12:34"; await Layout();
                Check(status.GetThemeFont("font").GetStringSize(status.Text, HorizontalAlignment.Left, -1, status.GetThemeFontSize("font_size")).X <= status.Size.X + 1,
                    $"El estado se corta junto al reloj en {where}.");

                game.Advance(0);
                PresentTestSnapshot(View(game, seat, 701)); await Layout();
                Check(_turnCore!.GetGlobalRect().IsEqualApprox(plate), $"El núcleo cambia de sitio entre fases en {where}.");
            }

            _tableView = _requestedTableView = false;
            foreach (var size in new[] { new Vector2I(1820, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                TestServerNow = () => 25 * TestSecond;
                var game = Game();
                ResetAnimations(); PresentTestSnapshot(View(game, 0)); await Layout();
                Check(_clockLabels.FirstOrDefault(l => l.IsVisibleInTree()) is { Text: "1:05" } shown &&
                    new Rect2(GlobalPosition, Size).Grow(1).Encloses(shown.GetGlobalRect()) &&
                    new Rect2(GlobalPosition, Size).Grow(1).Encloses(_advanceButton!.GetGlobalRect()),
                    $"El reloj o el avance quedan fuera de la pantalla en {size}.");
                await Capture($"reloj-{size.X}x{size.Y}");

                game.State.PendingAttack = new(0, CardCatalog.Destiny.Id, 3, false);
                game.State.Phase = Phase.DefenseChoice;
                PresentTestSnapshot(View(game, 1, 702)); RefreshDecisionPrompt(); await Layout();
                Check(_decisionPopup is { Visible: true } popup && popup.FindChild("TurnClock", true, false) is Label { Visible: true },
                    $"El aviso de decisión oculta el reloj en {size}.");
                await Capture($"decision-{size.X}x{size.Y}");

                var search = Game();
                search.State.Phase = Phase.Actions; search.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
                search.State.Players[1].Hand.Clear();
                Check(search.PlayCode(0, 0).Success, "No abre la galería de Disquete.");
                PresentTestSnapshot(View(search, 0, 703)); await Layout();
                Check(_clockLabels.Any(l => l.IsVisibleInTree() && (l != _turnCore?.Time)), $"La galería oculta el reloj en {size}.");
                await Capture($"galeria-{size.X}x{size.Y}");
            }

            GetWindow().Size = GetWindow().ContentScaleSize = new Vector2I(1820, 1024); await Layout();
            await CheckTurnGuardAndShortcut(Game, (game, seat, revision, spent) => View(game, seat, revision, spent), Check, Layout, Capture);

            var plain = DuelSnapshot.For(Game(), 0, 900);
            PresentTestSnapshot(plain);
            Check(!_turnCore!.Time.Visible && _turnCore.Remaining == null, "Un servidor sin reloj deja un temporizador inventado.");
            TestServerNow = () => null;
            PresentTestSnapshot(View(Game(), 0, 901));
            Check(_turnCore!.Time.Text == "–:––" && _turnCore.Remaining == null, "Sin hora del servidor el reloj inventa un tiempo.");
            TestServerNow = () => 25 * TestSecond;
            var rated = View(Game(), 0, 903);
            TestDecisionLimit = 300 * TestSecond;
            TestServerNow = () => rated.Clock!.UpdatedAt + 255 * TestSecond;
            PresentTestSnapshot(rated); RefreshTurnClock();
            Check(_turnCore!.Time.Text == "¡0:45!" && _turnCore.TooltipText.Contains("lo pierde"), "Un duelo con puntuación no avisa de su último minuto.");
            TestDecisionLimit = null;
            RefreshTurnClock();
            Check(!_turnCore!.Time.Text.StartsWith("¡") && _turnCore.TooltipText.Contains("informativo"), "Un duelo casual amenaza con un límite que no tiene.");
            TestServerNow = () => 25 * TestSecond;
            var ended = View(Game(), 0, 902); ended.Winner = 0;
            PresentTestSnapshot(ended);
            Check(!_turnCore!.Time.Visible, "El reloj sigue visible al terminar.");
        }
        finally
        {
            TestServerNow = null; TestDecisionLimit = null;
            (_tableView, _requestedTableView) = (saved.Item3, saved.Item4);
            GetWindow().Size = saved.Item1; GetWindow().ContentScaleSize = saved.Item2;
            ResetAnimations(); await Layout();
        }
        GD.Print("PVP_TURN_CORE_PASS · núcleo sin solapes, equipos ligeros, reloj, doble clic y Espacio.");
    }

    private async Task CheckTurnGuardAndShortcut(Func<GameEngine> newGame, Func<GameEngine, int, ulong, long, DuelSnapshot> view,
        Action<bool, string> check, Func<Task> layout, Func<string, Task> capture)
    {
        async Task Expire()
        {
            _turnGuardUntil = 0;
            await layout();
        }
        int Press(Button button, bool echo = false)
        {
            var presses = 0;
            void Count() => presses++;
            button.Pressed += Count;
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.Space, Pressed = true, Echo = echo }, true);
            viewport.PushInput(new InputEventKey { Keycode = Key.Space }, true);
            button.Pressed -= Count;
            return presses;
        }

        var game = newGame();
        _turnButtonMeaning = null;
        PresentTestSnapshot(view(game, 0, 800, 0)); await Expire();
        PresentTestSnapshot(view(game, 0, 801, 0));
        check(!TurnButtonHeld, "Una jugada en la misma fase bloquea el avance otra vez.");
        game.Advance(0);
        PresentTestSnapshot(view(game, 0, 802, 0)); await layout();
        check(TurnButtonHeld && _advanceButton!.Text == "Finalizar turno", "Finalizar turno acepta el segundo clic de un doble clic.");
        _advanceButton!.Disabled = false;
        check(Press(_advanceButton) == 0, "Espacio se salta la espera del cambio de fase.");
        await Expire();
        _advanceButton.Disabled = false;
        check(Press(_advanceButton, echo: true) == 0, "Mantener Espacio pulsado avanza.");
        check(Press(_advanceButton) == 1, "Espacio no pulsa el botón de turno.");
        _advanceButton.Disabled = true;
        check(Press(_advanceButton) == 0, "Espacio pulsa un botón desactivado.");

        var card = _handCards[1];
        card.Disabled = false; GuiInputTestDriver.Click(card); await layout();
        _advanceButton!.Disabled = false;
        var selected = _selected.ToArray();
        check(Press(_advanceButton) == 1 && _selected.SequenceEqual(selected), "Espacio actúa sobre la última carta pulsada en vez del turno.");
        card.GrabFocus();
        check(Press(_advanceButton) == 0, "Espacio roba la tecla a un control enfocado con el teclado.");
        card.ReleaseFocus();
        var dialog = new AcceptDialog();
        var embedded = GetViewport().GuiEmbedSubwindows; GetViewport().GuiEmbedSubwindows = true;
        AddChild(dialog); dialog.PopupCentered(); await layout();
        check(TurnShortcutTarget() == null, "Espacio avanza detrás de un diálogo.");
        dialog.Hide(); dialog.QueueFree(); await layout();
        GetViewport().GuiEmbedSubwindows = embedded;

        game.State.Players[1].Hand.Clear();
        game.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
        game.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
        check(game.PlayCode(0, 0).Success && game.State.Phase == Phase.ErrorResponse, "La prueba no abre una respuesta.");
        PresentTestSnapshot(view(game, 1, 803, 0)); await layout();
        check(TurnButtonHeld && _passResponseSlot != null, "Dejar resolver no espera al aparecer.");
        await Expire();
        await capture("respuesta");
        var pass = _passResponseSlot!.GetChildren().OfType<Button>().Single();
        pass.Disabled = false;
        check(Press(pass) == 1, "Espacio no deja resolver.");
        PresentTestSnapshot(view(game, 1, 804, 0));
        check(TurnButtonHeld, "Un doble clic puede pasar dos respuestas seguidas.");
        await Expire();
    }
}
