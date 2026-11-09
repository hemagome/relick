using Godot;

namespace Relick;

// Feed events through _Input and GUI hit testing, rather than invoking button signals.
internal static class GuiInputTestDriver
{
    public static void Click(Control control)
    {
        if (!control.IsVisibleInTree()) throw new InvalidOperationException("Control oculto: " + control.Name);
        var viewport = control.GetViewport();
        var point = control.GetGlobalTransformWithCanvas() * (control.Size / 2);
        if (viewport is Window window && window.IsEmbedded())
        {
            point += (Vector2)window.Position;
            viewport = control.GetTree().Root;
        }
        var pressed = false;
        void OnPressed() => pressed = true;
        if (control is Button button) button.Pressed += OnPressed;
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left, Pressed = true
        }, true);
        viewport.PushInput(new InputEventMouseButton
        {
            Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false
        }, true);
        if (control is Button clicked)
        {
            clicked.Pressed -= OnPressed;
            if (!pressed) throw new InvalidOperationException($"El botón no recibe el clic: {clicked.Text}; punto={point}, tamaño={control.Size}, viewport={viewport.Name}, hover={viewport.GuiGetHoveredControl()?.Name}.");
        }
    }

    public static void ScrollDown(Control control, int steps = 6)
    {
        var point = control.GetGlobalRect().GetCenter();
        var viewport = control.GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
        for (var i = 0; i < steps; i++)
        {
            viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 1 }, true);
            viewport.PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point,
                ButtonIndex = MouseButton.WheelDown, Pressed = false, Factor = 1 }, true);
        }
    }

    public static void ReplaceText(LineEdit field, string text)
    {
        Click(field);
        if (!field.HasFocus()) throw new InvalidOperationException("El clic no da foco al campo: " + field.PlaceholderText);
        var viewport = field.GetViewport();
        viewport.PushInput(new InputEventKey { Keycode = Key.A, CtrlPressed = true, Pressed = true }, true);
        viewport.PushInput(new InputEventKey { Keycode = Key.A, CtrlPressed = true }, true);
        foreach (var character in text)
        {
            viewport.PushInput(new InputEventKey { Unicode = character, Pressed = true }, true);
            viewport.PushInput(new InputEventKey { Unicode = character }, true);
        }
        if (field.Text != text) throw new InvalidOperationException("El campo no recibe la escritura: " + field.PlaceholderText);
    }
}
