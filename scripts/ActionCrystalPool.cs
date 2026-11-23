using Godot;

namespace Relick;

internal enum ActionCrystalChange { None, Spend, Refund, Recharge }

public partial class ActionCrystalPool : Control
{
    private const double AnimationSeconds = 0.72;
    private bool _initialized;
    private int _previous;
    private double _elapsed;
    private int _capacity = 2;
    public Color Accent { get; init; }
    internal int Amount { get; private set; }
    internal ActionCrystalChange Change { get; private set; }
    internal bool ReducedMotion { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new(44, 32);
        MouseFilter = MouseFilterEnum.Pass;
        ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();
        Resized += QueueRedraw;
        SetProcess(false);
    }

    internal void Present(int amount, string owner, bool animate = false, bool recharge = false)
    {
        amount = Math.Max(0, amount);
        TooltipText = $"{owner} · {amount} {(amount == 1 ? "acción" : "acciones")} disponibles";
        AccessibilityName = TooltipText;
        if (_initialized && Amount == amount && !recharge) return;
        var hadState = _initialized;
        _initialized = true;
        _previous = Amount;
        Amount = amount;
        _capacity = Math.Max(2, Math.Max(Amount, _previous));
        CustomMinimumSize = new(_capacity * 22, 32);
        if (!animate || !hadState || ReducedMotion)
        {
            Settle();
            return;
        }
        Change = recharge ? ActionCrystalChange.Recharge
            : Amount < _previous ? ActionCrystalChange.Spend : ActionCrystalChange.Refund;
        _elapsed = 0;
        SetProcess(true);
        QueueRedraw();
    }

    internal void Settle()
    {
        Change = ActionCrystalChange.None;
        _capacity = Math.Max(2, Amount);
        CustomMinimumSize = new(_capacity * 22, 32);
        SetProcess(false);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += Math.Max(0, delta);
        if (_elapsed >= AnimationSeconds) Settle();
        else QueueRedraw();
    }

    public override void _Draw()
    {
        for (var index = 0; index < _capacity; index++)
        {
            var center = new Vector2((index + 0.5f) * Size.X / _capacity, Size.Y / 2);
            var filled = index < Amount;
            var changed = Change == ActionCrystalChange.Recharge ? filled
                : Change == ActionCrystalChange.Spend ? index >= Amount && index < _previous
                : Change == ActionCrystalChange.Refund && index >= _previous && filled;
            var progress = changed ? Math.Clamp(((float)_elapsed - index * 0.08f) / 0.52f, 0, 1) : 1;
            var energy = filled ? 1f : 0f;
            if (changed) energy = Change == ActionCrystalChange.Spend ? 1 - progress : progress;
            DrawCrystal(center, energy);
            if (!changed || progress is <= 0 or >= 1) continue;
            var glow = Mathf.Sin(progress * Mathf.Pi);
            DrawArc(center, 13 + progress * 3, -Mathf.Pi * 0.8f, Mathf.Pi * 0.8f, 20,
                BoardArt.Tint(Accent, glow * 0.65f), 1, true);
            if (Change == ActionCrystalChange.Spend)
            {
                for (var shard = 0; shard < 4; shard++)
                {
                    var direction = Vector2.FromAngle(-Mathf.Pi * 0.75f + shard * Mathf.Pi / 2);
                    var point = center + direction * (5 + progress * 13);
                    DrawColoredPolygon([point + new Vector2(0, -2), point + new Vector2(2, 0), point + new Vector2(0, 2), point - new Vector2(2, 0)],
                        BoardArt.Tint(Accent, 1 - progress));
                }
            }
            else
            {
                var shine = center + new Vector2(0, 10 - progress * 20);
                DrawLine(shine - new Vector2(6, 0), shine + new Vector2(6, 0), BoardArt.Tint(BoardArt.Ink, glow), 2, true);
            }
        }
    }

    private static readonly Dictionary<(int, Color), ImageTexture> Icons = [];

    /// <summary>
    /// A row of lit action crystals as a texture, for places that take an icon rather than a control,
    /// such as a menu entry showing what an effect costs.
    /// </summary>
    internal static ImageTexture Icon(int count, Color accent)
    {
        if (Icons.TryGetValue((count, accent), out var cached)) return cached;
        const int width = 13, height = 18, samples = 4;
        var image = Image.CreateEmpty(Math.Max(1, count * width), height, false, Image.Format.Rgba8);
        var center = new Vector2(6.5f, 9);
        Vector2[] shape = [new(0, -8.5f), new(5.5f, -3.3f), new(4.2f, 4.6f), new(0, 8.5f), new(-4.2f, 4.6f), new(-5.5f, -3.3f)];
        static bool Inside(Vector2[] polygon, Vector2 point)
        {
            for (var i = 0; i < polygon.Length; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length];
                if ((b - a).Cross(point - a) < 0) return false;
            }
            return true;
        }
        Color Shade(Vector2 local)
        {
            // Lit facet above, shaded facet on the right, as the board's crystals.
            if (local.Y < -3.3f + Math.Abs(local.X) * 0.1f && local.Y < 0) return accent.Lightened(0.4f);
            if (local.X > 0 && local.Y > -1) return accent.Darkened(0.3f);
            return accent;
        }
        for (var crystal = 0; crystal < count; crystal++)
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            Color sum = new(0, 0, 0, 0);
            for (var sy = 0; sy < samples; sy++)
            for (var sx = 0; sx < samples; sx++)
            {
                var local = new Vector2(x + (sx + .5f) / samples, y + (sy + .5f) / samples) - center;
                if (!Inside(shape, local)) continue;
                var color = Shade(local);
                sum += new Color(color.R, color.G, color.B, 1);
            }
            var coverage = sum.A / (samples * samples);
            if (coverage <= 0) continue;
            image.SetPixel(crystal * width + x, y, new Color(sum.R / sum.A, sum.G / sum.A, sum.B / sum.A, coverage));
        }
        return Icons[(count, accent)] = ImageTexture.CreateFromImage(image);
    }

    private void DrawCrystal(Vector2 center, float energy)
    {
        Vector2[] points = [center + new Vector2(0, -13), center + new Vector2(8, -5), center + new Vector2(6, 7),
            center + new Vector2(0, 13), center + new Vector2(-6, 7), center + new Vector2(-8, -5)];
        DrawColoredPolygon(points, RelickVisuals.Playmat.Background);
        DrawColoredPolygon(points, BoardArt.Tint(Accent, 0.06f + energy * 0.8f));
        if (energy > 0)
        {
            DrawColoredPolygon([points[0], points[1], center, points[5]], BoardArt.Tint(Accent.Lightened(0.4f), energy));
            DrawColoredPolygon([center, points[1], points[2], points[3]], BoardArt.Tint(Accent.Darkened(0.3f), energy));
            DrawLine(points[0], center, BoardArt.Tint(BoardArt.Ink, energy * 0.6f), 1, true);
        }
        DrawPolyline([.. points, points[0]], BoardArt.Tint(Accent, 0.22f + energy * 0.65f), 1, true);
    }
}
