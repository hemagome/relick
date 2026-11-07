using Godot;

namespace Relick;

public partial class OnlineLobby
{
    // Offline visual review; never connects or creates a room.
    private async void LobbyVisualPreview()
    {
        try
        {
            var directory = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--lobby-preview-dir=")).Split('=', 2)[1];
            System.IO.Directory.CreateDirectory(directory);
            async Task Layout()
            {
                for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            foreach (var size in new[] { new Vector2I(1280, 800), new(844, 390), new(700, 390) })
            {
                GetWindow().Size = size; GetWindow().ContentScaleSize = size;
                await Layout();
                _create.Disabled = _join.Disabled = false;
                _status.Text = "Vista previa · Sin conexión";
                UpdateLobbyPresentation(false);
                await Layout();
                if (_entryCards.GetGlobalRect().End.X > Size.X || _entryCards.GetGlobalRect().Position.X < 0)
                    throw new Exception($"Las tarjetas desbordan en {size}.");
                if (Size != (Vector2)size) throw new Exception($"Tamaño lógico inesperado: {Size}, esperado {size}.");
                GuiInputTestDriver.ReplaceText(_name, "Jugador");
                GuiInputTestDriver.ReplaceText(_code, "AB12CD34");
                _code.Text = ""; _code.ReleaseFocus();
                var scroll = (ScrollContainer)_content.GetChild(0); scroll.ScrollVertical = 0;
                await Layout();
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"lobby-{size.X}x{size.Y}.png"));
                }
                UpdateLobbyPresentation(true);
                _room.Text = "Sala · AB12CD34";
                _players.Text = "Jugador  vs  Esperando rival…\nTu lugar: jugador 1";
                _preparation.Text = "Comparte el código con tu rival para preparar las manos.";
                await Layout();
                if (_entryCards.Visible || !_roomPanel.Visible)
                    throw new Exception("La espera muestra controles de otro estado.");
                if (_copy.GetGlobalRect().End.X > Size.X) throw new Exception("Copiar código desborda la sala.");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, $"waiting-{size.X}x{size.Y}.png"));
                }
                GD.Print($"LOBBY_LAYOUT_PASS · {size} · campos accesibles, sin desbordamiento horizontal.");
            }
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("LOBBY_PREVIEW_FAIL · " + error); GetTree().Quit(1); }
    }
}
