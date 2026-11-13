using Godot;

namespace Relick;

public enum BoardAnchor { Top, Middle, Bottom }

/// <summary>
/// Desktop playmat. Every zone is placed in one design space where all cards share a footprint;
/// the stage scales uniformly to the window, the rival hand stays at the top edge, the own hand
/// at the bottom one and both fields keep the middle.
/// </summary>
public partial class BoardCanvas : Control
{
    public const float DesignWidth = 1440, DesignHeight = 1024;
    /// <summary>The one card footprint of the board; landscape zones swap the axes.</summary>
    public const float CardWidth = 138, CardHeight = 193;
    /// <summary>Divider between both fields, in design units from the top.</summary>
    public const float Seam = 466;

    private readonly Control _stage = new() { Name = "BoardStage", MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<(Control Node, Rect2 Rect, BoardAnchor Anchor)> _items = [];
    internal float Unit { get; private set; } = 1;

    public BoardCanvas() => AddChild(_stage);

    public override void _Ready()
    {
        Resized += Arrange;
        Arrange();
    }

    /// <summary>Places a control at a design rectangle. Later placements draw above earlier ones.</summary>
    public T Place<T>(T node, Rect2 rect, BoardAnchor anchor = BoardAnchor.Middle) where T : Control
    {
        _stage.AddChild(node);
        _items.Add((node, rect, anchor));
        if (IsInsideTree()) Arrange();
        return node;
    }

    /// <summary>
    /// A field rectangle given from the seam outward. The rival's field is the same one turned
    /// around, so its canje and Bonus end up on the opposite side.
    /// </summary>
    public T PlaceField<T>(T node, bool enemy, float x, float fromSeam, float width, float height) where T : Control
        => Place(node, enemy
            ? new Rect2(DesignWidth - x - width, Seam - fromSeam - height, width, height)
            : new Rect2(x, Seam + fromSeam, width, height));

    private void Arrange()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        Unit = Math.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        var design = Size / Unit;
        var left = (design.X - DesignWidth) / 2;
        var extra = design.Y - DesignHeight;
        _stage.Position = Vector2.Zero; _stage.Scale = Vector2.One * Unit; _stage.Size = design;
        foreach (var (node, rect, anchor) in _items)
        {
            if (!IsInstanceValid(node)) continue;
            var shift = anchor switch { BoardAnchor.Middle => extra / 2, BoardAnchor.Bottom => extra, _ => 0 };
            node.Position = new Vector2(left + rect.Position.X, rect.Position.Y + shift);
            node.Size = rect.Size;
        }
    }
}
