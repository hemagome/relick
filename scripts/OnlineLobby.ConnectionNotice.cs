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
        _connectionNotice = new Control { Name = "ConnectionNotice", Visible = false };
        layer.AddChild(_connectionNotice);
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
        AccentButton(_reconnect, LobbyMint);
        _reconnect.FocusNeighborTop = _reconnect.GetPath();
        _reconnect.FocusNeighborBottom = _reconnect.GetPath();
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
        var show = !ready && (!connecting || _connectionNotice.Visible);
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
                _connectionNotice.Show();
                _reconnect.GrabFocus();
            }
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
