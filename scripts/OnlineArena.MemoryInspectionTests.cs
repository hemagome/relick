using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckMemoryInspection()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
        async Task Capture(string name)
        {
            if (capture == null || DisplayServer.GetName() == "headless") return;
            System.IO.Directory.CreateDirectory(capture);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, name + ".png"));
        }
        var embedded = GetViewport().GuiEmbedSubwindows;
        GetViewport().GuiEmbedSubwindows = true;
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var seat in new[] { 0, 1 })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size; await Layout();
                var game = new GameEngine(42);
                game.State.Players[0].Memory.AddRange([CardCatalog.Magnet.Id, CardCatalog.Senshuni.Id]);
                game.State.Players[1].Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Bonfire.Id]);
                foreach (var p in game.State.Players) p.CorruptedMemory.Add(CardCatalog.Destiny.Id);
                ResetAnimations(); PresentTestSnapshot(DuelSnapshot.For(game, seat, 410)); await Layout();
                var before = game.SaveSnapshot();
                foreach (var relative in new[] { 0, 1 })
                foreach (var corrupt in new[] { false, true })
                {
                    var button = (MemoryZoneButton)_root!.FindChild($"{(corrupt ? "CorruptedMemory" : "Memory")}Button{relative}", true, false);
                    _arenaScroll?.EnsureControlVisible(button); await Layout();
                    GuiInputTestDriver.Click(button); await Layout();
                    var dialog = GetChildren().OfType<AcceptDialog>().SingleOrDefault();
                    Check(dialog is { Visible: true }, $"No se abre la memoria {(corrupt ? "corrupta" : "normal")} del jugador {relative} en {size}, asiento {seat}.");
                    var inspectors = dialog!.FindChildren("*", "PanelContainer", true, false).OfType<CardInspector>().ToArray();
                    var cards = dialog.FindChildren("*", "Button", true, false).OfType<HandCard>().ToArray();
                    if (corrupt)
                    {
                        Check(inspectors.Length == 0 && cards.Length == 0, "Consultar corruptas revela sus identidades.");
                        Check(dialog.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.StartsWith("1 cartas corruptas")), "Falta el número de cartas corruptas.");
                    }
                    else
                    {
                        var ids = game.State.Players[relative == 0 ? seat : 1 - seat].Memory;
                        Check(cards.Select(c => c.Card.Id).SequenceEqual(ids), "La consulta muestra cartas de otra memoria.");
                        Check(inspectors.Length == 1, "La memoria no utiliza un único inspector.");
                        var view = (CardCollectionView)dialog.FindChild("CardCollectionView", true, false);
                        var inspector = inspectors.Single(); var instance = inspector.GetInstanceId();
                        for (var i = 0; i < cards.Length; i++)
                        {
                            view.Gallery.EnsureControlVisible(cards[i]); await Layout();
                            GuiInputTestDriver.Click(cards[i]); await Layout();
                            Check(dialog.Visible && view.SelectedIndex == i && cards[i].Chosen && cards.Count(c => c.Chosen) == 1,
                                "Consultar una carta cierra la memoria o pierde su selección única.");
                            Check(inspector.GetInstanceId() == instance && inspector.TitleLabel.Text == CardCatalog.Get(ids[i]).Name &&
                                inspector.DescriptionLabel.Text == CardInspectorText.Description(CardCatalog.Get(ids[i])), "Cambiar de carta no actualiza el mismo inspector.");
                            Check((view.Showcase != null) == !Short && (view.Showcase == null || view.Showcase.Card?.Id == ids[i] && view.Showcase.FaceTexture != null),
                                "La memoria no muestra la imagen de la carta consultada como el inspector del duelo.");
                        }
                        if (seat == 0 && relative == 0) await Capture($"memory-inspector-{size.X}x{size.Y}");
                    }
                    if (dialog.Size.X > size.X || dialog.Size.Y > size.Y)
                    {
                        var view = dialog.FindChild("CardCollectionView", true, false) as CardCollectionView;
                        throw new Exception($"La consulta desborda {size}: diálogo {dialog.Size}, vista {view?.Size}/{view?.GetCombinedMinimumSize()}, galería {view?.Gallery.Size}/{view?.Gallery.GetCombinedMinimumSize()}, inspector {view?.Inspector.Size}/{view?.Inspector.GetCombinedMinimumSize()}, lectura {view?.Inspector.ReadingViewport.GetCombinedMinimumSize()}.");
                    }
                    GuiInputTestDriver.Click(dialog.GetOkButton()); await Layout();
                    Check(!GodotObject.IsInstanceValid(dialog), "No se cierra la consulta de memoria.");
                }
                Check(game.SaveSnapshot() == before, "Consultar memorias modifica el duelo.");
                var memory = game.State.Players[seat].Memory;
                memory.Clear(); memory.AddRange([CardCatalog.Magnet.Id, CardCatalog.TimeBender.Id, CardCatalog.Destiny.Id, CardCatalog.Bonfire.Id]);
                memory.AddRange(Enumerable.Repeat(CardCatalog.Senshuni.Id, 32));
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 411)); await Layout();
                var full = (MemoryZoneButton)_root!.FindChild("MemoryButton0", true, false);
                _arenaScroll?.EnsureControlVisible(full); await Layout(); GuiInputTestDriver.Click(full); await Layout();
                var fullDialog = GetChildren().OfType<AcceptDialog>().Single();
                var collection = (CardCollectionView)fullDialog.FindChild("CardCollectionView", true, false);
                var allCards = fullDialog.FindChildren("*", "Button", true, false).OfType<HandCard>().ToArray();
                Check(allCards.Select(c => c.Card.Id).SequenceEqual(memory) && fullDialog.FindChildren("*", "PanelContainer", true, false).OfType<CardInspector>().Count() == 1,
                    "Una memoria grande pierde copias o crea inspectores adicionales.");
                var fixedInspector = collection.Inspector.GetGlobalRect();
                collection.Gallery.EnsureControlVisible(allCards[^1]); await Layout();
                GuiInputTestDriver.Click(allCards[^1]); await Layout();
                Check(collection.Gallery.ScrollVertical > 0 && collection.Inspector.GetGlobalRect() == fixedInspector && collection.SelectedIndex == memory.Count - 1,
                    "Desplazar una memoria grande mueve el inspector o confunde copias repetidas.");
                Check(fullDialog.Size.X <= size.X && fullDialog.Size.Y <= size.Y && collection.Size.X + 1 >= collection.GetCombinedMinimumSize().X,
                    $"La galería de memoria desborda {size}: diálogo {fullDialog.Size}, vista {collection.Size}, mínimo {collection.GetCombinedMinimumSize()}.");
                collection.Gallery.EnsureControlVisible(allCards[1]); await Layout(); GuiInputTestDriver.Click(allCards[1]); await Layout();
                var galleryScroll = collection.Gallery.ScrollVertical; var titlePosition = collection.Inspector.TitleLabel.GlobalPosition;
                GuiInputTestDriver.ScrollDown(collection.Inspector.ReadingViewport); await Layout();
                Check(collection.Gallery.ScrollVertical == galleryScroll && collection.Inspector.TitleLabel.GlobalPosition == titlePosition,
                    "Leer un efecto desplaza la galería o la cabecera del inspector.");
                var directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-screenshot-dir="))?.Split('=', 2)[1];
                if (directory != null && DisplayServer.GetName() != "headless" && seat == 0)
                {
                    System.IO.Directory.CreateDirectory(directory);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"memory-gallery-{size.X}x{size.Y}.png"));
                }
                GuiInputTestDriver.Click(fullDialog.GetOkButton()); await Layout();

                // A revealed rival card must not remain in the inspector when a hidden card is selected.
                ShowCards("Mano rival", [null, CardCatalog.Senshuni.Id]); await Layout();
                var mixed = GetChildren().OfType<AcceptDialog>().Single();
                var mixedView = (CardCollectionView)mixed.FindChild("CardCollectionView", true, false);
                var hidden = (OpponentHandCard)mixed.FindChild("CollectionCard0", true, false);
                Check(hidden.IsFaceDown && mixedView.Inspector.HasCard, "La consulta no distingue una carta revelada de una oculta.");
                GuiInputTestDriver.Click(hidden); await Layout();
                Check(!mixedView.Inspector.HasCard && mixedView.Inspector.DescriptionLabel.Text.Length == 0 && mixedView.Inspector.CostLabel.Text.Length == 0,
                    "La carta oculta conserva información del inspector anterior.");
                GuiInputTestDriver.Click(mixed.GetOkButton()); await Layout();
                var chosenCopy = -1;
                ShowCards("Elegir copia", [CardCatalog.Attack.Id, CardCatalog.Attack.Id], select: index => chosenCopy = index); await Layout();
                var selectable = GetChildren().OfType<AcceptDialog>().Single();
                Check(chosenCopy == -1, "La selección inicial del inspector ejecuta una selección de juego.");
                var otherCopy = (HandCard)selectable.FindChild("CollectionCard1", true, false);
                GuiInputTestDriver.Hover(otherCopy); await Layout();
                Check(chosenCopy == -1 && otherCopy.Chosen && selectable.Visible, "La vista previa de una copia confirma la jugada.");
                GuiInputTestDriver.Click(otherCopy); await Layout();
                Check(chosenCopy == 1 && !GodotObject.IsInstanceValid(selectable), "La galería confunde el índice de dos copias iguales al seleccionar un objetivo.");
                game.State.Players[seat].Memory.Clear();
                PresentTestSnapshot(DuelSnapshot.For(game, seat, 411)); await Layout();
                var empty = (MemoryZoneButton)_root!.FindChild("MemoryButton0", true, false);
                _arenaScroll?.EnsureControlVisible(empty); await Layout(); GuiInputTestDriver.Click(empty); await Layout();
                var emptyDialog = GetChildren().OfType<AcceptDialog>().Single();
                Check(emptyDialog.Visible && emptyDialog.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == "No hay cartas en esta zona."), "La memoria vacía no se puede consultar.");
                GuiInputTestDriver.Click(emptyDialog.GetOkButton()); await Layout();
            }
        }
        finally { GetViewport().GuiEmbedSubwindows = embedded; }
        GD.Print("PVP_MEMORY_INSPECTION_PASS · galería completa, inspector único actualizable, selección por copia, lectura y desplazamiento independientes, memoria rival, privacidad, objetivos, zona vacía y cierre; cuatro tamaños y ambos asientos.");
    }
}
