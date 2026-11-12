using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Large reading copy of the card under the pointer or selection. It shows public art only.</summary>
public partial class CardShowcase : Control
{
    private const double AppearSeconds = 0.18;
    private CardDefinition? _card;
    private Texture2D? _art, _back;
    private double _elapsed = AppearSeconds;
    internal CardDefinition? Card => _card;
    internal bool ReducedMotion { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _back = GD.Load<Texture2D>("res://assets/cards/card-back.png");
        ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();
        Resized += QueueRedraw;
        SetProcess(false);
    }

    internal static Texture2D? Artwork(CardDefinition card) => card.Kind == CardKind.Skill
        ? SkillArtwork.Load(card)
        : BoardArt.LoadCardArtwork(card) ?? (card.Kind == CardKind.Attack ? BoardArt.LoadCardArtwork(CardCatalog.Attack) : null);

    internal void Present(CardDefinition? card)
    {
        if (_card?.Id == card?.Id) return;
        _card = card;
        _art = card == null ? null : Artwork(card);
        _elapsed = ReducedMotion ? AppearSeconds : 0;
        SetProcess(!ReducedMotion);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += Math.Max(0, delta);
        if (_elapsed >= AppearSeconds) { _elapsed = AppearSeconds; SetProcess(false); }
        QueueRedraw();
    }

    public override void _Draw()
    {
        var area = new Rect2(Vector2.Zero, Size).Grow(-10);
        if (area.Size.X <= 0 || area.Size.Y <= 0) return;
        if (_card == null || _art == null)
        {
            var slot = BoardArt.FitArtwork(_back!, area.Grow(-18));
            BoardArt.Socket(this, slot.Grow(6), RelickVisuals.Playmat.Trace);
            DrawTextureRect(_back!, slot, false, new Color(1, 1, 1, 0.10f));
            var hint = _card?.Name ?? "Pasa el cursor sobre una carta";
            BoardArt.CenterText(this, hint, new(Size.X / 2, slot.GetCenter().Y + 5), 13, BoardArt.Muted);
            return;
        }
        var t = (float)(_elapsed / AppearSeconds);
        var eased = 1 - (1 - t) * (1 - t);
        var accent = BoardArt.CardColor(_card);
        var fit = BoardArt.FitArtwork(_art, area);
        var rect = new Rect2(fit.GetCenter() - fit.Size * Mathf.Lerp(0.94f, 1, eased) / 2 + new Vector2(0, (1 - eased) * 8), fit.Size * Mathf.Lerp(0.94f, 1, eased));
        RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 6), rect.Size).Grow(-4), new Color(0, 0, 0, 0.6f * eased), 14, 8);
        RelickVisuals.Shadow(this, rect.Grow(-4), BoardArt.Tint(accent, 0.35f * eased), 16, 8);
        DrawTextureRect(_art, rect, false, new Color(1, 1, 1, eased));
    }
}

/// <summary>Cosmetic feedback above the board: damage numbers and landing flashes. It never blocks play.</summary>
public partial class ImpactLayer : Control
{
    private const double NumberSeconds = 1.25, RingSeconds = 0.42;
    private readonly List<(Vector2 At, string Text, Color Color, bool Heavy, double Age)> _numbers = [];
    private readonly List<(Rect2 Rect, Color Color, double Age)> _rings = [];
    private const double BeamSeconds = 0.34, FlashSeconds = 0.22;
    private readonly List<(Vector2 From, Vector2 To, Color Color, double Age)> _beams = [];
    private double _flash = FlashSeconds;
    private Color _flashColor;
    internal bool ReducedMotion { get; set; }
    internal int ActiveCount => _numbers.Count + _rings.Count + _beams.Count + (_flash < FlashSeconds ? 1 : 0);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 22;
        ReducedMotion = DisplayServer.AccessibilityShouldReduceAnimation() == 1 ||
            ProjectSettings.GetSetting("relick/accessibility/reduced_motion", false).AsBool();
        SetProcess(false);
    }

    internal void Number(Vector2 at, string text, Color color, bool heavy = false)
    {
        _numbers.Add((at, text, color, heavy, 0));
        SetProcess(true); QueueRedraw();
    }

    internal void Ring(Rect2 rect, Color color)
    {
        if (ReducedMotion) return;
        _rings.Add((rect, color, 0));
        SetProcess(true); QueueRedraw();
    }

    /// <summary>A streak from the attacker toward the defender as the attack clip starts.</summary>
    internal void Beam(Vector2 from, Vector2 to, Color color)
    {
        if (ReducedMotion) return;
        _beams.Add((from, to, color, 0));
        SetProcess(true); QueueRedraw();
    }

    internal void Flash(Color color)
    {
        if (ReducedMotion) return;
        _flash = 0; _flashColor = color;
        SetProcess(true); QueueRedraw();
    }

    internal void Clear()
    {
        _numbers.Clear(); _rings.Clear(); _beams.Clear(); _flash = FlashSeconds;
        SetProcess(false); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var step = Math.Max(0, delta);
        for (var i = _numbers.Count - 1; i >= 0; i--)
        {
            var n = _numbers[i]; n.Age += step;
            if (n.Age >= NumberSeconds) _numbers.RemoveAt(i); else _numbers[i] = n;
        }
        for (var i = _rings.Count - 1; i >= 0; i--)
        {
            var r = _rings[i]; r.Age += step;
            if (r.Age >= RingSeconds) _rings.RemoveAt(i); else _rings[i] = r;
        }
        for (var i = _beams.Count - 1; i >= 0; i--)
        {
            var b = _beams[i]; b.Age += step;
            if (b.Age >= BeamSeconds) _beams.RemoveAt(i); else _beams[i] = b;
        }
        if (_flash < FlashSeconds) _flash = Math.Min(FlashSeconds, _flash + step);
        if (ActiveCount == 0) SetProcess(false);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var origin = GlobalPosition;
        if (_flash < FlashSeconds)
            DrawRect(new Rect2(Vector2.Zero, Size), BoardArt.Tint(_flashColor, 0.20f * (1 - (float)(_flash / FlashSeconds))));
        foreach (var (from, to, color, age) in _beams)
        {
            var t = (float)(age / BeamSeconds);
            var head = (from - origin).Lerp(to - origin, Mathf.SmoothStep(0, 1, Math.Min(1, t * 1.5f)));
            var tail = (from - origin).Lerp(to - origin, Mathf.SmoothStep(0, 1, Math.Max(0, t * 1.5f - 0.5f)));
            var alpha = 1 - t * t;
            DrawLine(tail, head, BoardArt.Tint(color, 0.25f * alpha), 22, true);
            DrawLine(tail, head, BoardArt.Tint(color, 0.55f * alpha), 9, true);
            DrawLine(tail, head, BoardArt.Tint(Colors.White, 0.9f * alpha), 3, true);
        }
        foreach (var (rect, color, age) in _rings)
        {
            var t = (float)(age / RingSeconds);
            var grown = new Rect2(rect.Position - origin, rect.Size).Grow(2 + 16 * t);
            RelickVisuals.Shadow(this, grown, BoardArt.Tint(color, 0.5f * (1 - t)), 12);
            BoardArt.Outline(this, grown, BoardArt.Tint(color, 1 - t), 6, 2);
        }
        foreach (var (at, text, color, heavy, age) in _numbers)
        {
            var t = (float)(age / NumberSeconds);
            // Punch in, hold, then drift up while fading.
            var pop = ReducedMotion ? 1 : t < 0.12f ? Mathf.Lerp(0.4f, 1.25f, t / 0.12f) : t < 0.22f ? Mathf.Lerp(1.25f, 1, (t - 0.12f) / 0.10f) : 1;
            var alpha = t < 0.65f ? 1 : 1 - (t - 0.65f) / 0.35f;
            var size = (int)((heavy ? 64 : 48) * pop);
            var point = at - origin + new Vector2(0, ReducedMotion ? 0 : -46 * t * t);
            if (!ReducedMotion && t < 0.3f)
            {
                var burst = t / 0.3f;
                for (var ray = 0; ray < 10; ray++)
                {
                    var direction = Vector2.FromAngle(ray * Mathf.Tau / 10 + 0.3f);
                    DrawLine(point + new Vector2(0, -size * 0.3f) + direction * (22 + 46 * burst),
                        point + new Vector2(0, -size * 0.3f) + direction * (36 + 78 * burst), BoardArt.Tint(color, 1 - burst), heavy ? 4 : 3, true);
                }
            }
            var font = RelickVisuals.Display;
            var width = font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
            var start = point - new Vector2(width / 2, 0);
            DrawStringOutline(font, start, text, HorizontalAlignment.Left, -1, size, 10, new Color(0.02f, 0.03f, 0.07f, alpha));
            DrawString(font, start, text, HorizontalAlignment.Left, -1, size, BoardArt.Tint(color, alpha));
        }
    }
}
