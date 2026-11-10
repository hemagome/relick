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
                Check(_create.Disabled && _join.Disabled && !_connectionNotice.Visible, "La conexión inicial muestra un aviso de desconexión o permite solicitudes.");
                PresentLobbyState(null, false, false, RoomRequest.None, "", "Sin conexión");
                await Layout();
                Check(_connectionNotice.IsVisibleInTree() && !_reconnect.Disabled && _connectionNoticeDescription.Text.Contains("Reconectar"), "La desconexión no muestra el cuadro con un siguiente paso.");
                var notice = _connectionNoticePanel.GetGlobalRect();
                Check(notice.Position.X >= 0 && notice.Position.Y >= 0 && notice.End.X <= Size.X && notice.End.Y <= Size.Y,
                    "El cuadro de desconexión sale de la pantalla.");
                Check(_reconnect.HasFocus(), "El aviso no toma el foco del teclado.");
                GuiInputTestDriver.Click(_name);
                Check(!_name.HasFocus(), "El aviso deja pasar clics a los campos de la sala.");
                var retryClicks = 0;
                void Retry() => retryClicks++;
                _reconnect.Pressed -= Connect; _reconnect.Pressed += Retry;
                try
                {
                    GuiInputTestDriver.Click(_reconnect);
                    Check(retryClicks == 1, "Reconectar no recibe un clic dentro del cuadro.");
                }
                finally { _reconnect.Pressed -= Retry; _reconnect.Pressed += Connect; }
                if (size.X is 1440 or 700) await Capture($"lobby-disconnected-{size.X}x{size.Y}");
                PresentLobbyState(null, false, true, RoomRequest.None, "", "Conectando…"); await Layout();
                Check(_connectionNotice.Visible && _reconnect.Disabled && _connectionNoticeTitle.Text.Contains("Reconectando"), "El cuadro desaparece antes de recuperar la conexión o permite reintentos repetidos.");
                PresentLobbyState(null, false, false, RoomRequest.None, "", "No se pudo conectar.");
                Check(_connectionNotice.Visible && !_reconnect.Disabled, "Un reintento fallido no permite volver a conectar.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "Conectado"); await Layout();
                Check(!_connectionNotice.Visible && !_status.GetParent<Control>().Visible && _status.Text.Length == 0,
                    "La recuperación no retira el cuadro o sigue anunciando Conectado.");
                Check(!_quickMatch.Disabled && _quickMatch.Text == "Buscar rival", "No se puede iniciar partida rápida.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "Conectado", quickMatchAvailable: false);
                Check(_quickMatch.Disabled && !_create.Disabled && !_join.Disabled,
                    "Un servidor sin cola impide usar las salas privadas.");
                PresentLobbyState(null, true, false, RoomRequest.FindQuickMatch, "", "Iniciando búsqueda…"); await Layout();
                Check(_quickMatch.Disabled && _create.Disabled && _join.Disabled && !_name.Editable,
                    "Iniciar búsqueda permite repetir solicitudes o entrar a una sala privada.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "Buscando rival…", true); await Layout();
                Check(!_quickMatch.Disabled && _quickMatch.Text == "Cancelar búsqueda" && _quickMatchStatus.Visible && _create.Disabled && _join.Disabled && !_code.Editable,
                    "La búsqueda no ofrece cancelar o mantiene acciones privadas activas.");
                _lobbyPage.EnsureControlVisible(_quickMatch); await Layout();
                var quickClicks = 0;
                void QuickClick() => quickClicks++;
                _quickMatch.Pressed -= QuickMatch; _quickMatch.Pressed += QuickClick;
                try { GuiInputTestDriver.Click(_quickMatch); Check(quickClicks == 1, "Cancelar búsqueda no recibe el clic."); }
                finally { _quickMatch.Pressed -= QuickClick; _quickMatch.Pressed += QuickMatch; }
                if (size.X is 1440 or 700) await Capture($"lobby-searching-{size.X}x{size.Y}");
                PresentLobbyState(null, true, false, RoomRequest.CancelQuickMatch, "", "Cancelando búsqueda…", true);
                Check(_quickMatch.Disabled && _quickMatch.Text == "Cancelando…", "Cancelar permite solicitudes repetidas.");
                PresentLobbyState(null, true, false, RoomRequest.None, "", "Búsqueda cancelada."); await Layout();
                Check(!_quickMatch.Disabled && !_create.Disabled && !_join.Disabled && _name.Editable && !_quickMatchStatus.Visible,
                    "La cancelación no recupera el lobby.");
                _lobbyNotice = "";
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
                PresentLobbyState(room, false, false, RoomRequest.None, "", "Sin conexión"); await Layout();
                Check(_connectionNotice.Visible && _copy.Disabled && _leave.Disabled && _roomPanel.Visible,
                    "Perder conexión en una sala borra la vista o permite operaciones.");
                PresentLobbyState(room, true, false, RoomRequest.None, "", "Conectado"); await Layout();
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
        GD.Print("PVP_LOBBY_POLISH_PASS · sin mensaje Conectado, cuadro de desconexión y reintentos, recuperación, solicitudes bloqueadas, errores accionables, copia y cierre; cuatro tamaños.");
    }
}
