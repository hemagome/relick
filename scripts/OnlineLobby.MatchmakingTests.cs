using Godot;
using Relick.Network;
using Relick.Network.Types;

namespace Relick;

public partial class OnlineLobby
{
    private async Task CheckMatchmakingAnimation()
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
        var originalReducedMotion = _matchLink.ReducedMotion;
        _content.Show();
        try
        {
            foreach (var size in new[] { new Vector2I(1440, 1024), new(1024, 768), new(844, 390), new(700, 390) })
            foreach (var reduced in new[] { false, true })
            {
                GetWindow().ContentScaleSize = size; await Layout();
                _localError = _lobbyNotice = ""; _quickMatchRequested = false; _matchConfirmationRoom = "";
                _matchLink.ReducedMotion = reduced;
                PresentLobbyState(null, true, false, RoomRequest.None, "", ""); await Layout();
                var cancelRect = _quickMatch.GetGlobalRect();
                Check(!_matchFeedback.Visible && !_matchLink.IsProcessing(), "La animación procesa cuando no hay búsqueda.");
                PresentLobbyState(null, true, false, RoomRequest.FindQuickMatch, "", "Iniciando búsqueda…"); await Layout();
                Check(_matchLink.State == MatchmakingLinkState.Starting && !_matchLink.IsProcessing(), "Se anuncia una búsqueda activa antes de confirmarla.");
                HoldForQuickMatch(null, true, true, RoomRequest.None, 0);
                PresentLobbyState(null, true, false, RoomRequest.None, "", "", true); await Layout();
                _searchSeconds = 12; PresentLobbyState(null, true, false, RoomRequest.None, "", "", true);
                Check(_quickMatchStatus.Text.Contains("00:12") && !_quickMatch.Disabled && _quickMatch.Text == "Cancelar búsqueda", "La animación oculta el tiempo o impide cancelar.");
                Check(_quickMatch.GetGlobalRect() == cancelRect, "La búsqueda desplaza el botón de cancelar.");
                Check(_matchLink.IsProcessing() != reduced, "No se respeta la preferencia de movimiento.");
                _matchLink.SetProcess(false); var elapsed = _matchLink.PlaybackSeconds;
                _matchLink.AdvancePlayback(0.4);
                Check(reduced ? _matchLink.PlaybackSeconds == elapsed : _matchLink.PlaybackSeconds != elapsed, "La variante estática se mueve o el ciclo no avanza.");
                var preserved = _matchLink.PlaybackSeconds;
                PresentLobbyState(null, true, false, RoomRequest.None, "", "", true);
                Check(_matchLink.PlaybackSeconds == preserved, "Actualizar el estado reinicia el ciclo de búsqueda.");
                if (!reduced && size.X is 1440 or 700) { await Layout(); await Capture($"match-searching-{size.X}x{size.Y}"); }
                PresentLobbyState(null, false, false, RoomRequest.None, "", "", true); await Layout();
                elapsed = _matchLink.PlaybackSeconds; _matchLink.AdvancePlayback(4);
                Check(!_matchLink.IsProcessing() && _matchLink.PlaybackSeconds == elapsed, "La animación sigue avanzando sin conexión.");
                PresentLobbyState(null, true, false, RoomRequest.CancelQuickMatch, "", "", true);
                Check(_matchLink.State == MatchmakingLinkState.Canceling && !_matchLink.IsProcessing() && _quickMatch.Disabled, "Cancelar mantiene activo el ciclo o permite repetir la solicitud.");
                HoldForQuickMatch(null, true, false, RoomRequest.None, 0);
                PresentLobbyState(null, true, false, RoomRequest.None, "", "");
                var room = new RoomInfo { Code = "AB12CD34", Dealt = true, MySeat = 0, PlayerOne = "Ana", PlayerTwo = "Beto" };
                Check(!HoldForQuickMatch(room, true, false, RoomRequest.None, 0), "Una sala privada o una búsqueda cancelada inventa un emparejamiento.");
                // The second player can be paired immediately, without ever receiving a queued row.
                HoldForQuickMatch(null, true, false, RoomRequest.FindQuickMatch, 0);
                var confirmations = PlayedMatchConfirmations;
                Check(HoldForQuickMatch(room, true, false, RoomRequest.None, 0) && PlayedMatchConfirmations == confirmations + 1,
                    "El emparejamiento inmediato pierde la confirmación.");
                _matchLink.SetProcess(false); _matchLink.AdvancePlayback(0.15); elapsed = _matchLink.PlaybackSeconds;
                Check(HoldForQuickMatch(room, true, false, RoomRequest.None, 0.1) && _matchLink.State == MatchmakingLinkState.Matched &&
                    _matchLink.PlaybackSeconds == elapsed && _quickMatchStatus.Text == "Rival encontrado", "La confirmación reinicia la animación o no identifica el estado real.");
                Check(HoldForQuickMatch(room, false, false, RoomRequest.None, 8), "Un corte de conexión consume la confirmación antes de recuperar la sala.");
                Check(HoldForQuickMatch(room, true, false, RoomRequest.None, 0), "No se reanuda la confirmación tras recuperar la conexión.");
                if (!reduced && size.X is 1440 or 700) { _matchLink.AdvancePlayback(0.3); await Layout(); await Capture($"match-found-{size.X}x{size.Y}"); }
                Check(!HoldForQuickMatch(room, true, false, RoomRequest.None, MatchmakingLink.ConfirmationSeconds), "La animación retrasa indefinidamente la preparación.");
                Check(!HoldForQuickMatch(room, true, false, RoomRequest.None, 1) && PlayedMatchConfirmations == confirmations + 1, "Se repite una confirmación ya presentada.");
                PresentLobbyState(room, true, false, RoomRequest.None, "", ""); await Layout();
                Check(!_matchFeedback.IsVisibleInTree() && !_matchLink.IsProcessing(), "Entrar a la sala deja una animación activa.");
                HoldForQuickMatch(null, true, false, RoomRequest.None, 0);
            }
        }
        finally
        {
            _matchLink.ReducedMotion = originalReducedMotion;
            _quickMatchRequested = false; _matchConfirmationRoom = ""; _matchConfirmationRemaining = 0;
            PresentLobbyState(null, true, false, RoomRequest.None, "", ""); _content.Hide();
        }
        GD.Print("PVP_MATCHMAKING_ANIMATION_PASS · búsqueda sin progreso ficticio, cancelación fija, pausa/desconexión, movimiento reducido, emparejamiento inmediato y confirmado, transición acotada y sin repetición; cuatro tamaños.");
    }
}
