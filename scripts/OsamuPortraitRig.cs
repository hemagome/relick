using Godot;

namespace Relick;

/// <summary>Osamu's hand-animated cutout. Coordinates use the arena's 608 × 504 portrait canvas.</summary>
internal sealed class OsamuPortraitRig(Texture2D body, Texture2D sword)
{
    internal readonly record struct Pose(Vector2 Offset, float Lean, float SwordAngle, float Height, float Effect)
    {
        internal Pose Blend(Pose to, float weight) => new(Offset.Lerp(to.Offset, weight), Mathf.Lerp(Lean, to.Lean, weight),
            Mathf.Lerp(SwordAngle, to.SwordAngle, weight), Mathf.Lerp(Height, to.Height, weight), Mathf.Lerp(Effect, to.Effect, weight));
    }

    private static readonly Pose Guard = new(Vector2.Zero, 0, -49, 1, 0);
    private static readonly Rect2 BodyRect = new(-28, 52, 515, 412);
    private static readonly Vector2 Waist = new(153, 408);
    // Grip anchors measured on the source textures, including their transparent margins.
    private static readonly Vector2 Hand = BodyRect.Position + BodyRect.Size * new Vector2(.738f, .563f);
    private static readonly Rect2 SwordRect = new(-28, -42, 246, 82);
    private static readonly Color MendLight = new("5df3b4");
    private static readonly Color ImpactLight = new("ff9c8b");

    private static readonly (float Time, Pose Pose)[] Attack =
    [
        (0, Guard),
        (.22f, new(new(-32, 7), -9, -62, .98f, 0)),
        (.35f, new(new(34, -10), 10, -38, 1.02f, 0)),
        (.53f, new(new(20, 5), 8, -32, .98f, 0)),
        (.73f, new(new(5, 3), 2, -44, .99f, 0)),
        (1, Guard)
    ];
    private static readonly (float Time, Pose Pose)[] Heal =
    [
        (0, Guard),
        (.20f, new(new(-7, 15), 3, -67, .945f, .12f)),
        (.50f, new(new(-3, -8), -4, -63, 1.035f, 1)),
        (.77f, new(new(0, -3), -1, -55, 1.015f, .62f)),
        (1, Guard)
    ];

    private double _clock;
    private float _blend = 1;
    private Pose _from = Guard;
    internal Pose Current { get; private set; } = Guard;

    internal void BeginMotion()
    {
        _from = Current;
        _blend = 0;
    }

    internal void Advance(PortraitMotion motion, double elapsed, double delta, bool reduced, bool mirrored)
    {
        _clock += delta;
        var time = (float)_clock + (mirrored ? 1.47f : 0);
        var progress = Mathf.Clamp((float)(elapsed / RelikarioPortrait.DurationFor(motion)), 0, 1);
        var next = motion switch
        {
            PortraitMotion.Attack => At(Attack, progress),
            PortraitMotion.Heal => At(Heal, progress),
            PortraitMotion.Hit => Recoil(progress, .44f),
            PortraitMotion.HitNormal => Recoil(progress, .71f),
            PortraitMotion.HitCritical => Recoil(progress, 1),
            PortraitMotion.ExhaustedIdle => Exhausted(time),
            _ => Guard
        };
        var breath = Mathf.Sin(time * 1.91f);
        next = next with { Height = next.Height + breath * .010f,
            SwordAngle = next.SwordAngle + Mathf.Sin(time * 2.18f - .57f) * 2.5f };
        if (reduced) next = Guard.Blend(next, .62f) with { Effect = next.Effect * .33f };
        // A brief blend starts every clip from the pose actually on screen, including after a critical hit.
        var blendSeconds = motion is PortraitMotion.Hit or PortraitMotion.HitNormal or PortraitMotion.HitCritical ? .045f : .14f;
        _blend = Mathf.Min(1, _blend + (float)delta / blendSeconds);
        Current = _from.Blend(next, Ease(_blend));
    }

    internal void Draw(CanvasItem canvas, Rect2 target, PortraitMotion motion, double elapsed, bool flip)
    {
        var scale = target.Size.X / 608;
        var view = new Transform2D(0, new Vector2(flip ? -scale : scale, scale), 0,
            flip ? new Vector2(target.End.X, target.Position.Y) : target.Position);
        var pose = Current;
        var torso = view * new Transform2D(Mathf.DegToRad(pose.Lean), new Vector2(1, pose.Height), 0, Waist + pose.Offset) *
            new Transform2D(0, -Waist);
        var blade = torso * new Transform2D(Mathf.DegToRad(pose.SwordAngle), Hand);
        var progress = Mathf.Clamp((float)(elapsed / RelikarioPortrait.DurationFor(motion)), 0, 1);

        canvas.DrawSetTransformMatrix(blade);
        var glow = .035f * (.5f + .5f * Mathf.Sin((float)_clock * 3.4f));
        canvas.DrawTextureRect(sword, SwordRect, false, new Color(1, 1 + glow, 1 + glow * 1.7f));
        canvas.DrawSetTransformMatrix(torso);
        canvas.DrawTextureRect(body, BodyRect, false);

        if (motion == PortraitMotion.Heal && pose.Effect > .01f) DrawMend(canvas, progress, pose.Effect);
        if (motion is PortraitMotion.Hit or PortraitMotion.HitNormal or PortraitMotion.HitCritical && pose.Effect > .01f)
            DrawImpact(canvas, progress, pose.Effect, motion == PortraitMotion.HitCritical);
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    private static Pose At((float Time, Pose Pose)[] keys, float progress)
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

    private static Pose Recoil(float progress, float force)
    {
        var recoil = Ease(Mathf.Min(1, progress / .11f)) * (1 - Ease(Mathf.Clamp((progress - .12f) / .68f, 0, 1)));
        var shock = Mathf.Sin(progress * 47) * Mathf.Exp(-progress * 11) * force;
        return new(new(-49 * force * recoil + shock * 8, 20 * force * recoil), -14 * force * recoil,
            -49 - 31 * force * recoil + shock * 7, 1 - .104f * force * recoil, recoil * force);
    }

    private static Pose Exhausted(float time)
    {
        var tremor = Mathf.Sin(time * 2.24f) + .36f * Mathf.Sin(time * 5.38f);
        return new(new(-14, 22 + tremor * 2.8f), 12 + tremor * 1.4f, 50 + tremor * 6, .896f, 0);
    }

    private static void DrawMend(CanvasItem canvas, float progress, float power)
    {
        for (var i = 0; i < 7; i++)
        {
            var t = Mathf.Clamp((progress - i * .038f) / .68f, 0, 1);
            var pos = new Vector2(147 + Mathf.Sin(i * 2.4f + t * 4) * (34 + i * 7), 388 - t * (150 + i * 11));
            var alpha = power * Mathf.Sin(t * Mathf.Pi);
            var color = BoardArt.Tint(MendLight, alpha);
            canvas.DrawRect(new Rect2(pos - new Vector2(2, 7), new Vector2(4, 14)), color);
            canvas.DrawRect(new Rect2(pos - new Vector2(7, 2), new Vector2(14, 4)), color);
        }
    }

    private static void DrawImpact(CanvasItem canvas, float progress, float power, bool critical)
    {
        var origin = new Vector2(201, 246);
        var count = critical ? 6 : 3;
        for (var i = 0; i < count; i++)
        {
            var direction = Vector2.FromAngle(-.68f - i * 1.08f);
            var distance = 28 + progress * 193 + i * 12;
            var color = BoardArt.Tint(ImpactLight, power * (1 - progress) * .88f);
            canvas.DrawLine(origin + direction * distance, origin + direction * (distance + (critical ? 40 : 19)), color, 3.4f, true);
        }
    }
}
