using Godot;

namespace Relick;

public partial class OnlineLobby
{
    private Control _connectionNotice = null!;
    private PanelContainer _connectionNoticePanel = null!;
    private Label _connectionNoticeTitle = null!;
    private Label _connectionNoticeDescription = null!;
    private Button _reconnect = null!;
    private Control? _focusBeforeConnectionNotice;

    private void BuildConnectionNotice()
    {
        // Keep the notice above both the lobby and the arena opened later.
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        var overlay = new ConnectionNoticeOverlay { Name = "ConnectionNotice", Visible = false, FocusMode = FocusModeEnum.All };
        _connectionNotice = overlay;
        layer.AddChild(_connectionNotice);
        _connectionNotice.FocusNeighborTop = _connectionNotice.FocusNeighborBottom = _connectionNotice.GetPath();
        _connectionNotice.FocusNext = _connectionNotice.FocusPrevious = _connectionNotice.GetPath();
        _connectionNotice.FocusNeighborLeft = _connectionNotice.FocusNeighborRight = _connectionNotice.GetPath();
        _connectionNotice.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0.02f, 0.03f, 0.05f, 0.88f) };
        _connectionNotice.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        _connectionNotice.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _connectionNoticePanel = Surface(center, 28);
        _connectionNoticePanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _connectionNoticePanel.AddThemeStyleboxOverride("panel", RelickVisuals.Frame(
            RelickVisuals.Playmat.Surface, ArenaPresentation.Coral, 12));
        var style = (StyleBoxFlat)_connectionNoticePanel.GetThemeStylebox("panel");
        style.ContentMarginLeft = style.ContentMarginRight = 28;
        style.ContentMarginTop = style.ContentMarginBottom = 28;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 20);
        _connectionNoticePanel.AddChild(column);
        _connectionNoticeTitle = Text("Sin conexión", 36);
        _connectionNoticeTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _connectionNoticeTitle.AddThemeColorOverride("font_color", ArenaPresentation.Coral);
        column.AddChild(_connectionNoticeTitle);
        _connectionNoticeDescription = Text("", 20);
        _connectionNoticeDescription.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_connectionNoticeDescription);
        _reconnect = AddButton(column, "Reconectar", Connect);
        _reconnect.Name = "Reconnect";
        overlay.Retry = _reconnect;
        AccentButton(_reconnect, LobbyMint);
        _reconnect.FocusNeighborTop = _reconnect.GetPath();
        _reconnect.FocusNeighborBottom = _reconnect.GetPath();
        _reconnect.FocusNeighborLeft = _reconnect.FocusNeighborRight = _reconnect.GetPath();
        _reconnect.FocusNext = _reconnect.GetPath();
        _reconnect.FocusPrevious = _reconnect.GetPath();
        Resized += AdjustConnectionNotice;
        AdjustConnectionNotice();
    }

    private void AdjustConnectionNotice()
    {
        _connectionNoticePanel.CustomMinimumSize = new(Math.Min(600, Math.Max(280, Size.X - 48)), 0);
        _connectionNoticeTitle.AddThemeFontSizeOverride("font_size", ArenaViewport.Short(this) ? 30 : 36);
    }

    private void PresentConnectionNotice(bool ready, bool connecting)
    {
        // Initial loading uses the small status. Once an outage appears, keep the
        // notice up throughout reconnection until the room subscription is ready.
        var show = !ready && (!connecting || _arena != null || _connectionNotice.Visible);
        if (show)
        {
            _connectionNoticeTitle.Text = connecting ? "Reconectando…" : "Sin conexión";
            _connectionNoticeDescription.Text = connecting
                ? "Estamos intentando recuperar la conexión con el servidor."
                : "Se perdió la conexión con el servidor.\nComprueba tu internet y pulsa Reconectar para continuar.";
            _reconnect.Disabled = connecting;
            _reconnect.Text = connecting ? "Reconectando…" : "Reconectar";
            if (!_connectionNotice.Visible)
            {
                _focusBeforeConnectionNotice = GetViewport().GuiGetFocusOwner();
                _focusBeforeConnectionNotice?.ReleaseFocus();
                // Embedded/native windows render above CanvasLayers and would
                // otherwise hide the notice or keep consuming input.
                if (_arena != null)
                    foreach (var window in _arena.FindChildren("*", "Window", true, false).OfType<Window>())
                    {
                        window.Hide();
                        if (window is not PopupMenu) window.QueueFree();
                    }
                _connectionNotice.Show();
            }
            // Keep keyboard navigation inside the notice even while the retry
            // button is disabled, without pausing network recovery processing.
            var focus = connecting ? _connectionNotice : _reconnect;
            if (!focus.HasFocus()) focus.GrabFocus();
        }
        else if (_connectionNotice.Visible)
        {
            _connectionNotice.Hide();
            if (GodotObject.IsInstanceValid(_focusBeforeConnectionNotice) && _focusBeforeConnectionNotice!.IsVisibleInTree())
                _focusBeforeConnectionNotice.GrabFocus();
            _focusBeforeConnectionNotice = null;
        }
    }
}
