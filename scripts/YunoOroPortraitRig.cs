using Godot;

namespace Relick;

/// <summary>Continuous deformation of Yuno Oro's cutout. All anchors are in texture UVs.</summary>
internal sealed class YunoOroPortraitRig(Texture2D sprite)
{
    internal const double FireAt = .43;
    private const int GridSize = 13;
    private static readonly Vector2 Glove = new(.44f, .45f);
    private static readonly Vector2 Muzzle = new(.61f, .43f);
    private static readonly Vector2 Chest = new(.58f, .35f);
    private static readonly Vector2[] Uvs = BuildUvs();
    private static readonly int[] Triangles = BuildTriangles();
    private static readonly Color Gold = new("ffba3b");
    private static readonly Color Light = new("fff0a0");

    internal readonly record struct Pose(Vector2 Offset, float Lean, float Height, float Punch, float Hair, float Glow, float Fatigue = 0)
    {
        internal Pose Blend(Pose goal, float t) => new(Offset.Lerp(goal.Offset, t), Mathf.Lerp(Lean, goal.Lean, t),
            Mathf.Lerp(Height, goal.Height, t), Mathf.Lerp(Punch, goal.Punch, t), Mathf.Lerp(Hair, goal.Hair, t),
            Mathf.Lerp(Glow, goal.Glow, t), Mathf.Lerp(Fatigue, goal.Fatigue, t));
    }

    private static readonly Pose Guard = new(Vector2.Zero, 0, 1, 0, 0, 0);
    private static readonly (float Time, Pose Pose)[] Attack =
    [
        (0, Guard),
        (.19f, new(new(-.075f, .035f), -10, .96f, -.115f, .02f, .10f)),
        (.36f, new(new(.13f, -.034f), 10, 1.02f, .21f, -.055f, 1)),
        (.53f, new(new(.085f, -.012f), 7, .99f, .105f, .012f, .34f)),
        (.76f, new(new(-.014f, .012f), -2, .99f, -.012f, .014f, 0)),
        (1, Guard)
    ];
    private static readonly (float Time, Pose Pose)[] Heal =
    [
        (0, Guard),
        (.21f, new(new(-.015f, .023f), -5, .95f, -.035f, .015f, .2f)),
        (.51f, new(new(.006f, -.025f), 2, 1.038f, -.026f, -.015f, 1)),
        (.78f, new(new(0, -.008f), 0, 1.02f, -.012f, .006f, .64f)),
        (1, Guard)
    ];

    private readonly Vector2[] _points = new Vector2[Uvs.Length];
    private readonly Color[] _colors = new Color[Uvs.Length];
    private Pose _from = Guard;
    private double _clock;
    private float _blend = 1;

    internal Pose Current { get; private set; } = Guard;

    internal void BeginMotion()
    {
        _from = Current;
        _blend = 0;
    }

    internal void Advance(PortraitMotion motion, double elapsed, double delta, bool reduced, bool flip)
    {
        _clock += delta;
        var time = (float)_clock + (flip ? 1.36f : 0);
        var progress = Mathf.Clamp((float)(elapsed / RelikarioPortrait.DurationFor(motion)), 0, 1);
        var breath = Mathf.Sin(time * 2.25f);
        var pose = motion switch
        {
            PortraitMotion.Attack => Sample(Attack, progress),
            PortraitMotion.Hit => Recoil(progress, .43f),
            PortraitMotion.HitNormal => Recoil(progress, .71f),
            PortraitMotion.HitCritical => Recoil(progress, 1),
            PortraitMotion.Heal => Sample(Heal, progress),
            PortraitMotion.ExhaustedIdle => new(new(-.024f, .017f + breath * .005f),
                -13 + breath * 1.4f, .84f, -.06f, .014f * breath, 0, Fatigue: 1),
            _ => Guard
        };
        pose = pose with
        {
            Height = pose.Height + breath * .011f,
            Hair = pose.Hair + Mathf.Sin(time * 2.8f - .5f) * .009f
        };
        if (reduced) pose = Guard.Blend(pose, .36f) with { Glow = 0 };
        var blendSeconds = motion is PortraitMotion.Hit or PortraitMotion.HitNormal or PortraitMotion.HitCritical ? .045f : .12f;
        _blend = Mathf.Min(1, _blend + (float)delta / blendSeconds);
        Current = _from.Blend(pose, Ease(_blend));
    }

    internal Vector2 Launch(Rect2 target, bool flip) => Map(FigureRect(target), Muzzle, Current, flip);
    internal Vector2 Target(Rect2 target, bool flip) => Map(FigureRect(target), Chest, Current, flip);

    internal void Draw(CanvasItem canvas, Rect2 target, PortraitMotion motion, double elapsed, bool flip)
    {
        if (target.Size.X <= 0 || target.Size.Y <= 0) return;
        var figure = FigureRect(target);
        var pose = Current;
        var color = Colors.White.Lerp(new Color(.75f, .70f, .64f), pose.Fatigue).Lerp(Light, pose.Glow * .27f);
        for (var i = 0; i < Uvs.Length; i++)
        {
            _points[i] = Map(figure, Uvs[i], pose, flip);
            _colors[i] = color;
        }
        canvas.DrawSetTransform(Vector2.Zero);
        RenderingServer.CanvasItemAddTriangleArray(canvas.GetCanvasItem(), Triangles, _points, _colors, Uvs, [], [], sprite.GetRid());
        if (pose.Glow < .025f) return;
        var progress = (float)(elapsed / RelikarioPortrait.DurationFor(motion));
        var scale = target.Size.Y / 504f;
        if (motion == PortraitMotion.Heal)
        {
            var center = Target(target, flip);
            for (var i = 0; i < 8; i++)
            {
                var t = Mathf.Clamp((progress - i * .032f) / .72f, 0, 1);
                var at = center + new Vector2(Mathf.Sin(i * 2.41f + t * 4) * (45 + 5 * i), 100 - t * 200) * scale;
                Diamond(canvas, at, (3 + i % 3) * scale, BoardArt.Tint(Light, pose.Glow * Mathf.Sin(t * Mathf.Pi)));
            }
        }
        else
        {
            var launch = motion == PortraitMotion.Attack;
            var center = launch ? Launch(target, flip) : Target(target, flip);
            var count = motion == PortraitMotion.HitCritical || launch ? 7 : 4;
            for (var i = 0; i < count; i++)
            {
                var angle = launch ? -.94f + i * .31f : i * Mathf.Tau / count;
                var direction = Vector2.FromAngle(angle);
                if (flip) direction.X = -direction.X;
                var at = center + direction * ((12 + i % 3 * 7) + progress * 71) * scale;
                canvas.DrawLine(at, at + direction * (10 + i % 3 * 5) * scale, BoardArt.Tint(Gold, pose.Glow * .8f), 2.6f * scale, true);
            }
        }
    }

    private Rect2 FigureRect(Rect2 target)
    {
        var frame = new Rect2(target.Position + target.Size * new Vector2(.15f, .025f), target.Size * new Vector2(.70f, .97f));
        var fitted = BoardArt.FitArtwork(sprite, frame);
        return new Rect2(fitted.Position.X, frame.End.Y - fitted.Size.Y, fitted.Size.X, fitted.Size.Y);
    }

    private static Vector2 Map(Rect2 figure, Vector2 uv, Pose pose, bool flip)
    {
        var aboveBoots = 1 - Ease(Mathf.Clamp((uv.Y - .50f) / .45f, 0, 1));
        var gloveDelta = (uv - Glove) / new Vector2(.24f, .20f);
        var gloveWeight = Mathf.Exp(-gloveDelta.LengthSquared());
        var hairWeight = 1 - Ease(Mathf.Clamp((uv.Y - .05f) / .24f, 0, 1));
        var offset = (uv - new Vector2(.5f, .91f)) * new Vector2(1, pose.Height);
        offset = offset.Rotated(Mathf.DegToRad(pose.Lean * aboveBoots));
        offset += new Vector2(.5f, .91f) + pose.Offset * aboveBoots;
        offset.X += pose.Punch * gloveWeight + pose.Hair * hairWeight;
        offset.Y -= pose.Punch * gloveWeight * .095f;
        if (flip) offset.X = 1 - offset.X;
        return figure.Position + offset * figure.Size;
    }

    private static Pose Recoil(float progress, float force)
    {
        var recoil = Ease(Math.Min(1, progress / .095f)) * (1 - Ease(Mathf.Clamp((progress - .13f) / .68f, 0, 1)));
        var echo = Mathf.Sin(progress * 42) * Mathf.Exp(-progress * 10) * force;
        return new(new(-recoil * force * .14f + echo * .018f, recoil * force * .035f),
            -18 * recoil * force, 1 - .13f * recoil * force, -.063f * recoil * force, -.065f * echo,
            Math.Max(0, recoil * force * (1 - progress)));
    }

    private static Pose Sample((float Time, Pose Pose)[] keys, float progress)
    {
        for (var i = 1; i < keys.Length; i++)
        {
            var end = keys[i];
            if (progress > end.Time) continue;
            var start = keys[i - 1];
            return start.Pose.Blend(end.Pose, Ease((progress - start.Time) / (end.Time - start.Time)));
        }
        return keys[^1].Pose;
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    private static void Diamond(CanvasItem canvas, Vector2 at, float radius, Color color) =>
        canvas.DrawColoredPolygon([at + new Vector2(0, -radius), at + new Vector2(radius, 0),
            at + new Vector2(0, radius), at + new Vector2(-radius, 0)], color);

    private static Vector2[] BuildUvs()
    {
        var uvs = new Vector2[GridSize * GridSize];
        for (var y = 0; y < GridSize; y++)
            for (var x = 0; x < GridSize; x++)
                uvs[y * GridSize + x] = new Vector2(x, y) / (GridSize - 1);
        return uvs;
    }

    private static int[] BuildTriangles()
    {
        var indices = new int[(GridSize - 1) * (GridSize - 1) * 6];
        var index = 0;
        for (var y = 0; y < GridSize - 1; y++)
            for (var x = 0; x < GridSize - 1; x++)
            {
                var top = y * GridSize + x;
                var bottom = top + GridSize;
                indices[index++] = top;
                indices[index++] = top + 1;
                indices[index++] = bottom;
                indices[index++] = top + 1;
                indices[index++] = bottom + 1;
                indices[index++] = bottom;
            }
        return indices;
    }
}
