using Godot;

namespace Relick;

/// <summary>Renders the playable zones on one plane and sends GUI input back through its inverse projection.</summary>
internal sealed partial class ProjectedTable : TextureRect
{
    private static readonly Vector2 DesignSize = new(BoardCanvas.DesignWidth, BoardCanvas.DesignHeight);
    private readonly SubViewport _viewport = new()
    {
        Name = "FlatBoardViewport", Size = (Vector2I)DesignSize, TransparentBg = true, Disable3D = true,
        RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
    };
    private readonly Control _zones = new() { Name = "FlatBoardZones", MouseFilter = MouseFilterEnum.Ignore, Size = DesignSize };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        FocusMode = FocusModeEnum.All;
        AccessibilityName = "Zonas de la mesa";
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
        TextureFilter = TextureFilterEnum.Linear;
        Size = DesignSize;
        AddChild(_viewport);
        _zones.Theme = RelickVisuals.Controls();
        _viewport.AddChild(_zones);
        Texture = _viewport.GetTexture();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/projected_table.gdshader") };
        material.SetShaderParameter("design_size", DesignSize);
        material.SetShaderParameter("projection", BoardCanvas.ProjectionParameters);
        material.SetShaderParameter("rise", BoardCanvas.PerspectiveRise);
        Material = material;
        MouseEntered += _viewport.NotifyMouseEntered;
        MouseExited += _viewport.NotifyMouseExited;
        FocusEntered += () => FocusOnEdge(Input.IsKeyPressed(Key.Shift));
        FocusExited += _viewport.GuiReleaseFocus;
    }

    internal void Place(Control node, Rect2 area, bool facingRival)
    {
        _zones.AddChild(node);
        node.Position = area.Position;
        node.Size = area.Size;
        if (facingRival)
        {
            node.PivotOffset = node.Size / 2;
            node.Rotation = Mathf.Pi;
        }
    }

    internal static ProjectedTable? Of(Control node) => node.GetViewport()?.GetParent() as ProjectedTable;

    internal Vector2 ScreenPoint(Control zone, Vector2 point, bool withCanvas = false)
    {
        var projected = BoardCanvas.Project(zone.GetGlobalTransform() * point);
        return (withCanvas ? GetGlobalTransformWithCanvas() : GetGlobalTransform()) * projected;
    }

    internal Rect2 ScreenBounds(Control zone, Rect2 area)
    {
        var tl = ScreenPoint(zone, area.Position);
        var tr = ScreenPoint(zone, new Vector2(area.End.X, area.Position.Y));
        var bl = ScreenPoint(zone, new Vector2(area.Position.X, area.End.Y));
        return new Rect2(tl, Vector2.Zero).Expand(tr).Expand(bl).Expand(ScreenPoint(zone, area.End));
    }

    // A card flies in screen space. Its resting width is measured across its middle, without adding
    // the horizontal shear of the trapezoid to its airborne size.
    internal Rect2 FlightFootprint(Control zone, Rect2 area)
    {
        var center = area.GetCenter();
        var width = ScreenPoint(zone, new(area.End.X, center.Y)).X - ScreenPoint(zone, new(area.Position.X, center.Y)).X;
        var height = ScreenPoint(zone, new(center.X, area.End.Y)).Y - ScreenPoint(zone, new(center.X, area.Position.Y)).Y;
        var size = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
        return new Rect2(ScreenPoint(zone, center) - size / 2, size);
    }

    public override bool _HasPoint(Vector2 point)
    {
        if (!new Rect2(Vector2.Zero, Size).HasPoint(point)) return false;
        var onTable = BoardCanvas.Unproject(point);
        return onTable.X >= 0 && onTable.X < DesignSize.X && onTable.Y >= BoardCanvas.FarEdge - 2 && onTable.Y < BoardCanvas.NearEdge + 2;
    }

    public override void _GuiInput(InputEvent input)
    {
        InputEvent? forwarded = input switch
        {
            InputEventMouse mouse => InputAt(mouse, mouse.Position),
            InputEventScreenTouch touch => InputAt(touch, touch.Position),
            InputEventScreenDrag drag => InputAt(drag, drag.Position),
            InputEventKey or InputEventJoypadButton or InputEventJoypadMotion when HasFocus() => input,
            _ => null
        };
        if (forwarded == null) return;
        if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || input is InputEventScreenTouch { Pressed: true })
            GrabFocus();
        if (input.IsActionPressed("ui_focus_next") && LeaveAtEdge(reverse: false) ||
            input.IsActionPressed("ui_focus_prev") && LeaveAtEdge(reverse: true))
        { AcceptEvent(); return; }
        _viewport.PushInput(forwarded, true);
        if (input is InputEventMouse)
        {
            var hovered = _viewport.GuiGetHoveredControl();
            MouseDefaultCursorShape = hovered?.MouseDefaultCursorShape ?? CursorShape.Arrow;
        }
        if (_viewport.IsInputHandled()) AcceptEvent();
    }

    private static InputEvent InputAt(InputEvent input, Vector2 local)
    {
        var planar = BoardCanvas.Unproject(local);
        var x = BoardCanvas.Unproject(local + Vector2.Right) - planar;
        var y = BoardCanvas.Unproject(local + Vector2.Down) - planar;
        // Make input local to the flat viewport, including the direction of pointer movement.
        var basis = new Transform2D(x, y, Vector2.Zero);
        return input.XformedBy(new Transform2D(x, y, planar - basis * local));
    }

    private Control[] Focusable() => _zones.FindChildren("*", "Control", true, false).OfType<Control>()
        .Where(control => control.IsVisibleInTree() && control.FocusMode == FocusModeEnum.All && control is not BaseButton { Disabled: true }).ToArray();

    private void FocusOnEdge(bool reverse)
    {
        var controls = Focusable();
        (reverse ? controls.LastOrDefault() : controls.FirstOrDefault())?.GrabFocus();
    }

    private bool LeaveAtEdge(bool reverse)
    {
        var controls = Focusable();
        var edge = reverse ? controls.FirstOrDefault() : controls.LastOrDefault();
        if (edge != _viewport.GuiGetFocusOwner()) return false;
        var next = reverse ? FindPrevValidFocus() : FindNextValidFocus();
        if (next == null || next == this) return false;
        next.GrabFocus();
        return true;
    }
}
