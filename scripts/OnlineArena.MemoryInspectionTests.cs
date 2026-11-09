using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    internal async Task CheckMemoryInspection()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        async Task Layout() { for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
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
                        Check(inspectors.Length == ids.Count && inspectors.Select(i => i.TitleLabel.Text).SequenceEqual(ids.Select(id => CardCatalog.Get(id).Name)), "La memoria pierde los inspectores de sus cartas.");
                        Check(inspectors.Select(i => i.DescriptionLabel.Text).SequenceEqual(ids.Select(id => CardInspectorText.Description(CardCatalog.Get(id)))), "La consulta pierde los efectos de las cartas.");
                    }
                    GuiInputTestDriver.Click(dialog.GetOkButton()); await Layout();
                    Check(!GodotObject.IsInstanceValid(dialog), "No se cierra la consulta de memoria.");
                }
                Check(game.SaveSnapshot() == before, "Consultar memorias modifica el duelo.");
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
        GD.Print("PVP_MEMORY_INSPECTION_PASS · clic, cartas y efectos, memoria rival, corruptas ocultas, zona vacía y cierre; cuatro tamaños y ambos asientos.");
    }
}
