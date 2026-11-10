using Godot;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private async Task CheckLobbyPolish()
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
        var clipboardAvailable = DisplayServer.HasFeature(DisplayServer.Feature.Clipboard);
        var previousClipboard = clipboardAvailable ? DisplayServer.ClipboardGet() : "";
        _content.Show();
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            {
                GetWindow().ContentScaleSize = size; await Layout(); _localError = _lobbyNotice = "";
                PresentLobbyState(null, false, true, RoomRequest.None, "", "Conectando…"); await Layout();
                Check(_create.Disabled && _join.Disabled && _reconnect.Visible && _reconnect.Disabled, "Conectar permite solicitudes repetidas.");
                PresentLobbyState(null, false, false, RoomRequest.None, "", "Sin conexión");
                Check(!_reconnect.Disabled && _status.Text.Contains("Reconectar"), "La desconexión no ofrece un siguiente paso.");
                PresentLobbyState(null, true, false, RoomRequest.Create, "", "Creando sala…");
                Check(_create.Disabled && _join.Disabled && !_name.Editable && !_code.Editable && _create.Text.Contains("Creando"), "La solicitud pendiente sigue editable o se puede repetir.");
                foreach (var error in new[] { "No se encontró la sala.", "La sala ya está completa." })
                {
                    PresentLobbyState(null, true, false, RoomRequest.None, error, error);
                    await Layout();
                    Check(!_join.Disabled && _status.Text != error && (_status.Text.Contains("Revisa") || _status.Text.Contains("Pide")), "Un error de código no permite corregirlo o no explica cómo seguir.");
                    Check(_lobbyPage.GetGlobalRect().Intersects(_status.GetGlobalRect()), "El error queda fuera del área visible.");
                }
                if (size.X is 1440 or 700) await Capture($"lobby-error-{size.X}x{size.Y}");
                var room = new RoomInfo { Code = "AB12CD34", MySeat = 0, PlayerOne = "Ana", PlayerTwo = "", Dealt = false };
                PresentLobbyState(room, true, false, RoomRequest.None, "", "Sala creada."); await Layout();
                var scroll = (ScrollContainer)_content.GetChild(0); scroll.EnsureControlVisible(_copy); await Layout();
                GuiInputTestDriver.Click(_copy);
                Check(clipboardAvailable ? _copy.Text == "Copiado ✓" && DisplayServer.ClipboardGet() == room.Code : _copy.Text != "Copiado ✓" && _localError.Length > 0,
                    "Copiar no confirma el código exacto o anuncia éxito sin portapapeles.");
                if (size.X is 1440 or 700) { await Layout(); await Capture($"lobby-copy-{size.X}x{size.Y}"); }
                _copyFeedbackSeconds = 0; _localError = "";
                PresentLobbyState(room, true, false, RoomRequest.None, "", "Sala creada.");
                Check(_copy.Text == "Copiar código", "La confirmación de copia no se restablece.");
                PresentLobbyState(room, true, false, RoomRequest.Leave, "", "Cerrando sala…");
                Check(_copy.Disabled && _leave.Disabled && _leave.Text.Contains("Cerrando"), "Cerrar permite enviar o compartir otra vez.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "Sala cerrada."); await Layout();
                Check(!_roomPanel.Visible && _entryCards.Visible && _name.Text.Length > 0 && !_create.Disabled && !_join.Disabled, "El cierre no recupera la entrada al lobby.");
                Check(_entryCards.GetGlobalRect().End.X <= Size.X + 1, "El lobby desborda horizontalmente.");
            }
        }
        finally { if (clipboardAvailable) DisplayServer.ClipboardSet(previousClipboard); _localError = ""; _content.Hide(); }
        GD.Print("PVP_LOBBY_POLISH_PASS · conexión/reconexión, solicitudes bloqueadas, errores accionables, copia sin falsos positivos, cierre y recuperación de entrada; cuatro tamaños.");
    }
}
