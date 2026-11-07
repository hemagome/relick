using Godot;

namespace Relick;

public partial class Main
{
    private async void PvpInputSmokeTest()
    {
        try
        {
            void Check(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message);
            }
            async Task Frame()
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (_onlineLobby == null) OpenOnlineLobby();
            await Frame(); await Frame();
            Check(CardsInMotion, "La prueba debe abrir PvP durante el reparto inicial de práctica.");
            var elapsed = _drawTime;
            var pending = _drawQueue.Count;
            var fields = _onlineLobby!.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            GuiInputTestDriver.ReplaceText(fields[0], "Prueba uno");
            GuiInputTestDriver.ReplaceText(fields[1], "AB12CD34");
            // A late dice callback may rebuild practice while the lobby is open.
            Render();
            await Frame(); await Frame();
            Check(!_root.Visible && !_diceOverlay.Visible, "La práctica reaparece sobre PvP al actualizarse.");
            await Frame();
            Check(_drawOverlay?.IsVisibleInTree() != true, "El reparto de práctica se dibuja sobre PvP.");
            Check(_drawTime == elapsed && _drawQueue.Count == pending, "La práctica avanza mientras PvP está abierto.");
            var back = _onlineLobby.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "Volver a práctica");
            GuiInputTestDriver.Click(back);
            Check(_onlineLobby == null, "El clic para volver a práctica no se recibe.");
            Check(_drawOverlay!.Visible, "La animación de práctica no se restaura al volver.");
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (CardsInMotion && DateTime.UtcNow < deadline) await Frame();
            Check(!CardsInMotion && !_drawOverlay.Visible, "El reparto no termina al volver a práctica.");
            var pvp = _root.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "PvP");
            GuiInputTestDriver.Click(pvp);
            Check(_onlineLobby != null, "No se puede reabrir PvP con el ratón.");
            await Frame(); await Frame();
            fields = _onlineLobby!.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            GuiInputTestDriver.ReplaceText(fields[0], "Prueba dos");
            GuiInputTestDriver.ReplaceText(fields[1], "ZX98YU76");
            back = _onlineLobby.FindChildren("*", "Button", true, false).OfType<Button>().Single(b => b.Text == "Volver a práctica");
            GuiInputTestDriver.Click(back);
            Check(_onlineLobby == null, "No se puede volver a práctica después de reabrir PvP.");
            GD.Print("PVP_INPUT_PASS · clics, foco, escritura, regreso a práctica y reapertura mediante eventos de entrada.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError("PVP_INPUT_FAIL · " + error);
            GetTree().Quit(1);
        }
    }
}

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
