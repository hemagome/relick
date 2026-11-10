using Godot;

namespace Relick;

// One available action: keep navigation here instead of moving focus into the
// underlying screen, including when the retry button is temporarily disabled.
public partial class ConnectionNoticeOverlay : Control
{
    public Button Retry { get; set; } = null!;

    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || input is not InputEventKey key) return;
        var navigation = key.Keycode is Key.Tab or Key.Up or Key.Down or Key.Left or Key.Right or Key.Escape;
        if (!Retry.Disabled && !navigation) return;
        if (Retry.Disabled) GrabFocus(); else Retry.GrabFocus();
        GetViewport().SetInputAsHandled();
    }
}
