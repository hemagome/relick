using Godot;
using Relick.Network;

namespace Relick;

public partial class OnlineArena
{
    private PanelContainer? _resultPanel;
    private CenterContainer? _resultControls;
    private Label? _resultStatus, _resultCloseHint;
    private Button? _resultReturn;
    private bool _resultDismissed;
    private bool ResultSettled => !IsAnimating && _pendingPortraits.Count == 0 && _portraits.All(p => !p.IsActionPlaying);

    private void BuildResultPanel()
    {
        if (_resultControls != null) { RemoveChild(_resultControls); _resultControls.QueueFree(); _resultControls = null; }
        _resultPanel = null; _resultReturn = null; _resultStatus = null;
        if (_snapshot?.Winner is not int winner) return;
        var s = _snapshot;
        _resultControls = new CenterContainer { Name = "DuelResultControls", MouseFilter = MouseFilterEnum.Ignore, ZIndex = 21 };
        AddChild(_resultControls); _resultControls.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { Name = "DuelResultPanel", Visible = false, CustomMinimumSize = new(Math.Min(Size.X - 40, Short ? 430 : 520), 0) };
        var accent = winner == -1 ? BoardArt.Gold : winner == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        panel.AddThemeStyleboxOverride("panel", RelickVisuals.Glass(RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Surface,
            accent, 11, 10, BoardArt.Tint(accent, 0.25f)));
        _resultControls.AddChild(panel); _resultPanel = panel;
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 6); panel.AddChild(column);
        var title = ArenaPresentation.Text(winner == -1 ? "EMPATE" : winner == s.MySeat ? "VICTORIA" : "DERROTA", Short ? 22 : 28,
            winner == -1 ? BoardArt.Gold : winner == s.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral);
        title.Name = "DuelResultTitle"; title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontOverride("font", RelickVisuals.Display); column.AddChild(title);
        var reading = new ScrollContainer { Name = "DuelResultReading", CustomMinimumSize = new(0, Short ? 104 : 132),
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
        if (_resultPanel == null) return;
        var visible = !_resultDismissed && ResultSettled;
        if (visible && !_resultPanel.Visible)
        {
            _resultPanel.Modulate = _impacts.ReducedMotion ? Colors.White : new Color(1, 1, 1, 0);
            if (!_impacts.ReducedMotion)
                _resultPanel.CreateTween().TweenProperty(_resultPanel, "modulate:a", 1f, 0.25);
        }
        _resultPanel.Visible = visible;
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
