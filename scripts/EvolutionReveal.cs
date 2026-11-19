using Godot;
using Relick.Core;

namespace Relick;

/// <summary>The public Relikario card leaves its portrait, turns into its next form, and settles back onto the board.</summary>
public sealed partial class EvolutionReveal : Control
{
    internal const float CardAspect = 697f / 987;
    internal const float RevealAt = 0.53f;
    internal const float Duration = 1.48f;
    private const float ArriveAt = 0.36f, ReturnAt = 1.10f;
    private static readonly Color Gold = new("f3c776"), Light = new("fff4cf");
    private GradientTexture2D _glow = null!;
    private Texture2D? _beforeArt, _afterArt;
    private string _beforeCaption = "", _afterCaption = "";
    private Color _accent;
    private Rect2? _origin, _landing;
    private float _age;

    internal int RelativePlayer { get; private set; }
    internal bool Revealed => _age >= RevealAt;
    internal Texture2D? FaceTexture => Revealed ? _afterArt : _beforeArt;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseForcePassScrollEvents = false;
        ZIndex = 25;
        _glow = new GradientTexture2D
        {
            Width = 128, Height = 128,
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new(.5f, .5f), FillTo = new(1, .5f),
            Gradient = new Gradient
            {
                Offsets = [0, .24f, .62f, 1],
                Colors = [Colors.White, new Color(1, 1, 1, .56f), new Color(1, 1, 1, .14f), new Color(1, 1, 1, 0)]
            }
        };
    }

    internal void Play(int relativePlayer, RelikarioDefinition before, RelikarioDefinition after, Rect2? origin)
    {
        RelativePlayer = relativePlayer;
        _origin = origin;
        _landing = null;
        _beforeArt = RelikarioArt.Card(before.Id, before.Level) ?? RelikarioArt.Portrait(before.Id);
        _afterArt = RelikarioArt.Card(after.Id, after.Level) ?? RelikarioArt.Portrait(after.Id);
        _beforeCaption = $"{before.DisplayName} · Lv {before.Level}";
        _afterCaption = $"{after.DisplayName} · Lv {after.Level}";
        _accent = relativePlayer == 0 ? ArenaPresentation.Mint : ArenaPresentation.Coral;
        _age = 0;
        Show();
        QueueRedraw();
    }

    internal void Stop()
    {
        Hide();
        _beforeArt = _afterArt = null;
    }

    internal void AdvancePlayback(double delta, Rect2? landing)
    {
        _age += (float)delta;
        if (_age >= Duration) { Stop(); return; }
        _landing = landing;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible || Size.Y <= 0 || Size.X <= 0) return;
        var entry = Mathf.SmoothStep(0, ArriveAt, _age);
        var exit = Mathf.SmoothStep(ReturnAt, Duration, _age);
        var veil = Mathf.SmoothStep(0, .22f, _age) * (1 - exit);
        var focus = new Vector2(Size.X * .5f, Size.Y * .48f);
        var height = Math.Min(440, Math.Min(Size.Y * .61f, Size.X * .53f));
        var cardSize = new Vector2(height * CardAspect, height);
        var center = (_origin?.GetCenter() ?? focus).Lerp(focus, entry).Lerp(_landing?.GetCenter() ?? focus, exit);
        var extent = (_origin?.Size ?? cardSize * .82f).Lerp(cardSize, entry).Lerp(_landing?.Size ?? cardSize * .87f, exit);
        var turn = Mathf.SmoothStep(ArriveAt, 2 * RevealAt - ArriveAt, _age);
        var flip = Math.Max(.045f, Mathf.Abs(Mathf.Cos(turn * Mathf.Pi)));
        var angle = Mathf.Sin(entry * Mathf.Pi) * (RelativePlayer == 0 ? -.19f : .19f) + Mathf.Sin(turn * Mathf.Pi) * .10f;
        var burst = Revealed ? 1 - Mathf.SmoothStep(RevealAt, RevealAt + .32f, _age) : 0;
        var cardOpacity = Mathf.SmoothStep(0, .12f, _age) * (1 - exit * exit);

        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.027f, .036f, .072f, .76f * veil));
        DrawSigil(focus, height, veil, burst);
        DrawCard(center, extent, angle, flip, cardOpacity, burst);

        var captionOpacity = Mathf.SmoothStep(.20f, .35f, _age) * (1 - Mathf.SmoothStep(ReturnAt, ReturnAt + .18f, _age));
        DrawCaption("EVOLUCIÓN", focus - new Vector2(0, height / 2 + 22), (int)Math.Clamp(height * .08f, 20, 34), Light, captionOpacity);
        var captionSize = (int)Math.Clamp(height * .055f, 14, 22);
        DrawCaption(Revealed ? _afterCaption : _beforeCaption, focus + new Vector2(0, height / 2 + captionSize + 13),
            captionSize, Revealed ? Light : Colors.White, captionOpacity);
    }

    private void DrawSigil(Vector2 center, float height, float opacity, float burst)
    {
        var radius = height * .68f;
        var gold = BoardArt.Tint(Gold, .55f * opacity);
        DrawGlow(center, radius * 1.65f, _accent, .46f * opacity);
        DrawGlow(center, radius * 1.14f, Gold, (.25f + .49f * burst) * opacity);
        DrawArc(center, radius, 0, Mathf.Tau, 96, BoardArt.Tint(Gold, .24f * opacity), 1, true);
        DrawArc(center, radius * .88f, 0, Mathf.Tau, 96, BoardArt.Tint(_accent, .45f * opacity), 1, true);
        for (var i = 0; i < 4; i++)
        {
            var angle = _age * .68f + i * Mathf.Tau / 4;
            DrawArc(center, radius * 1.05f, angle, angle + .96f, 24, gold, 2, true);
        }
        for (var i = 0; i < 24; i++)
        {
            var direction = Vector2.FromAngle(-_age * .35f + i * Mathf.Tau / 24);
            DrawLine(center + direction * radius * (i % 3 == 0 ? .91f : .96f), center + direction * radius, gold, 1, true);
        }
        if (burst > 0)
            DrawArc(center, radius * Mathf.Lerp(.8f, 1.35f, 1 - burst), 0, Mathf.Tau, 96, BoardArt.Tint(Light, .66f * burst * opacity), 2, true);

        var gather = Mathf.SmoothStep(0, RevealAt, _age);
        var drift = Mathf.SmoothStep(RevealAt, Duration, _age);
        for (var i = 0; i < 24; i++)
        {
            var variation = i % 7 / 6f;
            var direction = Vector2.FromAngle(i * 2.39996f + _age * (.46f + variation * .32f));
            var distance = radius * (1.3f + variation * .33f - .77f * gather + drift * (.62f + variation * .46f));
            var point = center + direction * distance;
            var trail = direction * height * (.018f + .06f * burst + variation * .025f);
            var color = BoardArt.Tint(i % 3 == 0 ? _accent : Light, opacity * (.38f + .45f * variation));
            DrawLine(point - trail, point, color, 1.25f, true);
            DrawCircle(point, 1.4f + variation * 1.4f, color, antialiased: true);
        }
    }

    private void DrawCard(Vector2 center, Vector2 extent, float angle, float flip, float opacity, float burst)
    {
        var rect = new Rect2(-extent / 2, extent);
        DrawSetTransform(center, angle, new Vector2(flip, 1));
        DrawRect(rect.Grow(7), new Color(.02f, .026f, .055f, .84f * opacity));
        DrawRect(rect.Grow(4), BoardArt.Tint(Revealed ? Light : Gold, .92f * opacity), false, 1.5f);
        DrawRect(rect, new Color(.032f, .043f, .082f, opacity));
        if (FaceTexture is { } artwork)
            DrawTextureRect(artwork, BoardArt.FitArtwork(artwork, rect), false, new Color(1, 1, 1, opacity));
        if (burst > 0) DrawRect(rect, BoardArt.Tint(Light, .65f * burst * opacity));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawGlow(Vector2 center, float radius, Color color, float opacity) =>
        DrawTextureRect(_glow, new Rect2(center - Vector2.One * radius, Vector2.One * radius * 2), false, BoardArt.Tint(color, opacity));

    private void DrawCaption(string caption, Vector2 baseline, int size, Color color, float opacity)
    {
        if (opacity <= 0) return;
        var font = RelickVisuals.Display;
        var left = baseline - new Vector2(font.GetStringSize(caption, fontSize: size).X / 2, 0);
        DrawString(font, left + new Vector2(0, 2), caption, fontSize: size, modulate: new Color(0, 0, 0, opacity));
        DrawString(font, left, caption, fontSize: size, modulate: BoardArt.Tint(color, opacity));
    }
}
