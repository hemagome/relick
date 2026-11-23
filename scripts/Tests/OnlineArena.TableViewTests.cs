using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckTableView()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static bool Facing(Control control, float rotation) => Math.Abs(Mathf.AngleDifference(control.Rotation, rotation)) < .001f;
        async Task Layout() { for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        T Board<T>(string name) where T : Control => (T)_root!.FindChild(name, true, false);
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            PresentPlaymat();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }

        var savedPath = _boardSettingsPath;
        var savedMode = (_tableView, _requestedTableView);
        var oldEmbedded = GetViewport().GuiEmbedSubwindows;
        _boardSettingsPath = $"user://table-view-test-{Guid.NewGuid():N}.cfg";
        GetViewport().GuiEmbedSubwindows = true;
        try
        {
            Check(!LoadTableView(), "La instalación nueva debe comenzar en vista clásica.");
            foreach (var size in new[] { new Vector2I(1820, 1024), new(1440, 1024) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                var game = GameEngine.FromOpening(new DuelOpening
                {
                    Names = ["Ana", "Beto"], Relikarios = [RelikarioCatalog.Yuno.Id, RelikarioCatalog.Osamu.Id], StartingSeat = seat,
                    Hands = [Enumerable.Repeat(CardCatalog.Attack.Id, 5).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 5).ToList()],
                    Decks = [CardCatalog.TrainingDeck(), CardCatalog.TrainingDeck()], Bonuses = [CardCatalog.Senshuni.Id, CardCatalog.Pendant.Id]
                }, new SeededRandom(85));
                game.State.Phase = Phase.Combat;
                foreach (var player in game.State.Players)
                {
                    player.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Bonfire.Id, CardCatalog.Pendant.Id, CardCatalog.Senshuni.Id]);
                    player.CorruptedMemory.AddRange([CardCatalog.Attack.Id, CardCatalog.Attack.Id, CardCatalog.Attack.Id]);
                    player.AttachedCards.AddRange([CardCatalog.Attack.Id, CardCatalog.Attack.Id]);
                    player.Equipment = CardCatalog.Totem.Id;
                    player.EquipmentStackedCopies = 2;
                    player.Skills.Add(CardCatalog.Destiny.Id);
                    player.Exchange.Add(CardCatalog.PlumberStar.Id);
                }
                ResetAnimations();
                _tableView = _requestedTableView = false;
                var before = DuelSnapshot.For(game, seat, 800);
                PresentTestSnapshot(before); await Layout();
                var classicDeck = ZoneRect(Board<DeckStack>("DeckStack0"));
                Check(!Board<BoardCanvas>("DuelArena").TableView && Facing(Board<DeckStack>("DeckStack1"), 0), "La vista inicial no es clásica.");
                await Capture($"clasica-{size.X}-asiento-{seat}");

                var menu = _arenaMenu!.GetPopup();
                Check(menu.GetItemIndex(TableViewMenuId) >= 0, "Falta el interruptor de vista en el menú de la arena.");
                _evolution.Play(0, RelikarioCatalog.Yuno, RelikarioCatalog.YunoLv2, null);
                menu.EmitSignal(PopupMenu.SignalName.IdPressed, TableViewMenuId);
                ApplyRequestedTableView();
                Check(!TableView && LoadTableView(), "El menú no guarda la preferencia o interrumpe una evolución.");
                _evolution.Stop(); ApplyRequestedTableView(); await Layout();
                var canvas = Board<BoardCanvas>("DuelArena");
                Check(canvas.TableView && ReferenceEquals(before, _snapshot), "Cambiar el modo reemplaza el estado del duelo.");
                Check(_portraits.All(p => p.StandingOnTable && Mathf.IsEqualApprox(p.Scale.X, p.Scale.Y)) && _portraits[0].Scale.X > _portraits[1].Scale.X,
                    "Los dos Relikarios deben erguirse con una escala uniforme y el propio debe verse más cercano.");
                Check(new Control[] { Board<Control>("PlayerName1"), _life[1], _actionPools[1],
                    Board<Control>("Dexterity1"), Board<Control>("ResourceBadge1") }.All(marker =>
                    Mathf.IsZeroApprox(marker.GetGlobalTransform().Rotation) && ProjectedTable.Of(marker) == null),
                    "Los marcadores de vidas, acciones, Destreza y PR del rival deben quedar de frente.");
                Check(new Control[] { Board<Control>("Hero0"), Board<Control>("Hero1"), _combatCenter! }.All(hud =>
                    hud.GetGlobalTransform().Scale is var scale && Mathf.IsEqualApprox(scale.X, scale.Y)),
                    "Los marcadores y el núcleo de turno deben conservar su proporción, sin aplastarse con la mesa.");
                GuiInputTestDriver.Hover(_evolveButton!); await Layout();
                Check(_relikarioPreview is { Evolution: true }, "El Relikario propio tapa la lectura del botón de evolución.");
                ClearContext(); await Layout();
                Check(!ZoneRect(Board<DeckStack>("DeckStack0")).IsEqualApprox(classicDeck), "El mazo propio sigue en la esquina de la vista clásica.");
                for (var side = 0; side < 2; side++)
                {
                    var deck = Board<DeckStack>($"DeckStack{side}");
                    var memory = Board<MemoryZoneButton>($"MemoryButton{side}");
                    var orientation = side == 0 ? 0 : Mathf.Pi;
                    Check(ZoneNode(side, CardZone.Corrupted, -1) == memory && memory.DisplayedAmount == 7 && memory.DisplayedFaceDown == 3,
                        "Las cartas boca abajo deben compartir la pila con las cartas boca arriba.");
                    Check(new Control?[] { deck, memory, ZoneNode(side, CardZone.Equipment, -1), ZoneNode(side, CardZone.Bonus, -1),
                        ZoneNode(side, CardZone.Area, -1), ZoneNode(side, CardZone.Attached, -1), ZoneNode(side, CardZone.Skill, 0),
                        ZoneNode(side, CardZone.Exchange, 0) }.All(piece => piece != null && Facing(piece, orientation)),
                        "Las zonas del campo deben orientarse hacia su dueño.");
                    Check(deck.RaisedPile && memory.RaisedPile &&
                        Board<AttachmentStack>($"AttachmentStack{side}").RaisedPile && Board<ZoneCardButton>($"EquipmentZone{side}").StackCopies == 3,
                        "Falta profundidad en una de las pilas.");
                    var table = ProjectedTable.Of(memory);
                    Check(table != null, "La memoria sigue dibujándose como un rectángulo de pantalla.");
                    var deckCenter = table!.ScreenPoint(deck, deck.Size / 2).Y;
                    var columnCenter = table.ScreenPoint(memory, memory.Size / 2 + new Vector2(0, (BoardCanvas.CardHeight + 10) / 2)).Y;
                    Check(Math.Abs(deckCenter - columnCenter) < 9, "El mazo no está centrado en su columna.");
                    var lift = table.ScreenPoint(deck, deck.TopRect.GetCenter()) - table.ScreenPoint(deck, deck.Size / 2);
                    Check(lift.Y < -1 && (side == 0 ? lift.X > 1 : lift.X < -1),
                        "La tapa del mazo debe elevarse hacia arriba y hacia fuera para descubrir su frente y su costado interior.");
                    var (amount, fullTop) = (deck.Amount, deck.TopRect);
                    deck.Amount = amount - 7;
                    var lowerTop = deck.TopRect;
                    deck.Amount = amount;
                    Check(lowerTop.Size.X < fullTop.Size.X, "La altura del mazo debe seguir su número de cartas sin saturarse.");
                    var tl = table!.ScreenPoint(memory, Vector2.Zero);
                    var tr = table.ScreenPoint(memory, new Vector2(memory.Size.X, 0));
                    var bl = table.ScreenPoint(memory, new Vector2(0, memory.Size.Y));
                    var br = table.ScreenPoint(memory, memory.Size);
                    var outer = Math.Abs(tr.X - tl.X);
                    var inner = Math.Abs(br.X - bl.X);
                    var widening = tl.Y < bl.Y ? inner - outer : outer - inner;
                    Check(widening > 3 && Mathf.IsEqualApprox(tl.Y, tr.Y) && Mathf.IsEqualApprox(bl.Y, br.Y),
                        "Los bordes de la zona no convergen con la perspectiva de la mesa.");
                }
                var fan = Board<HandFan>("OpponentHandFan");
                var rivalCard = Board<OpponentHandCard>("OpponentHandCard0");
                var rivalDeck = Board<DeckStack>("DeckStack1");
                var farRail = canvas.GlobalPosition.Y + ((canvas.Size.Y - BoardCanvas.DesignHeight * canvas.Unit) / 2 +
                    BoardCanvas.RearRailY * canvas.Unit);
                Check(fan.HeldOffTable && fan.IsVisibleInTree() && _opponentCards.All(card => card.IsFaceDown &&
                    Math.Abs(card.Rotation) < Mathf.Pi / 4 && Global(card, card.VisibleFaceRect).End.Y < farRail),
                    "El abanico rival debe verse erguido, con los dorsos detrás del borde de la mesa.");
                Check(ZoneRect(rivalCard).Size.X <= ZoneRect(rivalDeck).Size.X + 2, "El abanico rival es más grande que sus cartas en el campo.");
                GuiInputTestDriver.Click(Board<ZoneCardButton>("EquipmentZone1")); await Layout();
                Check(_fieldSelection is { Zone: FieldZone.Equipment } field && field.Owner == 1 - seat &&
                    _showcase is { FaceTexture: not null } && _showcase.Card?.Id == CardCatalog.Totem.Id && Facing(_showcase, 0),
                    "Seleccionar la carta invertida debe mostrar su arte de frente en el inspector.");
                await Capture($"mesa-{size.X}-asiento-{seat}");
                ClearContext(); await Layout();

                var memoryButton = Board<MemoryZoneButton>("MemoryButton0");
                var plane = ProjectedTable.Of(memoryButton)!;
                Vector2 Pointer(float x, float y) => plane.ScreenPoint(memoryButton, new(x, y), withCanvas: true);
                var topRight = Pointer(memoryButton.Size.X, 6);
                var bottomRight = Pointer(memoryButton.Size.X, memoryButton.Size.Y - 4);
                // This point is inside the enclosing rectangle but beyond the receding right edge.
                var outside = new Vector2((topRight.X + bottomRight.X) / 2, topRight.Y);
                GuiInputTestDriver.Click(plane.GetViewport(), outside); await Layout();
                Check(!GetChildren().OfType<AcceptDialog>().Any(), "Un clic fuera del trapecio se registra en la memoria.");
                GuiInputTestDriver.Click(plane.GetViewport(), Pointer(memoryButton.Size.X - 5, memoryButton.Size.Y - 12)); await Layout();
                var dialog = GetChildren().OfType<AcceptDialog>().Single();
                Check(dialog.Visible && dialog.FindChild("CollectionCards", true, false)?.GetChildCount() == 4 &&
                    dialog.FindChild("FaceDownMemoryCards", true, false)?.GetChildCount() == 3,
                    "El clic en la memoria inclinada no corresponde a su zona.");
                GuiInputTestDriver.Click(dialog.GetOkButton()); await Layout();

                var rival = 1 - seat;
                var draw = new DuelVisualEvent { Kind = DuelVisualKind.Draw, Player = rival, Value = game.State.Players[rival].Hand.Count };
                var ownDraw = new DuelVisualEvent { Kind = DuelVisualKind.Draw, Player = seat, Value = game.State.Players[seat].Hand.Count };
                game.State.Players[rival].Hand.Add(CardCatalog.Attack.Id);
                game.State.Players[seat].Hand.Add(CardCatalog.Attack.Id);
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 801)); Animate([draw, ownDraw], []); await Layout();
                UpdateFlights(0); UpdateFlights(0); UpdateFlights(0);
                var landing = ZoneRect(Board<OpponentHandCard>($"OpponentHandCard{draw.Value}"));
                Check(_flight != null && Facing(_flight, Mathf.Pi) && _flightBack is { Visible: true } && _flightSource.IsEqualApprox(ZoneRect(Board<DeckStack>("DeckStack1"))) &&
                    _flightDestination.IsEqualApprox(landing) && Upright(_flightBack).IsEqualApprox(_flightSource),
                    "La carta rival cambia de tamaño al iniciar el robo o toma una ruta incorrecta.");
                UpdateFlights(.449);
                Check(_flight != null && Math.Abs(Mathf.AngleDifference(_flight.Rotation, Board<OpponentHandCard>($"OpponentHandCard{draw.Value}").Rotation)) < .01f &&
                    _flightBack != null && Upright(_flightBack).Size.DistanceTo(landing.Size) < 1,
                    "La carta rival cambia de tamaño de golpe al incorporarse al abanico.");
                var ownLanding = ZoneRect(Board<HandCard>($"HandCard{ownDraw.Value}"));
                Check(_companion is { Front: { Visible: true } front } &&
                    Upright(front, front.VisibleFaceRect).Size.DistanceTo(ownLanding.Size) < 1,
                    "La cara de la carta propia no conserva su tamaño al incorporarse al abanico.");
                UpdateFlights(.01);
                Check(!IsAnimating, "El robo no termina en la mesa.");
                ResetAnimations();

                var beforeCanje = _snapshot!;
                var exchange = ZoneNode(1, CardZone.Exchange, 0)!;
                var expectedOrigin = PortraitFootprint(ZoneRect(exchange));
                var departed = new DuelVisualEvent { Kind = DuelVisualKind.Pile, Player = rival, Source = CardZone.Exchange, Destination = CardZone.Memory };
                var rivalPlayer = game.State.Players[rival];
                rivalPlayer.Memory.Add(rivalPlayer.Exchange[0]); rivalPlayer.Exchange.RemoveAt(0);
                var afterCanje = DuelSnapshot.For(game, seat, 802);
                afterCanje.Visuals = [departed];
                var origins = CaptureOrigins(afterCanje, beforeCanje);
                PresentTestSnapshot(afterCanje); Animate(afterCanje.Visuals, origins); await Layout();
                UpdateFlights(0); UpdateFlights(0); UpdateFlights(0);
                Check(_flight != null && Facing(_flight, Mathf.Pi * 1.5f) && _flightSource.IsEqualApprox(expectedOrigin),
                    "La carta de canje rival debe salir con su orientación apaisada e invertida.");
                UpdateFlights(.449);
                Check(_flight != null && Math.Abs(Mathf.AngleDifference(_flight.Rotation, Mathf.Pi)) < .01f &&
                    _flightDestination.IsEqualApprox(ZoneRect(Board<MemoryZoneButton>("MemoryButton1"))),
                    "La carta debe quedar orientada hacia el rival al llegar a su memoria.");
                UpdateFlights(.01); ResetAnimations();

                _arenaMenu!.GetPopup().EmitSignal(PopupMenu.SignalName.IdPressed, TableViewMenuId);
                ApplyRequestedTableView(); await Layout();
                Check(!TableView && !LoadTableView() && _portraits.All(p => !p.StandingOnTable && p.Scale == Vector2.One),
                    "El interruptor no restaura la vista clásica y su escala.");
            }

            _tableView = _requestedTableView = true;
            GetWindow().ContentScaleSize = new(844, 390); await Layout();
            Check(!TableView && _arenaMenu!.GetPopup().GetItemIndex(TableViewMenuId) < 0 && _portraits.All(p => p.Scale == Vector2.One),
                "La preferencia de escritorio debe respetar el tablero compacto.");
        }
        finally
        {
            (_tableView, _requestedTableView) = savedMode;
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_boardSettingsPath));
            _boardSettingsPath = savedPath;
            GetViewport().GuiEmbedSubwindows = oldEmbedded;
            ResetAnimations();
        }
        GD.Print("PVP_TABLE_VIEW_PASS · orientación del rival, marcadores legibles, inspector, geometría de clics, robo, ambos asientos y vista compacta.");
    }
}
