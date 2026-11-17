using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Large reading copy of the card under the pointer or selection. It shows public art only.</summary>
public partial class CardShowcase : Control
{
    private const double AppearSeconds = 0.18;
    private CardDefinition? _card;
    private string? _key;
    private Texture2D? _art, _back;
    private Color _accent;
    private double _elapsed = AppearSeconds;
    internal CardDefinition? Card => _card;
    internal Texture2D? FaceTexture => _art;
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

    internal static Texture2D? Artwork(CardDefinition card, string? relikario = null) => card.Kind == CardKind.Skill
        ? SkillArtwork.Load(card)
        : BoardArt.LoadCardArtwork(card, relikario) ?? (card.Kind == CardKind.Attack ? BoardArt.LoadCardArtwork(CardCatalog.Attack, relikario) : null);

    internal void Present(CardDefinition? card, string? relikario = null)
    {
        var key = card?.Id == CardCatalog.Attack.Id ? $"attack:{RelikarioArt.Family(relikario)}" : card?.Id;
        Show(key, card, card == null ? null : Artwork(card, relikario), relikario);
    }

    /// <summary>Shows a reading sheet that is not a card, such as the explanation of a Relikia effect.</summary>
    internal void PresentSheet(string key, Texture2D? sheet) => Show("sheet:" + key, null, sheet);

    private void Show(string? key, CardDefinition? card, Texture2D? art, string? relikario = null)
    {
        if (_key == key) return;
        _key = key; _card = card; _art = art;
        _accent = card == null ? BoardArt.Gold : BoardArt.CardColor(card, relikario);
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
        if (_art == null)
        {
            var slot = BoardArt.FitArtwork(_back!, area.Grow(-18));
            BoardArt.Socket(this, slot.Grow(6), RelickVisuals.Playmat.Trace);
            DrawTextureRect(_back!, slot, false, new Color(1, 1, 1, 0.10f));
            if (_card != null) BoardArt.CenterText(this, _card.Name, new(Size.X / 2, slot.GetCenter().Y + 5), 13, BoardArt.Muted);
            return;
        }
        var t = (float)(_elapsed / AppearSeconds);
        var eased = 1 - (1 - t) * (1 - t);
        var fit = BoardArt.FitArtwork(_art, area);
        var rect = new Rect2(fit.GetCenter() - fit.Size * Mathf.Lerp(0.94f, 1, eased) / 2 + new Vector2(0, (1 - eased) * 8), fit.Size * Mathf.Lerp(0.94f, 1, eased));
        RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 6), rect.Size).Grow(-4), new Color(0, 0, 0, 0.6f * eased), 14, 8);
        RelickVisuals.Shadow(this, rect.Grow(-4), BoardArt.Tint(_accent, 0.35f * eased), 16, 8);
        DrawTextureRect(_art, rect, false, new Color(1, 1, 1, eased));
    }
}

/// <summary>Cosmetic feedback above the board: damage numbers and landing flashes. It never blocks play.</summary>
public partial class ImpactLayer : Control
{
    private const double NumberSeconds = 1.25, RingSeconds = 0.42;
    private readonly List<(Vector2 At, string Text, Color Color, bool Heavy, double Age)> _numbers = [];
    private readonly List<(Rect2 Rect, Color Color, double Age)> _rings = [];
    private const double FlashSeconds = 0.22;
    private sealed record Projectile(RelikarioPortrait Attacker, RelikarioPortrait Defender, Texture2D RightArt, Texture2D? LeftArt, bool Lob)
    {
        public double Age { get; set; }
        public double Release => Lob ? 0.42 : 0.48;
        public double Travel => Lob ? 0.44 : 0.34;
    }
    private readonly List<Projectile> _projectiles = [];
    private double _flash = FlashSeconds;
    private Color _flashColor;
    internal bool ReducedMotion { get; set; }
    private const double CardSeconds = 0.8;
    private (Texture2D Art, Vector2 Center, Color Accent, double Age)? _card;
    /// <summary>A code just played by either duelist is shown for a moment over the middle of the board.</summary>
    internal bool ShowingCard => _card != null;
    private const double SummonGather = 0.45, SummonPop = 0.3, SummonFly = 0.45, SummonFade = 0.2;
    /// <summary>Seconds from a summon's start until the flying Trap reaches its place and starts to fade there.</summary>
    internal const double SummonLanding = SummonGather + SummonPop + SummonFly;
    /// <summary>How long the landed Trap takes to fade into the real card underneath.</summary>
    internal const double SummonLandingFade = SummonFade;
    private sealed record Summon(Texture2D[] Ingredients, Vector2 From, Vector2 Meet, Texture2D Trap, Rect2? To, Color Accent)
    {
        public double Age { get; set; }
        public double Duration => SummonGather + SummonPop + (To != null ? SummonFly : 0.35) + SummonFade;
    }
    private Summon? _summon;
    /// <summary>A Trap being created: echoes of its recipe meet and the card appears, then goes to its place.</summary>
    internal bool ShowingSummon => _summon != null;
    internal Rect2? SummonTarget => _summon?.To;
    internal Vector2? SummonMeet => _summon?.Meet;
    internal int SummonEchoes => _summon?.Ingredients.Length ?? 0;
    internal int ActiveCount => _numbers.Count + _rings.Count + _projectiles.Count + (_flash < FlashSeconds ? 1 : 0) + (_card != null ? 1 : 0) + (_summon != null ? 1 : 0);
    internal Vector2? LastNumberAt => _numbers.Count == 0 ? null : _numbers[^1].At;

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

    internal bool AttackInFlightTo(RelikarioPortrait defender) => _projectiles.Exists(p => p.Defender == defender);

    internal void LaunchAttack(RelikarioPortrait attacker, RelikarioPortrait defender)
    {
        if (ReducedMotion || !attacker.IsVisibleInTree() || !defender.IsVisibleInTree() ||
            RelikarioArt.Load(attacker.RelikarioId, "attack-projectile") is not { } art) return;
        var lob = RelikarioArt.Family(attacker.RelikarioId) == RelikarioCatalog.Osamu.Id;
        // A directional sprite can preserve lettering that would be reversed by a horizontal flip.
        var leftArt = lob ? null : RelikarioArt.Load(attacker.RelikarioId, "attack-projectile-left");
        _projectiles.Add(new(attacker, defender, art, leftArt, lob));
        SetProcess(true); QueueRedraw();
    }

    internal void Flash(Color color)
    {
        if (ReducedMotion) return;
        _flash = 0; _flashColor = color;
        SetProcess(true); QueueRedraw();
    }

    /// <summary>Pops the played card's art at <paramref name="center"/>; skipped with reduced motion.</summary>
    internal void PlayedCard(Texture2D art, Vector2 center, Color accent)
    {
        if (ReducedMotion) return;
        _card = (art, center, accent, 0);
        SetProcess(true); QueueRedraw();
    }

    /// <summary>
    /// Echoes of the two exchanged cards rise from the memory they went to, meet over the exchange and
    /// call the Trap from its own deck; the Trap then flies to <paramref name="to"/>, or stays and fades
    /// when its place is chosen next (Zahira Flair). The real cards never move: they are only echoes.
    /// </summary>
    internal void TrapSummon(Texture2D[] ingredients, Vector2 from, Vector2 meet, Texture2D trap, Rect2? to, Color accent)
    {
        if (ReducedMotion) return;
        _summon = new Summon(ingredients, from, meet, trap, to, accent);
        SetProcess(true); QueueRedraw();
    }

    internal void Clear()
    {
        _numbers.Clear(); _rings.Clear(); _projectiles.Clear(); _flash = FlashSeconds; _card = null; _summon = null;
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
        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var projectile = _projectiles[i]; projectile.Age += step;
            if (projectile.Age >= projectile.Release + projectile.Travel) _projectiles.RemoveAt(i);
        }
        if (_flash < FlashSeconds) _flash = Math.Min(FlashSeconds, _flash + step);
        if (_card is { } card) _card = card.Age + step >= CardSeconds ? null : card with { Age = card.Age + step };
        if (_summon is { } summon) { summon.Age += step; if (summon.Age >= summon.Duration) _summon = null; }
        if (ActiveCount == 0) SetProcess(false);
        QueueRedraw();
    }

    private void DrawProjectile(Projectile projectile, Vector2 origin)
    {
        var (attacker, defender, rightArt, leftArt, lob) = projectile;
        if (projectile.Age < projectile.Release || !attacker.IsVisibleInTree() || !defender.IsVisibleInTree()) return;
        var (from, portraitWidth) = attacker.AttackLaunch(lob);
        var to = defender.AttackTarget;
        var path = to - from;
        var direction = path.X < 0 ? -1 : 1;
        var art = direction < 0 ? leftArt ?? rightArt : rightArt;
        var t = (float)((projectile.Age - projectile.Release) / projectile.Travel);
        var flight = lob ? t : t * (0.65f + 0.35f * t);
        var at = from.Lerp(to, flight) - origin;
        var size = art.GetSize() * (portraitWidth * (lob ? .46f : .39f) / Math.Max(art.GetWidth(), art.GetHeight()));
        var rotation = Mathf.Atan2(path.Y, Math.Abs(path.X)) * direction;
        if (lob)
        {
            at.Y -= Math.Min(path.Length() * .23f, size.Y * 1.3f) * 4 * t * (1 - t);
            rotation = Mathf.Lerp(-.3f, .5f, t) * direction;
        }
        var pivot = new Vector2(.5f, lob ? .74f : .5f);
        var flip = direction < 0 && leftArt == null && !lob;
        DrawSetTransform(at, rotation, new(flip ? -1 : 1, 1));
        DrawTextureRect(art, new Rect2(-size * pivot, size), false, new Color(1, 1, 1, Math.Min(1, (1 - t) * 9)));
        DrawSetTransform(Vector2.Zero);
    }

    private void DrawSummon(Summon s, Vector2 origin)
    {
        var age = (float)s.Age;
        var meet = s.Meet - origin;
        var echo = new Vector2(92, 128);
        // Gather: the recipe's echoes travel from memory to either side of the meeting point, then close in.
        if (age < SummonGather + SummonPop)
        {
            var travel = Mathf.SmoothStep(0, 1, Math.Min(1, age / (float)SummonGather));
            var close = Math.Clamp((age - (float)SummonGather) / (float)SummonPop, 0, 1);
            var alpha = 0.6f * (1 - close);
            for (var i = 0; i < s.Ingredients.Length; i++)
            {
                var side = s.Ingredients.Length == 1 ? 0 : i == 0 ? -1 : 1;
                var target = meet + new Vector2(side * 58 * (1 - close), 0);
                var at = (s.From - origin).Lerp(target, travel);
                var rect = new Rect2(at - echo * (1 - 0.4f * close) / 2, echo * (1 - 0.4f * close));
                DrawLine(at, meet, BoardArt.Tint(s.Accent, 0.5f * alpha * travel), 3, true);
                RelickVisuals.Shadow(this, rect, BoardArt.Tint(s.Accent, 0.45f * alpha), 12, 8);
                DrawTextureRect(s.Ingredients[i], rect, false, new Color(1, 1, 1, alpha));
            }
        }
        if (age < SummonGather) return;
        // Appear, then fly to the Trap's place or hold and fade.
        var pop = Math.Clamp((age - (float)SummonGather) / (float)SummonPop, 0, 1);
        var scale = pop < 0.6f ? Mathf.Lerp(0.55f, 1.06f, pop / 0.6f) : Mathf.Lerp(1.06f, 1, (pop - 0.6f) / 0.4f);
        var height = 210f * scale;
        var center = meet;
        var hold = s.To != null ? SummonFly : 0.35;
        var fly = Math.Clamp((age - (float)(SummonGather + SummonPop)) / (float)hold, 0, 1);
        if (s.To is { } to && fly > 0)
        {
            var eased = Mathf.SmoothStep(0, 1, fly);
            center = meet.Lerp(to.GetCenter() - origin, eased);
            height = Mathf.Lerp(210, Math.Max(80, to.Size.Y), eased);
        }
        var fade = Math.Clamp((age - (float)(SummonGather + SummonPop + hold)) / (float)SummonFade, 0, 1);
        var alphaCard = Math.Min(1, pop * 3) * (1 - fade);
        var size = new Vector2(height * s.Trap.GetWidth() / s.Trap.GetHeight(), height);
        var trapRect = new Rect2(center - size / 2, size);
        if (pop < 1) RelickVisuals.Shadow(this, trapRect.Grow(10 * (1 - pop)), BoardArt.Tint(Colors.White, 0.5f * (1 - pop)), 24, 12);
        RelickVisuals.Shadow(this, trapRect.Grow(-4), BoardArt.Tint(s.Accent, 0.6f * alphaCard), 20, 10);
        DrawTextureRect(s.Trap, trapRect, false, new Color(1, 1, 1, alphaCard));
        BoardArt.Outline(this, trapRect, BoardArt.Tint(s.Accent, alphaCard), 8, 2);
    }

    public override void _Draw()
    {
        var origin = GlobalPosition;
        if (_flash < FlashSeconds)
            DrawRect(new Rect2(Vector2.Zero, Size), BoardArt.Tint(_flashColor, 0.20f * (1 - (float)(_flash / FlashSeconds))));
        if (_summon is { } s) DrawSummon(s, origin);
        if (_card is { } played)
        {
            // Pop in, hold, then rise slightly while fading: long enough to recognise, short enough not to wait for.
            var t = (float)(played.Age / CardSeconds);
            var pop = t < 0.18f ? Mathf.Lerp(0.82f, 1.04f, t / 0.18f) : t < 0.3f ? Mathf.Lerp(1.04f, 1, (t - 0.18f) / 0.12f) : 1;
            var alpha = t < 0.12f ? t / 0.12f : t > 0.7f ? 1 - (t - 0.7f) / 0.3f : 1;
            var height = 270 * pop;
            var size = new Vector2(height * played.Art.GetWidth() / played.Art.GetHeight(), height);
            var rect = new Rect2(played.Center - origin - size / 2 - new Vector2(0, t > 0.7f ? 18 * (t - 0.7f) / 0.3f : 0), size);
            RelickVisuals.Shadow(this, rect.Grow(-4), BoardArt.Tint(played.Accent, 0.55f * alpha), 22, 10);
            DrawTextureRect(played.Art, rect, false, new Color(1, 1, 1, alpha));
            BoardArt.Outline(this, rect, BoardArt.Tint(played.Accent, alpha), 8, 2);
        }
        foreach (var projectile in _projectiles) DrawProjectile(projectile, origin);
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
