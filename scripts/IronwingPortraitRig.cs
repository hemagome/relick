using Godot;

namespace Relick;

/// <summary>Rigid sprite, flight controls, and effects sampled in arena portrait coordinates.</summary>
internal sealed class IronwingPortraitRig(Texture2D hull)
{
    internal const double FireAt = .48;
    private static readonly Vector2 Center = new(304, 273);
    private static readonly Rect2 HullRect = new(-237, -158, 474, 316);
    private static readonly Vector2 FarEngine = OnHull(.586f, .328f);
    private static readonly Vector2 NearEngine = OnHull(.720f, .432f);
    private static readonly Vector2 FarGun = OnHull(.130f, .484f);
    private static readonly Vector2 NearGun = OnHull(.623f, .947f);
    private static readonly Vector2 Cockpit = OnHull(.322f, .655f);
    private static readonly Color Cyan = new("54e9ff"), Ice = new("e2feff"), Green = new("67fac5"), Amber = new("ffa957");
    private static Texture2D? _exhaust;
    private static Texture2D? _smoke;

    internal readonly record struct Pose(Vector2 Offset, float Bank, Vector2 Scale, float Thrust, float Impact)
    {
        internal Pose Blend(Pose to, float weight) => new(Offset.Lerp(to.Offset, weight), Mathf.Lerp(Bank, to.Bank, weight),
            Scale.Lerp(to.Scale, weight), Mathf.Lerp(Thrust, to.Thrust, weight), Mathf.Lerp(Impact, to.Impact, weight));
    }

    private static readonly Pose Cruise = new(Vector2.Zero, .10f, Vector2.One, .56f, 0);
    private Pose _from = Cruise;
    private double _clock;
    private float _blend = 1;
    internal Pose Current { get; private set; } = Cruise;

    internal void BeginMotion()
    {
        _from = Current;
        _blend = 0;
    }

    internal void Advance(PortraitMotion motion, double elapsed, double delta, bool reduced, bool mirrored)
    {
        _clock += delta;
        var time = (float)_clock + (mirrored ? 1.41f : 0);
        var p = Mathf.Clamp((float)(elapsed / RelikarioPortrait.DurationFor(motion)), 0, 1);
        var hover = new Vector2(Mathf.Sin(time * 1.64f) * 7, Mathf.Sin(time * 2.05f) * 10);
        var thrust = Cruise.Thrust + Mathf.Sin(time * 6.7f) * .06f;
        var cruise = Cruise with { Offset = hover, Bank = Cruise.Bank + Mathf.Sin(time * 1.73f) * .055f, Thrust = thrust };
        var next = motion switch
        {
            PortraitMotion.Attack => Attack(cruise, p),
            PortraitMotion.Hit => Recoil(cruise, p, .35f),
            PortraitMotion.HitNormal => Recoil(cruise, p, .66f),
            PortraitMotion.HitCritical => Recoil(cruise, p, 1),
            PortraitMotion.Heal => Heal(cruise, p),
            PortraitMotion.ExhaustedIdle => new(new Vector2(14 + hover.X * .55f, 39 + hover.Y * .41f),
                -.12f + Mathf.Sin(time * 4.4f) * .048f, new Vector2(1, .94f),
                .25f + .11f * Mathf.Sin(time * 12) * Mathf.Sin(time * 2.3f), 0),
            _ => cruise
        };
        if (reduced) next = Cruise.Blend(next, .34f) with { Impact = next.Impact * .40f };
        _blend = Mathf.Min(1, _blend + (float)delta / .11f);
        Current = _from.Blend(next, Ramp(0, 1, _blend));
    }

    internal (Vector2 Far, Vector2 Near) Guns(Rect2 target, bool mirrored)
    {
        var transform = HullTransform(target, mirrored);
        return (transform * FarGun, transform * NearGun);
    }

    internal Vector2 Target(Rect2 target, bool mirrored) => HullTransform(target, mirrored) * Cockpit;

    internal void Draw(CanvasItem canvas, Rect2 target, PortraitMotion motion, double elapsed, bool mirrored, bool reduced)
    {
        if (target.Size.X <= 0) return;
        var transform = HullTransform(target, mirrored);
        var p = Mathf.Clamp((float)(elapsed / RelikarioPortrait.DurationFor(motion)), 0, 1);
        DrawEngine(canvas, transform, FarEngine, Current.Thrust * .88f);
        DrawEngine(canvas, transform, NearEngine, Current.Thrust);
        canvas.DrawSetTransformMatrix(transform);
        if (motion == PortraitMotion.ExhaustedIdle)
            DrawSmoke(canvas, reduced ? 1.57 : _clock, Ramp(0, 1, _blend) * (reduced ? .65f : 1f));
        var tint = Colors.White.Lerp(Amber, Current.Impact * .18f);
        canvas.DrawTextureRect(hull, HullRect, false, tint);
        if (motion == PortraitMotion.Attack && !reduced)
        {
            var shot = (float)(elapsed - FireAt);
            var charge = Ramp(-.24f, -.04f, shot) * (1 - Ramp(-.04f, 0, shot)) * .42f;
            var flash = Ramp(-.025f, .025f, shot) * (1 - Ramp(.025f, .18f, shot));
            DrawMuzzle(canvas, FarGun, charge + flash);
            DrawMuzzle(canvas, NearGun, charge + flash);
        }
        if (Current.Impact > .01f) DrawSparks(canvas, p, Current.Impact, motion == PortraitMotion.HitCritical);
        if (motion == PortraitMotion.Heal) DrawRepair(canvas, p, reduced);
        canvas.DrawSetTransform(Vector2.Zero);
    }

    private Transform2D HullTransform(Rect2 target, bool mirrored)
    {
        var factor = target.Size.X / 608;
        // The source points left. Face into the board on either side, preserving the same bank.
        var view = new Transform2D(0, new Vector2(mirrored ? factor : -factor, factor), 0,
            mirrored ? target.Position : new Vector2(target.End.X, target.Position.Y));
        return view * new Transform2D(Current.Bank, Current.Scale, 0, Center + Current.Offset);
    }

    private static Pose Attack(Pose cruise, float p)
    {
        var pull = Pulse(p, 0, .17f, .20f, .37f);
        var surge = Pulse(p, .17f, .39f, .48f, .88f);
        var returnBank = Pulse(p, .48f, .67f, .69f, 1);
        return cruise with
        {
            Offset = cruise.Offset + new Vector2(22 * pull - 43 * surge, 8 * pull - 28 * surge + 12 * returnBank),
            Bank = cruise.Bank - .16f * pull + .61f * surge - .23f * returnBank,
            Scale = new(1 + .065f * surge, 1 - .15f * surge - .18f * returnBank),
            Thrust = cruise.Thrust - .20f * pull + .79f * surge
        };
    }

    private static Pose Recoil(Pose cruise, float p, float force)
    {
        var impact = Pulse(p, 0, .13f, .17f, .83f) * force;
        var flutter = Mathf.Sin(p * 41) * Mathf.Exp(-p * 7) * impact;
        var correction = Pulse(p, .30f, .57f, .64f, 1) * force;
        return cruise with
        {
            Offset = cruise.Offset + new Vector2(61 * impact - 12 * correction, 28 * impact + 9 * flutter),
            Bank = cruise.Bank - .42f * impact + .21f * correction + .18f * flutter,
            Scale = new(1 - .07f * impact, 1 - .21f * impact),
            Thrust = cruise.Thrust - .50f * impact + .34f * correction,
            Impact = impact
        };
    }

    private static Pose Heal(Pose cruise, float p)
    {
        var lift = Pulse(p, .02f, .45f, .55f, 1);
        return cruise with { Offset = cruise.Offset + new Vector2(-5, -26) * lift, Bank = cruise.Bank + .13f * lift,
            Scale = Vector2.One * (1 + .05f * lift), Thrust = cruise.Thrust + .39f * lift };
    }

    private static float Pulse(float t, float start, float peak, float release, float end) =>
        Ramp(start, peak, t) * (1 - Ramp(release, end, t));

    private static float Ramp(float start, float stop, float t) => Mathf.SmoothStep(start, stop, t);
    private static Vector2 OnHull(float x, float y) => HullRect.Position + HullRect.Size * new Vector2(x, y);

    private static void DrawEngine(CanvasItem canvas, Transform2D hullTransform, Vector2 nozzle, float power)
    {
        if (power <= .02f) return;
        var exhaust = _exhaust ??= CreateExhaust();
        canvas.DrawSetTransformMatrix(hullTransform * new Transform2D(-.70f, nozzle));
        var length = 30 + 66 * power;
        var width = 10 + 18 * power;
        canvas.DrawTextureRect(exhaust, new Rect2(-3, -width / 2, length, width), false, BoardArt.Tint(Cyan, .76f));
        canvas.DrawTextureRect(exhaust, new Rect2(-3, -width * .23f, length * .80f, width * .46f), false, BoardArt.Tint(Ice, .86f));
    }

    private static Texture2D CreateExhaust()
    {
        const int width = 96, height = 24;
        var data = new byte[width * height * 4];
        for (var x = 0; x < width; x++)
        {
            var t = x / (float)(width - 1);
            var tail = 1 - t;
            for (var y = 0; y < height; y++)
            {
                var cross = Mathf.Abs(2 * y / (float)(height - 1) - 1);
                var edge = Mathf.Max(0, 1 - cross / (.20f + .80f * tail));
                var offset = (y * width + x) * 4;
                data[offset] = data[offset + 1] = data[offset + 2] = 255;
                data[offset + 3] = (byte)(255 * edge * edge * tail * tail);
            }
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, data));
    }

    private static void DrawSmoke(CanvasItem canvas, double clock, float strength)
    {
        if (strength <= .01f) return;
        var smoke = _smoke ??= CreateSmoke();
        const int count = 10;
        const double interval = .15;
        var emission = clock / interval;
        var newest = Math.Floor(emission);
        // Draw the dissolving end first and layer the denser puffs at the nozzle on top.
        for (var i = count - 1; i >= 0; i--)
        {
            var age = (float)((emission - newest + i) / count);
            var phase = (float)((newest - i) % 29) * 2.4f;
            var curl = Mathf.Sin(phase + 4 * age) * (8 + 16 * age) * age;
            var point = NearEngine + new Vector2(109 * age + curl, -118 * age + curl * .45f);
            var size = new Vector2(1.12f, 1) * (22 + 74 * age) * (.91f + .09f * Mathf.Cos(phase));
            var density = Ramp(0, .11f, age) * (1 - Ramp(.28f, 1, age)) * strength;
            canvas.DrawTextureRect(smoke, new Rect2(point - size / 2, size), false, BoardArt.Tint(Colors.White, density));
        }
    }

    private static Texture2D CreateSmoke()
    {
        const int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var shadow = new Color("444956");
        var light = new Color("a1a7b4");
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var point = new Vector2(x + .5f, y + .5f) * (2f / size) - Vector2.One;
            var center = CloudLobe(point, new(-.13f, .11f), .71f);
            var crown = CloudLobe(point, new(-.20f, -.34f), .47f);
            var edge = CloudLobe(point, new(.28f, -.12f), .52f);
            var color = shadow.Lerp(light, Mathf.Clamp(crown * .67f + edge * .47f + center * .34f, 0, 1));
            color.A = Mathf.Max(center, Mathf.Max(crown, edge));
            image.SetPixel(x, y, color);
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static float CloudLobe(Vector2 point, Vector2 center, float radius) =>
        1 - Ramp(radius * .20f, radius, point.DistanceTo(center));

    private static void DrawMuzzle(CanvasItem canvas, Vector2 gun, float power)
    {
        if (power < .01f) return;
        canvas.DrawCircle(gun, 23 * power, BoardArt.Tint(Cyan, .24f * power), true);
        canvas.DrawCircle(gun, 9 * power, BoardArt.Tint(Ice, .93f * power), true);
    }

    private static void DrawSparks(CanvasItem canvas, float p, float impact, bool critical)
    {
        for (var i = 0; i < (critical ? 8 : 4); i++)
        {
            var ray = Vector2.FromAngle(i * 2.4f + .48f);
            var point = Cockpit + ray * (30 + p * (100 + 8 * i));
            var color = BoardArt.Tint(i % 2 == 0 ? Amber : Ice, impact * (1 - p));
            canvas.DrawLine(point, point + ray * (critical ? 22 : 12), color, critical ? 4 : 2, true);
        }
    }

    private static void DrawRepair(CanvasItem canvas, float p, bool reduced)
    {
        var power = Pulse(p, .07f, .35f, .73f, 1) * (reduced ? .35f : 1);
        for (var i = 0; i < 2; i++)
        {
            var angle = p * 3.8f + i * Mathf.Pi;
            canvas.DrawArc(Cockpit, 55 + 68 * p + i * 9, angle, angle + 1.68f, 28,
                BoardArt.Tint(Green, power * .78f), i == 0 ? 3 : 2, true);
        }
        for (var i = 0; i < 5; i++)
        {
            var point = new Vector2(-100 + i * 44, 59 - 116 * p + Mathf.Sin(i * 2.8f + p * 5) * 22);
            var color = BoardArt.Tint(i % 2 == 0 ? Green : Ice, power);
            canvas.DrawLine(point - new Vector2(5, 0), point + new Vector2(5, 0), color, 2, true);
            canvas.DrawLine(point - new Vector2(0, 5), point + new Vector2(0, 5), color, 2, true);
        }
    }
}
