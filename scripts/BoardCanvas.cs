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
    public const float DesignWidth = 1440, DesignHeight = 992;
    /// <summary>The one card footprint of the board; landscape zones swap the axes.</summary>
    public const float CardWidth = 138, CardHeight = 193;
    /// <summary>Divider between both fields, in design units from the top.</summary>
    public const float Seam = 450;
    /// <summary>How far each field reaches from the divider.</summary>
    internal const float HalfDepth = 408;
    internal const float FarEdge = Seam - HalfDepth, NearEdge = Seam + HalfDepth;
    private const float Depth = .29f, NearY = 852;
    internal const float RearRailY = 194;
    internal const float PerspectiveSlope = Depth / (NearEdge - FarEdge);
    internal const float PerspectiveRise = (NearY - RearRailY) * (1 + Depth) / (NearEdge - FarEdge);
    internal static Vector4 ProjectionParameters => new(DesignWidth / 2, NearEdge, NearY, PerspectiveSlope);
    private enum Layer { Screen, Hud, Figure }

    private readonly Control _stage = new() { Name = "BoardStage", MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<(Control Node, Rect2 Rect, BoardAnchor Anchor, Layer Layer)> _items = [];
    private ProjectedTable? _table;
    internal bool TableView { get; init; }
    internal float Unit { get; private set; } = 1;
    internal static Vector2 RivalCardSize => new Vector2(CardWidth, CardHeight) * (PerspectiveScale(FarEdge) * .90f);
    internal static Rect2 RivalHandSlot => new(340, 12, DesignWidth - 680, RearRailY - 24);

    public BoardCanvas() => AddChild(_stage);

    public override void _Ready()
    {
        if (TableView)
        {
            _table = new ProjectedTable { Name = "ProjectedTable" };
            _stage.AddChild(_table);
        }
        Resized += Arrange;
        Arrange();
    }

    /// <summary>Places a control at a design rectangle. Later placements draw above earlier ones.</summary>
    public T Place<T>(T node, Rect2 rect, BoardAnchor anchor = BoardAnchor.Middle) where T : Control
        => Add(node, rect, anchor, Layer.Screen);

    internal T PlaceTableHud<T>(T node, Rect2 rect) where T : Control => Add(node, rect, BoardAnchor.Middle, Layer.Hud);

    internal void PlaceFigure(RelikarioPortrait portrait, bool enemy)
    {
        const float center = 388;
        const float width = 180, height = 168;
        var contact = new Vector2(enemy ? DesignWidth - center : center, Seam + (enemy ? -27 : 158));
        Add(portrait, new Rect2(contact, new Vector2(width, height)), BoardAnchor.Middle, Layer.Figure);
    }

    private T Add<T>(T node, Rect2 rect, BoardAnchor anchor, Layer layer) where T : Control
    {
        _stage.AddChild(node);
        _items.Add((node, rect, anchor, layer));
        if (IsInsideTree()) Arrange();
        return node;
    }

    /// <summary>
    /// A field rectangle given from the seam outward. The rival's field is the same one turned
    /// around, so its canje and Bonus end up on the opposite side.
    /// </summary>
    public T PlaceField<T>(T node, bool enemy, float x, float fromSeam, float width, float height) where T : Control
    {
        var rect = FieldRect(enemy, x, fromSeam, width, height);
        if (_table == null) return Place(node, rect);
        // Only the pieces on the tabletop face their owner. The scoreboard is placed separately.
        _table.Place(node, rect, facingRival: enemy);
        return node;
    }

    internal static Rect2 FieldRect(bool enemy, float x, float fromSeam, float width, float height) => enemy
        ? new Rect2(DesignWidth - x - width, Seam - fromSeam - height, width, height)
        : new Rect2(x, Seam + fromSeam, width, height);
    private static float PerspectiveScale(float y) => 1 / (1 - (y - NearEdge) * PerspectiveSlope);

    internal static Vector2 Project(Vector2 point, float elevation = 0)
    {
        var scale = PerspectiveScale(point.Y);
        return new(DesignWidth / 2 + (point.X - DesignWidth / 2) * scale,
            NearY + ((point.Y - NearEdge) * PerspectiveRise - elevation) * scale);
    }

    internal static Vector2 Unproject(Vector2 point)
    {
        var dy = point.Y - NearY;
        var offset = dy / (PerspectiveRise + dy * PerspectiveSlope);
        return new(DesignWidth / 2 + (point.X - DesignWidth / 2) / PerspectiveScale(NearEdge + offset), NearEdge + offset);
    }

    private void Arrange()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        Unit = Math.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        var design = Size / Unit;
        // One axis always fits exactly: rounding must not push its edge a fraction off the screen.
        var left = Math.Max(0, (design.X - DesignWidth) / 2);
        var extra = Math.Max(0, design.Y - DesignHeight);
        _stage.Position = Vector2.Zero; _stage.Scale = Vector2.One * Unit; _stage.Size = design;
        if (_table != null) _table.Position = new Vector2(left, extra / 2);
        foreach (var (node, rect, anchor, layer) in _items)
        {
            if (!IsInstanceValid(node)) continue;
            var shift = anchor switch { BoardAnchor.Middle => extra / 2, BoardAnchor.Bottom => extra, _ => 0 };
            var origin = new Vector2(left, shift);
            var position = rect.Position;
            var scale = Vector2.One;
            // The scoreboard and phase controls follow the table but stay level so their text is readable.
            if (TableView && layer == Layer.Hud)
            {
                var center = rect.GetCenter();
                var height = Project(new Vector2(center.X, rect.End.Y)).Y - Project(new Vector2(center.X, rect.Position.Y)).Y;
                scale = new(PerspectiveScale(center.Y), height / rect.Size.Y);
                position = Project(center) - rect.Size * scale / 2;
            }
            else if (TableView && layer == Layer.Figure)
            {
                // Stand vertically, with the feet on the projected board instead of foreshortening the sprite.
                scale *= PerspectiveScale(position.Y);
                position = Project(position) - rect.Size * scale * new Vector2(.5f, .94f);
            }
            node.Position = origin + position;
            node.Scale = scale;
            node.Size = rect.Size;
        }
        if (TableView) QueueRedraw();
    }

    public override void _Draw()
    {
        if (!TableView || Size.X <= 0 || Size.Y <= 0) return;
        var origin = (Size / Unit - new Vector2(DesignWidth, DesignHeight)) / 2;
        Vector2 At(float x, float y) => (origin + Project(new Vector2(x, y))) * Unit;
        var tl = At(0, FarEdge); var tr = At(DesignWidth, FarEdge);
        var bl = At(0, NearEdge); var br = At(DesignWidth, NearEdge);
        var ml = At(0, Seam); var mr = At(DesignWidth, Seam);
        var lip = new Vector2(0, 15 * Unit);
        var surface = RelickVisuals.Playmat;
        DrawColoredPolygon([bl, br, br + lip, bl + lip], surface.Background.Lightened(.045f));
        DrawPolygon([tl, tr, mr, ml], [surface.Surface, surface.Surface, surface.Surface.Lerp(ArenaPresentation.Coral, .12f), surface.Surface.Lerp(ArenaPresentation.Coral, .12f)]);
        DrawPolygon([ml, mr, br, bl], [surface.Surface.Lerp(ArenaPresentation.Mint, .14f), surface.Surface.Lerp(ArenaPresentation.Mint, .14f), surface.Surface, surface.Surface]);
        DrawPolyline([bl, tl, tr, br, bl], BoardArt.Tint(surface.Frame.Lightened(.22f), .88f), 1.6f, true);
        DrawLine(ml, mr, BoardArt.Tint(surface.Trace, .85f), 2, true);
    }
}
