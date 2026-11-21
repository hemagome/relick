using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckCardInspector()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Capture(string name)
        {
            var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
            if (directory == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(directory);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
        }
        var scrollingCases = 0;
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1820, 1024), new(1024, 768), new(1280, 480), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var game = PendingCodeFixture(seat, CardCatalog.Senshuni.Id);
            var player = game.State.Players[seat];
            player.Hand.Clear(); player.Hand.AddRange([CardCatalog.Magnet.Id, CardCatalog.Destiny.Id, CardCatalog.Bonfire.Id, CardCatalog.Senshuni.Id]);
            if (!Short) player.Hand.InsertRange(3, [CardCatalog.Attack.Id, CardCatalog.Sniper.Id, CardCatalog.TimeBomb.Id]);
            player.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id]);
            player.SkillCounters[0] = 2; player.SkillCounters[1] = 1;
            player.Exchange.Add(CardCatalog.Jessica.Id);
            player.Bonus = CardCatalog.ExtraLife.Id;
            player.AttachedCards.AddRange([CardCatalog.Pendant.Id, CardCatalog.Sniper.Id, CardCatalog.TimeBomb.Id]);
            var rival = game.State.Players[1 - seat];
            rival.Equipment = CardCatalog.Pc.Id; rival.EquipmentDie = 4;
            rival.Skills.AddRange([CardCatalog.IronwingRoll.Id, CardCatalog.TurboSword.Id]);
            rival.SkillCounters[1] = CardCatalog.TurboSwordCounterLimit;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 401)); await Layout();
            var attached = (AttachmentStack)ZoneNode(0, CardZone.Attached, -1)!;
            var equipment = (ZoneCardButton)ZoneNode(1, CardZone.Equipment, -1)!;
            var emptyEquipment = (ZoneCardButton)ZoneNode(0, CardZone.Equipment, -1)!;
            var emptyAttached = (AttachmentStack)ZoneNode(1, CardZone.Attached, -1)!;
            Check(attached.CardRect.Size.Y >= attached.Size.Y - 9 && attached.CardRect.End.X <= attached.Size.X &&
                attached.AccessibilityName.EndsWith("3 cartas"), $"La pila sobre el Relikario pierde el tamaño o el recuento: {attached.CardRect} en {attached.Size} ({size}).");
            var equipmentArea = new Rect2(new Vector2(4, 4), equipment.Size - new Vector2(8, 8));
            Check(equipment.CardRect.IsEqualApprox(BoardArt.FitArtwork(BoardArt.LoadCardArtwork(CardCatalog.Pc)!, equipmentArea)) &&
                equipment.StatusText == "D6: 4",
                "El equipo pierde espacio o su dado guardado.");
            Check(emptyEquipment.Disabled && emptyEquipment.Caption == "EQUIPO" && emptyAttached.Disabled,
                "Una zona vacía permite consultar una carta inexistente.");
            var before = game.SaveSnapshot();
            var cardSizes = _handCards.Select(card => card.Size).ToArray();
            if (!Short)
            {
                var fan = _handCards[0].GetParent() as HandFan;
                Check(fan != null && _handCards[0].Rotation < 0 && _handCards[^1].Rotation > 0 &&
                    fan.GetCombinedMinimumSize().X < cardSizes.Sum(card => card.X),
                    "La mano horizontal no inclina las cartas ni ahorra espacio al superponerlas.");
            }
            var panels = new[] { "OwnRelikarioPanel", "EnemyRelikarioPanel" }
                .Select(name => (Control)_root!.FindChild(name, true, false)).ToArray();
            var panelRects = panels.Select(panel => panel.GetGlobalRect()).ToArray();
            void CheckPanelSize()
            {
                if (!Compact && !Short)
                    Check(panels.Select(panel => panel.GetGlobalRect()).SequenceEqual(panelRects),
                        "Abrir o cerrar el inspector cambia la posición o el tamaño de los cuadros de Relikario.");
            }
            var compactHeight = _inspector.Size.Y;
            Check(!_inspector.ReadingViewport.Visible && !_inspector.TitleLabel.Visible && compactHeight < 96,
                "Sin selección el inspector reserva espacio para una carta inexistente.");
            if (seat == 0) await Capture($"inspector-empty-{size.X}x{size.Y}");
            Check(_inspector.Material is ShaderMaterial { Shader: not null }, "Falta el material acrílico del inspector.");
            var initialMoves = _moves.GetChildren().ToArray();
            var hoverCard = _handCards[0];
            _hand!.EnsureControlVisible(hoverCard); await Layout();
            GuiInputTestDriver.Hover(hoverCard); await Layout();
            Check(_inspector.ReadingViewport.Visible && _contextTitle.Text == hoverCard.Card.Name &&
                _selected.Count == 0 && !hoverCard.Chosen && hoverCard.TooltipText.Length == 0 &&
                _moves.GetChildren().SequenceEqual(initialMoves),
                "Pasar el cursor no abre el inspector o cambia las jugadas disponibles.");
            if (seat == 0 && size.X is 1440 or 844) await Capture($"inspector-hover-{size.X}x{size.Y}");
            GuiInputTestDriver.Hover(_inspector.ReadingViewport); await Layout();
            Check(_contextTitle.Text == hoverCard.Card.Name, "Mover el cursor al texto cierra la vista previa.");
            void ClickEmptyField() => GuiInputTestDriver.Click(GetViewport(), panels[0].GlobalPosition + new Vector2(5, 5));
            ClickEmptyField(); await Layout();
            Check(!_inspector.HasCard, $"El campo vacío no cierra la vista previa en {size}.");
            for (var index = 0; index < player.Hand.Count; index++)
            {
                var card = _handCards[index]; _hand!.EnsureControlVisible(card); await Layout();
                card.Disabled = false;
                try { GuiInputTestDriver.Click(card); }
                catch (Exception e) { throw new Exception($"Inspector {size}, asiento {seat}, carta {index}: {e.Message}", e); }
                await Layout();
                CheckPanelSize();
                var definition = CardCatalog.Get(player.Hand[index]);
                Check(_contextTitle.Text == definition.Name, "El inspector no sigue la carta seleccionada.");
                Check(_inspector.Attributes.Visible == (definition.Kind == CardKind.Skill), "Se duplican valores del efecto como atributos de equipo o código.");
                Check(!_inspector.CounterBadge.Visible, "Una carta de la mano se muestra con los contadores de otra copia.");
                if (definition.Kind == CardKind.Skill)
                    Check(!_inspector.DescriptionLabel.Text.Contains("Daño normal:") && !_inspector.DescriptionLabel.Text.Contains("Premio:"), "El inspector repite los atributos en la descripción.");
                else Check(_inspector.DescriptionLabel.Text == definition.Text, "El inspector pierde condiciones de la carta.");
                Check(card.Size == cardSizes[index], "El inspector cambia el tamaño de la carta en la mano.");
                if (!Short) Check(card.ZIndex > 0, "La carta inspeccionada queda cubierta por las demás.");
                Check(_inspector.FindChildren("*", "Label", true, false).OfType<Label>().All(l => l.Text is not ("Efecto" or "Seleccionada" or "Efecto visible")), "El inspector conserva etiquetas redundantes.");
                Check(_inspector.FindChildren("*", "Label", true, false).OfType<Label>().Count(l => l.Text == "Coste") == 1, "El inspector duplica el campo de coste.");
                if (definition.Id == CardCatalog.Senshuni.Id)
                {
                    var reading = (Control)_inspector.FindChild("InspectorReading", true, false);
                    Check(_inspector.DescriptionLabel.GetGlobalRect().End.Y <= reading.GetGlobalRect().End.Y + 1,
                        "El efecto breve de Senshuni queda oculto al abrir el inspector.");
                }
                Check(_inspector.Size.Y > compactHeight && _inspector.ReadingViewport.Visible, "Consultar una carta no amplía el inspector.");
                var viewport = new Rect2(Vector2.Zero, Size);
                foreach (var field in new Control[] { _selection, _contextTitle, _inspector.Attributes, _inspector.CounterBadge, _contextCost })
                    if (field.IsVisibleInTree()) Check(viewport.Encloses(field.GetGlobalRect()), "Una cabecera o coste queda fuera de pantalla.");
                Check(_contextTitle.GetGlobalRect().End.Y <= _inspector.ReadingViewport.GetGlobalRect().Position.Y + 1,
                    "La identidad de la carta comparte el desplazamiento de la descripción.");
                if (Short)
                {
                    Check(_hand.VerticalScrollMode == ScrollContainer.ScrollMode.Auto && _hand.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled,
                        "La galería lateral no tiene desplazamiento independiente.");
                    Check(!(_hand.GetGlobalRect().Intersects(_inspector.GetGlobalRect())), "La mano tapa la lectura del inspector.");
                    Check(Math.Abs(card.Size.X - 75.6f) <= 1 && Math.Abs(card.Size.Y - 110.4f) <= 1,
                        "La galería lateral cambia el tamaño de las cartas completas.");
                }
                if (Short && index == 0 && _hand.GetVScrollBar().MaxValue > _hand.GetVScrollBar().Page + 1)
                {
                    var title = _contextTitle.Text; var selected = _selected.ToArray();
                    var readingPosition = _inspector.ReadingViewport.ScrollVertical;
                    var inspectorRect = _inspector.GetGlobalRect();
                    GuiInputTestDriver.ScrollDown(_hand); await Layout();
                    Check(_hand.ScrollVertical > 0 && _contextTitle.Text == title && _selected.SetEquals(selected) &&
                        _inspector.ReadingViewport.ScrollVertical == readingPosition && _inspector.GetGlobalRect() == inspectorRect,
                        "Desplazar la mano altera la selección o la lectura del inspector.");
                    _hand.ScrollVertical = 0; await Layout();
                }
                if (_inspector.DescriptionLabel.GetCombinedMinimumSize().Y > _inspector.ReadingViewport.Size.Y + 1)
                {
                    var fixedControls = new Control[] { _selection, _contextTitle, _inspector.Attributes, _contextCost, _decisionBar, _hand };
                    var positions = fixedControls.Select(c => c.GetGlobalRect()).ToArray();
                    var descriptionTop = _inspector.DescriptionLabel.GlobalPosition.Y;
                    GuiInputTestDriver.ScrollDown(_inspector.ReadingViewport); await Layout();
                    Check(_inspector.ReadingViewport.ScrollVertical > 0 && _inspector.DescriptionLabel.GlobalPosition.Y < descriptionTop,
                        "El gesto de lectura no desplaza el efecto largo.");
                    Check(fixedControls.Select((c, i) => c.GetGlobalRect() == positions[i]).All(v => v),
                        "Desplazar el efecto mueve la instrucción, cabecera, atributos, coste, botones o mano.");
                    var readingPosition = _inspector.ReadingViewport.ScrollVertical;
                    _inspector.SetHint(_selection.Text); await Layout();
                    Check(_inspector.ReadingViewport.ScrollVertical == readingPosition, "Actualizar la misma instrucción reinicia la lectura.");
                    scrollingCases++;
                    if (seat == 0 && index == 0) await Capture($"inspector-scrolled-{size.X}x{size.Y}");
                    _inspector.ReadingViewport.ScrollVertical = 0; await Layout();
                }
                CheckPendingCodeBounds();
                if (seat == 0 && index is 0 or 2 or 3) await Capture($"inspector-{definition.Id}-{size.X}x{size.Y}");
            }
            Check(_inspector.ClassificationLabel.Text == "Código Personaje" && _inspector.DescriptionLabel.Text == "Roba 2 cartas.", "Senshuni pierde la clasificación completa o repite su robo.");
            var selectedCard = _handCards[^1];
            Check(selectedCard.Chosen, "La carta debe estar seleccionada antes de hacer clic en el campo.");
            ClickEmptyField(); await Layout();
            Check(_selected.Count == 0 && !selectedCard.Chosen && !_inspector.HasCard,
                $"El campo vacío no deselecciona la carta de la mano en {size}.");
            if (!Short)
            {
                await ToSignal(GetTree().CreateTimer(0.65), SceneTreeTimer.SignalName.Timeout);
                Check(selectedCard.DrawnLift < 0.1f, $"La carta sigue elevada tras hacer clic en el campo vacío en {size}.");
            }
            SelectSkill(0); await Layout();
            var values = _inspector.Attributes.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToArray();
            Check(values.Contains("4") && values.Contains("6"), "Reverse no muestra el daño de su copia con contadores.");
            void CheckCounters(int relative, int slot, CardCounters expected)
            {
                var skill = (SkillTile)ZoneNode(relative, CardZone.Skill, slot)!;
                Check(skill.PresentedCounters == expected && _inspector.CounterBadge is { Visible: true } badge &&
                    badge.Counters == expected && badge.TooltipText == expected.Description,
                    $"La carta y el inspector de {skill.Skill!.Name} deben mostrar el mismo recuento.");
            }
            CheckCounters(0, 0, new(2, null));
            SelectSkill(1); await Layout();
            CheckCounters(0, 1, new(1, CardCatalog.DestinyCounterLimit));
            SelectField(1 - seat, FieldZone.Skill, 0);
            CheckCounters(1, 0, new(0, CardCatalog.IronwingRollCounterLimit));
            SelectField(1 - seat, FieldZone.Skill, 1);
            CheckCounters(1, 1, new(CardCatalog.TurboSwordCounterLimit, CardCatalog.TurboSwordCounterLimit));
            if (seat == 0 && size.X is 1440 or 844) await Capture($"card-counters-{size.X}x{size.Y}");
            SelectField(1 - seat, FieldZone.Equipment, -1);
            Check(_contextTitle.Text == CardCatalog.Pc.Name && !_inspector.Attributes.Visible && !_inspector.CounterBadge.Visible,
                "Consultar un equipo rival conserva atributos o contadores de otra carta.");
            Check(_moves.FindChildren("*", "Button", true, false).OfType<Button>().All(button => button.TooltipText != "Limpiar selección"),
                "Sigue apareciendo el botón para limpiar la selección.");
            Check(FieldCardId((1 - seat, FieldZone.Hand, 0)) == null, "El inspector tiene acceso a una identidad privada.");
            _hand!.EnsureControlVisible(hoverCard); await Layout();
            GuiInputTestDriver.Hover(hoverCard); await Layout();
            Check(_contextTitle.Text == CardCatalog.Pc.Name && _fieldSelection == (1 - seat, FieldZone.Equipment, -1),
                "Pasar el cursor sobre otra carta desplaza la selección fijada.");
            ClickEmptyField(); await Layout();
            CheckPanelSize();
            Check(_fieldSelection == null && _selected.Count == 0 && !_inspector.ClassificationLabel.Visible &&
                !_inspector.Attributes.Visible && !_inspector.DescriptionLabel.Visible && !_inspector.CounterBadge.Visible,
                $"El clic en el campo no limpia la selección o deja datos antiguos en {size}.");
            if (!Compact && !Short)
            {
                SelectSkill(0); await Layout();
                var emptySlot = (SkillTile)_root!.FindChild("Skill0_2", true, false);
                GuiInputTestDriver.Click(GetViewport(), emptySlot.GetGlobalRect().GetCenter()); await Layout();
                Check(!_inspector.HasCard && _selectedSkill < 0, "Un espacio de habilidad vacío no limpia la selección.");
            }
            Check(_snapshot!.Players[1 - seat].Hand.All(id => id == null) &&
                _root!.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(c => c.IsFaceDown), "El inspector revela cartas privadas del rival.");
            Check(game.SaveSnapshot() == before, "Consultar una carta o habilidad ejecuta una jugada.");

            rival.Equipment = CardCatalog.Sniper.Id; rival.EquipmentDie = 0; rival.EquipmentCounters = 2;
            player.Relikario = seat == 0 ? RelikarioCatalog.Yuno : RelikarioCatalog.Osamu;
            var lv2 = RelikarioCatalog.EvolutionFor(player.Relikario)!;
            PresentTestSnapshot(DuelSnapshot.For(game, seat, 402)); await Layout();
            equipment = (ZoneCardButton)ZoneNode(1, CardZone.Equipment, -1)!;
            SelectEquipment(1 - seat);
            var sniper = new CardCounters(2, CardCatalog.SniperCounterLimit);
            Check(equipment.PresentedCounters == sniper && CardCounterBadge.OnCard(equipment.CardRect, sniper) is var counterRect &&
                counterRect.HasArea() && equipment.CardRect.Encloses(counterRect) && _inspector.CounterBadge.Counters == sniper,
                "Mira de Sniper pierde el recuento al consultar una copia equipada.");
            ClearContext();
            Check(_evolveButton is { Disabled: true }, "La prueba requiere que el botón de Lv 2 esté desactivado.");
            var evolve = _evolveButton!;
            _arenaScroll?.EnsureControlVisible(evolve); await Layout();
            var movesBeforeHover = _moves.GetChildren().ToArray();
            GuiInputTestDriver.Hover(evolve); await Layout();
            var artwork = RelikarioArt.Card(lv2.Id, lv2.Level);
            var inlineArtwork = _inspector.FindChild("InspectorRelikarioArtwork", true, false) as TextureRect;
            var effects = _inspector.DescriptionLabel.Text;
            Check(artwork != null && _contextTitle.Text == $"{lv2.Name} · Lv 2" && effects.Contains(lv2.AbilityText) &&
                (seat != 0 || effects.Contains("2 vidas") && effects.Contains("+1 al D6 de ataque")) &&
                (Desktop ? _showcase is { Visible: true } && _showcase.FaceTexture == artwork
                    : inlineArtwork is { Visible: true } && inlineArtwork.Texture == artwork),
                $"Pasar el cursor sobre el botón desactivado no muestra la carta y los efectos de {lv2.Name} en {size}.");
            GuiInputTestDriver.Hover(_inspector.ReadingViewport); await Layout();
            Check(evolve.Disabled && _contextTitle.Text == $"{lv2.Name} · Lv 2" && _moves.GetChildren().SequenceEqual(movesBeforeHover),
                "La vista de Lv 2 se cierra al leer o altera las jugadas disponibles.");
            CheckPendingCodeBounds();
        }
        Check(scrollingCases > 0, "No se ejercitó la lectura de efectos largos.");
        ResetInteraction();
        GD.Print("PVP_CARD_INSPECTOR_PASS · cartas completas, clasificación, atributos por copia, descripción sin duplicados, coste único, privacidad, modo compacto, lectura independiente con cabecera/coste/controles fijos y cinco tamaños para ambos asientos.");
    }
}
