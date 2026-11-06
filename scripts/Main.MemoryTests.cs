using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private async void MemorySmokeTest()
    {
        try
        {
            _helpOpen = true; _die.SetProcess(false); _yuno.SetProcess(false); _enemyYuno.SetProcess(false);
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            AcceptDialog Dialog() => GetChildren().OfType<AcceptDialog>().Single(d => !d.IsQueuedForDeletion());
            void Press(Node root, string name)
            {
                var button = root.FindChild(name, true, false) as Button ?? throw new InvalidOperationException($"Falta {name}.");
                Check(!button.Disabled, $"No se puede pulsar {name}.");
                button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            async Task CloseDialog()
            {
                Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed); _helpOpen = true;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            BindGame(new GameEngine(42));
            foreach (var p in _game.State.Players)
            {
                p.Memory.AddRange(new[] { "attack", "lucky-pendant", "lucky-pendant", "magnet-shuffle", "attack" });
                p.CorruptedMemory.AddRange(new[] { "lucky-pendant", "lucky-pendant" });
            }
            Render();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var before = System.Text.Json.JsonSerializer.Serialize(_game.State);
            foreach (var owner in new[] { 0, 1 })
            {
                var p = _game.State.Players[owner];
                var pile = (MemoryZoneButton)_root.FindChild($"MemoryButton{owner}", true, false);
                var corruptPile = (MemoryZoneButton)_root.FindChild($"CorruptedMemoryButton{owner}", true, false);
                Check(pile.Amount == p.Memory.Count && pile.TopCard?.Id == p.Memory.Last() && !pile.Corrupted,
                    "La memoria incluye cartas corruptas o no muestra su miniatura.");
                Check(corruptPile.Amount == p.CorruptedMemory.Count && corruptPile.Corrupted && corruptPile.TopCard == null,
                    "El indicador de corruptas no está separado o expone identidades.");
                _helpOpen = false; Press(_root, $"MemoryButton{owner}");
                var gallery = Dialog().FindChildren("*", "Button", true, false).OfType<HandCard>().ToArray();
                Check(gallery.Select(c => c.Card.Id).SequenceEqual(p.Memory) && gallery.Any(c => c.HasAttackArtwork),
                    "La memoria no muestra todas sus cartas con la ilustración correspondiente.");
                Press(Dialog(), "MemoryCard1");
                Check((Dialog().FindChild("ZoneCardPreview", true, false) as HandCard)?.Card.Id == "lucky-pendant", "No se puede ampliar una carta en memoria.");
                Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed);
                Check(Dialog().FindChild("MemoryGallery", true, false) != null, "Cerrar la carta no regresa a memoria.");
                Press(Dialog(), "CorruptedMemoryTab");
                var backs = Dialog().FindChildren("*", "Button", true, false).OfType<CardBackButton>().ToArray();
                Check(backs.Length == p.CorruptedMemory.Count && backs.All(c => c.HasBackArtwork) &&
                    !Dialog().FindChildren("*", "Button", true, false).OfType<HandCard>().Any(), "Las corruptas no muestran solo reversos.");
                Press(Dialog(), "CorruptedMemoryCard1");
                Check(backs[1].Chosen && !backs[0].Chosen, "No se puede seleccionar un reverso sin revelarlo.");
                Check(backs.All(c => !c.TooltipText.Contains("Recuperación") && !c.TooltipText.Contains("Recarga")), "Una carta corrupta expone su identidad.");
                await CloseDialog();
                _helpOpen = false; Press(_root, $"CorruptedMemoryButton{owner}");
                Check(Dialog().FindChildren("*", "Button", true, false).OfType<CardBackButton>().Count() == 2,
                    "No se puede abrir directamente la zona corrupta.");
                await CloseDialog();
            }
            Check(System.Text.Json.JsonSerializer.Serialize(_game.State) == before, "Consultar/seleccionar cartas mueve cartas o consume acciones.");

            var player = _game.State.Players[0]; player.Memory.Clear(); player.Memory.AddRange(Enumerable.Repeat("lucky-pendant", 100));
            player.CorruptedMemory.Clear(); player.CorruptedMemory.AddRange(Enumerable.Repeat("lucky-pendant", 100)); Render();
            _helpOpen = false; ShowMemory(0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var list = (ScrollContainer)Dialog().FindChild("MemoryGallery", true, false);
            Check(list.FindChildren("*", "Button", true, false).OfType<HandCard>().Count() == 100 && list.GetVScrollBar().MaxValue > list.GetVScrollBar().Page,
                "La memoria grande oculta cartas o no permite desplazarse.");
            list.ScrollVertical = 250; var position = list.ScrollVertical;
            Press(Dialog(), "MemoryCard8"); Dialog().EmitSignal(AcceptDialog.SignalName.Confirmed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(((ScrollContainer)Dialog().FindChild("MemoryGallery", true, false)).ScrollVertical == position,
                "Ampliar una carta pierde la posición de la galería.");
            Press(Dialog(), "CorruptedMemoryTab");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(Dialog().FindChildren("*", "Button", true, false).OfType<CardBackButton>().Count() == 100,
                "La zona corrupta introduce un límite de cartas.");
            await CloseDialog();
            _game.State.Phase = Phase.Actions; _game.State.CurrentPlayer = 0; _game.State.ActionsLeft = 2; player.Relikia = 400;
            var beforeReplay = System.Text.Json.JsonSerializer.Serialize(_game.State);
            Check(!_game.UseRelikiaEffect(0, RelikiaEffect.ReplayCode).Success && beforeReplay == System.Text.Json.JsonSerializer.Serialize(_game.State),
                "Se permite activar equipos de memoria como si fueran códigos.");
            var available = GetViewportRect().Size - new Vector2(48, 30);
            var minimum = _root.GetCombinedMinimumSize();
            Check(minimum.X <= available.X + 1 && minimum.Y <= available.Y + 1, $"Las zonas nuevas desbordan el tablero: {minimum} / {available}");
            player.Memory.Clear(); player.CorruptedMemory.Clear(); Render();
            _helpOpen = false; ShowMemory(0);
            Check(!Dialog().FindChildren("*", "Button", true, false).OfType<HandCard>().Any(), "La memoria vacía conserva cartas visuales.");
            Press(Dialog(), "CorruptedMemoryTab");
            Check(!Dialog().FindChildren("*", "Button", true, false).OfType<CardBackButton>().Any(), "Las corruptas vacías conservan reversos.");
            await CloseDialog();
            GD.Print("MEMORY_SMOKE_PASS · miniaturas, detalle y regreso, dos jugadores, zonas separadas, reversos seleccionables sin identidad, 100 cartas, efectos de Relikia y ajuste de ventana.");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
