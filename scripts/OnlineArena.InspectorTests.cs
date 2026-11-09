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
        foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(1280, 480), new(844, 390), new(700, 390) })
        foreach (var seat in new[] { 0, 1 })
        {
            GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
            var game = PendingCodeFixture(seat, CardCatalog.Senshuni.Id);
            var player = game.State.Players[seat];
            player.Hand.Clear(); player.Hand.AddRange([CardCatalog.Magnet.Id, CardCatalog.Destiny.Id, CardCatalog.Bonfire.Id, CardCatalog.Senshuni.Id]);
            player.Skills.AddRange([CardCatalog.Reverse.Id, CardCatalog.Destiny.Id]);
            player.SkillCounters[0] = 2; player.SkillCounters[1] = 1;
            game.State.Players[1 - seat].Equipment = CardCatalog.Pc.Id;
            ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 401)); await Layout();
            var before = game.SaveSnapshot();
            var compactHeight = _inspector.Size.Y;
            Check(!_inspector.ReadingViewport.Visible && !_inspector.TitleLabel.Visible && compactHeight < 96,
                "Sin selección el inspector reserva espacio para una carta inexistente.");
            if (seat == 0) await Capture($"inspector-empty-{size.X}x{size.Y}");
            foreach (var index in new[] { 0, 1, 2, 3 })
            {
                var card = _handCards[index]; _hand!.EnsureControlVisible(card); await Layout();
                card.Disabled = false;
                try { GuiInputTestDriver.Click(card); }
                catch (Exception e) { throw new Exception($"Inspector {size}, asiento {seat}, carta {index}: {e.Message}", e); }
                await Layout();
                var definition = CardCatalog.Get(player.Hand[index]);
                Check(_contextTitle.Text == definition.Name, "El inspector no sigue la carta seleccionada.");
                Check(_inspector.Attributes.Visible == (definition.Kind == CardKind.Skill), "Se duplican valores del efecto como atributos de equipo o código.");
                if (definition.Kind == CardKind.Skill)
                    Check(!_inspector.DescriptionLabel.Text.Contains("Daño normal:") && !_inspector.DescriptionLabel.Text.Contains("Premio:"), "El inspector repite los atributos en la descripción.");
                else Check(_inspector.DescriptionLabel.Text == definition.Text, "El inspector pierde condiciones de la carta.");
                Check(card.CustomMinimumSize == new Vector2(126, 184) * (Short ? 0.6f : 1), "El inspector cambia el tamaño o el arte completo de la mano.");
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
                foreach (var field in new Control[] { _selection, _contextTitle, _inspector.Attributes, _inspector.CounterLabel, _contextCost })
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
                    var fixedControls = new Control[] { _selection, _contextTitle, _inspector.Attributes, _contextCost, _inspectorActions, _hand };
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
            ClearContext(); SelectSkill(0); await Layout();
            var values = _inspector.Attributes.FindChildren("*", "Label", true, false).OfType<Label>().Select(l => l.Text).ToArray();
            Check(values.Contains("4") && values.Contains("6"), "Reverse no muestra el daño de su copia con contadores.");
            SelectSkill(1); await Layout();
            Check(_inspector.CounterLabel.Visible && _inspector.CounterLabel.Text == "Contadores: 1/2", "Destiny no muestra los contadores autorizados de su copia.");
            SelectField(1 - seat, FieldZone.Equipment, -1);
            Check(_contextTitle.Text == CardCatalog.Pc.Name && !_inspector.Attributes.Visible && !_inspector.CounterLabel.Visible,
                "Consultar un equipo rival conserva atributos o contadores de otra carta.");
            Check(FieldCardId((1 - seat, FieldZone.Hand, 0)) == null, "El inspector tiene acceso a una identidad privada.");
            ClearContext(); await Layout();
            Check(!_inspector.ClassificationLabel.Visible && !_inspector.Attributes.Visible && !_inspector.DescriptionLabel.Visible && !_inspector.CounterLabel.Visible,
                "Limpiar la selección deja datos antiguos del inspector.");
            Check(_snapshot!.Players[1 - seat].Hand.All(id => id == null) &&
                _root!.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>().All(c => c.IsFaceDown), "El inspector revela cartas privadas del rival.");
            Check(game.SaveSnapshot() == before, "Consultar una carta o habilidad ejecuta una jugada.");
        }
        Check(scrollingCases > 0, "No se ejercitó la lectura de efectos largos.");
        ResetInteraction();
        GD.Print("PVP_CARD_INSPECTOR_PASS · cartas completas, clasificación, atributos por copia, descripción sin duplicados, coste único, privacidad, modo compacto, lectura independiente con cabecera/coste/controles fijos y cinco tamaños para ambos asientos.");
    }
}
