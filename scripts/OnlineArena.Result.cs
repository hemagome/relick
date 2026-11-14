using Godot;
using Relick.Network;

namespace Relick;

public partial class OnlineArena
{
    private PanelContainer? _resultPanel;
    private Label? _resultStatus, _resultCloseHint;
    private Button? _resultReturn;
    private bool _resultDismissed;
    private bool ResultSettled => !IsAnimating && _pendingPortraits.Count == 0 && _portraits.All(p => !p.IsActionPlaying);

    private void BuildResultPanel(VBoxContainer parent)
    {
        _resultPanel = null; _resultReturn = null; _resultStatus = null;
        if (_snapshot?.Winner is not int winner) return;
        var s = _snapshot;
        var panel = new PanelContainer { Name = "DuelResultPanel", Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", RelickVisuals.Frame(RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame));
        parent.AddChild(panel); _resultPanel = panel;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var title = ArenaPresentation.Text(winner == -1 ? "EMPATE" : winner == s.MySeat ? "VICTORIA" : "DERROTA", Short ? 22 : 28,
            winner == -1 ? BoardArt.Gold : winner == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral);
        title.Name = "DuelResultTitle"; column.AddChild(title);
        var reading = new ScrollContainer { Name = "DuelResultReading", CustomMinimumSize = new(0, 40),
            SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(reading);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 8); reading.AddChild(text);
        var reason = ArenaPresentation.Text(s.EndReason ?? "Resultado confirmado por el servidor.", Short ? 14 : 16);
        reason.Name = "DuelResultReason"; reason.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.AddChild(reason);
        foreach (var seat in new[] { s.MySeat, 1 - s.MySeat })
        {
            var player = s.Players[seat];
            var score = ArenaPresentation.Text($"{(seat == s.MySeat ? "TÚ" : "RIVAL")} · {player.Name}\n{player.Health}/{player.MaxHealth} vidas · {player.Pr} PR", 13, BoardArt.Muted);
            score.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.AddChild(score);
        }
        _resultCloseHint = ArenaPresentation.Text("Volver cierra la sala para ambos.", 11, BoardArt.Muted);
        _resultCloseHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; column.AddChild(_resultCloseHint);
        _resultStatus = ArenaPresentation.Text("", 12, BoardArt.Gold);
        _resultStatus.Name = "DuelResultStatus"; _resultStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resultStatus.MaxLinesVisible = 2; column.AddChild(_resultStatus);
        _resultReturn = ArenaPresentation.Button(column, "Volver al lobby", RequestResultExit);
        _resultReturn.Name = "ReturnToLobby"; RelickVisuals.Primary(_resultReturn);
        var secondary = new HBoxContainer(); secondary.AddThemeConstantOverride("separation", 6); column.AddChild(secondary);
        var history = ArenaPresentation.Button(secondary, "Historial", ShowDuelHistory);
        history.SizeFlagsHorizontal = SizeFlags.ExpandFill; history.AddThemeFontSizeOverride("font_size", 12);
        var review = ArenaPresentation.Button(secondary, "Revisar tablero", () => { _resultDismissed = true; UpdateResultPresentation(); });
        review.Name = "ReviewResultBoard"; review.SizeFlagsHorizontal = SizeFlags.ExpandFill; review.AddThemeFontSizeOverride("font_size", 12);
    }

    private void UpdateResultPresentation()
    {
        if (_resultPanel == null) { HideResultOverlay(); return; }
        var visible = !_resultDismissed && ResultSettled;
        UpdateResultOverlay(visible);
        _resultPanel.Visible = visible; _actionDock.Visible = !visible;
        var closing = Connection.PendingRoomRequest == RoomRequest.Leave;
        _resultReturn!.Disabled = !Connection.Ready || Connection.PendingRoomRequest != RoomRequest.None || !ResultSettled;
        _resultReturn.Text = closing ? "Cerrando sala…" : "Volver al lobby";
        _resultCloseHint!.Text = Connection.Room == null ? "Vuelve al lobby cuando termines de leer." : "Volver cierra la sala para ambos.";
        _resultStatus!.Text = !Connection.Ready ? "Reconecta para comprobar la sala y volver al lobby."
            : closing ? "Esperando la confirmación del servidor…"
            : Connection.RoomError.Length > 0 ? Connection.RoomError
            : Connection.Room == null ? "La sala ya está cerrada. Tu resultado sigue disponible." : "";
        _resultStatus.Visible = _resultStatus.Text.Length > 0;
    }

    private void RequestResultExit()
    {
        if (!HasFinishedResult || !ResultSettled || !Connection.Ready || Connection.PendingRoomRequest != RoomRequest.None) return;
        ReturnToLobbyRequested?.Invoke();
    }
}
