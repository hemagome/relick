using Godot;

namespace Relick;

// Static manga/arcade decoration; the lobby's surfaces keep their own reading contrast.
public partial class LobbyBackground : Control
{
    private GradientTexture2D _wash = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(false);
        var background = RelickVisuals.Playmat.Background;
        _wash = new GradientTexture2D
        {
            Width = 128, Height = 128, FillFrom = Vector2.Zero, FillTo = Vector2.One,
            Gradient = new Gradient
            {
                Offsets = [0, 0.58f, 1],
                Colors = [background.Lerp(new("35556f"), 0.72f), background.Lerp(new("203145"), 0.8f), background.Lerp(new("412638"), 0.6f)]
            }
        };
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;
        DrawTextureRect(_wash, new Rect2(Vector2.Zero, Size), false);
        var cyan = RelickVisuals.State.Target;
        var red = RelickVisuals.State.Rival;
        var silver = RelickVisuals.State.Focus;

        // Cropped panel gutters and speed lines stay at the perimeter.
        DrawColoredPolygon([new(0, Size.Y * 0.66f), new(Size.X * 0.3f, Size.Y), new(0, Size.Y)], BoardArt.Tint(red, 0.045f));
        DrawPolyline([new(-24, Size.Y * 0.56f), new(Size.X * 0.24f, Size.Y + 24)], BoardArt.Tint(silver, 0.1f), 1, true);
        DrawPolyline([new(-24, Size.Y * 0.6f), new(Size.X * 0.22f, Size.Y + 24)], BoardArt.Tint(red, 0.15f), 2, true);
        for (var i = 0; i < 8; i++)
        {
            var start = new Vector2(-20, Size.Y * (0.72f + i * 0.038f));
            DrawLine(start, start + new Vector2(Size.X * (0.12f + i * 0.009f), Size.Y * 0.17f), BoardArt.Tint(silver, 0.045f), 1, true);
        }

        Halftone(new(Size.X * 0.025f, Size.Y * 0.89f), new(Math.Min(210, Size.X * 0.24f), Size.Y * 0.33f), silver);
        Halftone(new(Size.X * 0.98f, Size.Y * 0.11f), new(Math.Min(240, Size.X * 0.27f), Size.Y * 0.33f), cyan);

        // The logo's paired chevrons become a quiet, oversized edge motif.
        var height = Size.Y * 0.68f;
        var width = Math.Clamp(Size.X * 0.17f, 140, 270);
        Chevron(new(Size.X - width * 1.02f, Size.Y * 0.17f, width, height), cyan);
        Chevron(new(Size.X - width * 0.55f, Size.Y * 0.17f, width, height), red);
        DrawPolyline([new(Size.X * 0.78f, -20), new(Size.X * 0.9f, Size.Y * 0.17f), new(Size.X + 20, Size.Y * 0.17f)], BoardArt.Tint(silver, 0.1f), 1, true);
    }

    private void Chevron(Rect2 rect, Color color)
    {
        var p = rect.Position; var w = rect.Size.X; var h = rect.Size.Y;
        Vector2[] points = [p, p + new Vector2(w * 0.38f, 0), p + new Vector2(w, h * 0.5f),
            p + new Vector2(w * 0.38f, h), p + new Vector2(0, h), p + new Vector2(w * 0.62f, h * 0.5f)];
        DrawColoredPolygon(points, BoardArt.Tint(color, 0.045f));
        DrawPolyline(points.Append(points[0]).ToArray(), BoardArt.Tint(color, 0.14f), 1, true);
    }

    private void Halftone(Vector2 center, Vector2 radius, Color color)
    {
        const int spacing = 14;
        for (var y = Math.Max(0, (int)(center.Y - radius.Y)); y < Math.Min(Size.Y, center.Y + radius.Y); y += spacing)
        for (var x = Math.Max(0, (int)(center.X - radius.X)); x < Math.Min(Size.X, center.X + radius.X); x += spacing)
        {
            var point = new Vector2(x, y);
            var distance = ((point - center) / radius).LengthSquared();
            if (distance < 1) DrawCircle(point, 1.3f, BoardArt.Tint(color, (1 - distance) * 0.09f));
        }
    }
}
