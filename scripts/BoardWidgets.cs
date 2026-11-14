using Godot;
using Relick.Core;

namespace Relick;

public enum GameIcon { Sword, Bolt, Heart, Cards, Memory, Gem, Shield, Exchange, Gift, Reload, Flag, Speaker, SpeakerMuted, Gear, Hand, Skip, WhiteFlag, Alert, Hourglass, Check, Pencil, Copy, Trash, Bench }

/// <summary>Shared vector symbols: readable at card, counter and toolbar sizes.</summary>
public static class BoardArt
{
    public static readonly Color Ink = new("f2f4fb");
    public static readonly Color Muted = new("8f9dbd");
    public static readonly Color Gold = new("ffc86b");
    public static Color CardColor(CardDefinition? card) => card?.Kind switch
    {
        CardKind.Attack => new("ff6f8e"), CardKind.Skill => new("4fe3b8"),
        CardKind.Attachment => new("b5a2fa"), CardKind.Equipment => new("7cc8ff"),
        CardKind.Code when card.CodeType == CodeType.Trap => new("ff6f8e"), _ => Gold
    };
    public static GameIcon CardIcon(CardDefinition? card) => card?.Kind switch
    {
        CardKind.Attack => GameIcon.Sword, CardKind.Skill => GameIcon.Bolt,
        CardKind.Attachment => GameIcon.Flag, CardKind.Equipment => GameIcon.Shield,
        CardKind.Code => (card.Heal > 0 || card.HealFromDie) ? GameIcon.Heart : GameIcon.Reload, _ => GameIcon.Cards
    };
    public static string CardType(CardDefinition card) => card.Kind switch
    {
        CardKind.Attack when card.CodeType == CodeType.Trap => "CÓDIGO TRAMPA · ATAQUE",
        CardKind.Attack => "ATAQUE", CardKind.Skill => "HABILIDAD",
        CardKind.Code => card.CodeType switch
        {
            CodeType.Play => "CÓDIGO · JUGADA", CodeType.Object => "CÓDIGO · OBJETO",
            CodeType.Character => "CÓDIGO · PERSONAJE", CodeType.Trap => "CÓDIGO TRAMPA", _ => "CÓDIGO"
        },
        CardKind.Attachment => "SOBRE EL RELIKARIO", _ => "EQUIPO"
    };
    public static Color Tint(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    internal static Texture2D? LoadCardArtwork(CardDefinition card)
    {
        var path = $"res://assets/cards/{card.Id}-clean.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    internal static Rect2 FitArtwork(Texture2D artwork, Rect2 area) => FitArtwork(artwork.GetSize(), area);

    internal static Rect2 FitArtwork(Vector2 size, Rect2 area)
    {
        var extent = size * Math.Min(area.Size.X / size.X, area.Size.Y / size.Y);
        return new Rect2(area.GetCenter() - extent / 2, extent);
    }

    private static Vector2[] Octagon(Rect2 rect, int radius)
    {
        var cut = Math.Min(Math.Min(radius, 8), Math.Min(rect.Size.X, rect.Size.Y) / 2);
        var x = rect.Position.X; var y = rect.Position.Y; var r = rect.End.X; var b = rect.End.Y;
        return [new(x + cut, y), new(r - cut, y), new(r, y + cut), new(r, b - cut),
            new(r - cut, b), new(x + cut, b), new(x, b - cut), new(x, y + cut)];
    }

    public static void Plate(CanvasItem c, Rect2 rect, Color fill, Color edge, int radius = 12, int border = 1)
    {
        if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
        var points = Octagon(rect, radius);
        if (fill.A > 0)
        {
            // Lit from above: every plate carries the same quiet gradient as the docks.
            var top = fill.Lightened(0.07f); var bottom = fill.Darkened(0.16f);
            top.A = bottom.A = fill.A;
            c.DrawPolygon(points, [top, top, top, bottom, bottom, bottom, bottom, top]);
        }
        if (border > 0 && edge.A > 0) c.DrawPolyline([.. points, points[0]], edge, border, true);
    }

    public static void Outline(CanvasItem c, Rect2 rect, Color edge, int radius = 12, int border = 1)
    {
        var points = Octagon(rect, radius);
        c.DrawPolyline([.. points, points[0]], edge, border, true);
    }

    /// <summary>Recessed socket for an empty zone: darker than the mat, with corner ticks instead of a full frame.</summary>
    public static void Socket(CanvasItem c, Rect2 rect, Color accent, float strength = 1)
    {
        var points = Octagon(rect, 8);
        var top = new Color(0, 0, 0, 0.34f * strength); var bottom = new Color(0, 0, 0, 0.16f * strength);
        c.DrawPolygon(points, [top, top, top, bottom, bottom, bottom, bottom, top]);
        c.DrawPolyline([.. points, points[0]], Tint(accent, 0.16f * strength), 1, true);
        var tick = Math.Min(14, Math.Min(rect.Size.X, rect.Size.Y) / 4);
        var color = Tint(accent, 0.55f * strength);
        foreach (var (corner, dx, dy) in new[] { (rect.Position, 1, 1), (new Vector2(rect.End.X, rect.Position.Y), -1, 1),
            (rect.End, -1, -1), (new Vector2(rect.Position.X, rect.End.Y), 1, -1) })
        {
            var p = corner + new Vector2(dx * 4, dy * 4);
            c.DrawPolyline([p + new Vector2(0, dy * tick), p + new Vector2(0, dy * 4), p + new Vector2(dx * 4, 0), p + new Vector2(dx * tick, 0)], color, 1.5f, true);
        }
    }

    public static void Title(CanvasItem c, string text, Vector2 pos, int size, Color color, bool centered = false)
    {
        var font = RelickVisuals.Display;
        if (centered) pos.X -= font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X / 2;
        c.DrawString(font, pos + new Vector2(0, 1.5f), text, HorizontalAlignment.Left, -1, size, new Color(0, 0, 0, 0.45f * color.A));
        c.DrawString(font, pos, text, HorizontalAlignment.Left, -1, size, color);
    }

    public static void Text(CanvasItem c, string text, Vector2 pos, int size, Color color, float width = -1)
        => c.DrawString(ThemeDB.FallbackFont, pos, text, HorizontalAlignment.Left, width, size, color);
    public static void CenterText(CanvasItem c, string text, Vector2 pos, int size, Color color)
    {
        var width = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        Text(c, text, pos - new Vector2(width / 2, 0), size, color);
    }

    internal static void CardBadge(CanvasItem canvas, Vector2 size, string text)
    {
        const int fontSize = 9;
        var width = Mathf.Ceil(ThemeDB.FallbackFont.GetStringSize(text, fontSize: fontSize).X) + 12;
        var badge = new Rect2(size.X - width - 4, size.Y - 22, width, 18);
        RelickVisuals.Shadow(canvas, badge, new Color(0, 0, 0, 0.5f), 4, 3);
        Plate(canvas, badge, new Color("0b1020"), Tint(Gold, 0.8f), 4);
        CenterText(canvas, text, new(badge.GetCenter().X, badge.Position.Y + 12), fontSize, Gold);
    }

    public static void Icon(CanvasItem c, GameIcon icon, Vector2 center, float size, Color color, float canvasScale = 1)
    {
        c.DrawSetTransform(center * canvasScale, 0, Vector2.One * (size / 32 * canvasScale));
        void Shape(params Vector2[] points) => c.DrawColoredPolygon(points, color);
        void Block(float x, float y, float w, float h) => c.DrawRect(new(x, y, w, h), color);
        void Stroke(params Vector2[] points) => c.DrawPolyline(points, color, 3, false);
        switch (icon)
        {
            case GameIcon.Sword:
                Shape(new(-4, 1), new(7, -14), new(13, -14), new(13, -8), new(2, 6));
                Stroke(new(-8, -2), new(4, 10)); Stroke(new(-2, 5), new(-11, 14));
                break;
            case GameIcon.Bolt:
                Shape(new(0, -14), new(-11, 3), new(-2, 3), new(-5, 14), new(12, -5), new(3, -5), new(7, -14));
                break;
            case GameIcon.Heart:
                Shape(new(-14, -8), new(-9, -13), new(-4, -13), new(0, -8), new(4, -13), new(9, -13), new(14, -8),
                    new(14, -2), new(10, 4), new(0, 14), new(-10, 4), new(-14, -2));
                break;
            case GameIcon.Cards:
                Stroke(new(-10, 9), new(-13, 9), new(-13, -13), new(5, -13));
                Shape(new(-7, -8), new(8, -8), new(12, -4), new(12, 14), new(-7, 14));
                break;
            case GameIcon.Memory:
                Stroke(new(-12, 13), new(-12, -13), new(8, -13), new(13, -8), new(13, 13), new(-12, 13));
                Block(-6, -12, 11, 9); Block(-6, 3, 13, 10);
                break;
            case GameIcon.Gem:
                Shape(new(-13, -5), new(-6, -13), new(6, -13), new(13, -5), new(0, 14));
                c.DrawPolyline(new Vector2[] { new(-8, -5), new(0, 9), new(8, -5) }, RelickVisuals.Playmat.Surface, 2);
                break;
            case GameIcon.Shield:
                Shape(new(-12, -13), new(12, -13), new(12, 2), new(7, 9), new(0, 14), new(-7, 9), new(-12, 2));
                c.DrawLine(new(0, -8), new(0, 7), RelickVisuals.Playmat.Surface, 3);
                break;
            case GameIcon.Exchange:
                Shape(new(-13, -9), new(5, -9), new(5, -14), new(14, -6), new(5, 2), new(5, -3), new(-13, -3));
                Shape(new(13, 9), new(-5, 9), new(-5, 14), new(-14, 6), new(-5, -2), new(-5, 3), new(13, 3));
                break;
            case GameIcon.Gift:
                Block(-12, -4, 24, 5); Block(-10, 4, 8, 10); Block(2, 4, 8, 10);
                Stroke(new(0, -5), new(-9, -12), new(-9, -5), new(9, -5), new(9, -12), new(0, -5));
                break;
            case GameIcon.Reload:
                Stroke(new(11, -5), new(5, -12), new(-6, -12), new(-12, -5), new(-12, 6), new(-5, 13), new(6, 13), new(12, 6));
                Shape(new(4, -4), new(14, -4), new(14, -14));
                break;
            case GameIcon.Flag:
                Block(-12, -14, 4, 28);
                Shape(new(-6, -13), new(13, -13), new(7, -5), new(13, 3), new(-6, 3));
                break;
            case GameIcon.Speaker:
                Stroke(new(-12, -4), new(-7, -4), new(-1, -10), new(-1, 10), new(-7, 4), new(-12, 4), new(-12, -4));
                c.DrawArc(new(-1, 0), 7, -Mathf.Pi / 4, Mathf.Pi / 4, 12, color, 2.5f, true);
                c.DrawArc(new(-1, 0), 13, -Mathf.Pi / 4, Mathf.Pi / 4, 16, color, 2.5f, true);
                break;
            case GameIcon.Gear:
                // Settings cog: a ring with eight teeth, hollow at the hub.
                c.DrawArc(Vector2.Zero, 7.5f, 0, Mathf.Tau, 40, color, 5.5f, true);
                for (var tooth = 0; tooth < 8; tooth++)
                {
                    var angle = tooth * Mathf.Tau / 8;
                    Shape(Vector2.FromAngle(angle - 0.24f) * 9.5f, Vector2.FromAngle(angle - 0.15f) * 14,
                        Vector2.FromAngle(angle + 0.15f) * 14, Vector2.FromAngle(angle + 0.24f) * 9.5f);
                }
                break;
            case GameIcon.SpeakerMuted:
                Stroke(new(-5, -6), new(-1, -10), new(-1, -5));
                Stroke(new(-1, 6), new(-1, 10), new(-7, 4), new(-12, 4), new(-12, -4), new(-9, -4));
                c.DrawLine(new(-13, -12), new(11, 12), color, 3, true);
                break;
            case GameIcon.Hand:
                // A raised open hand: four fingers over the palm, thumb to the side.
                Block(-10, -9, 4.5f, 12); Block(-4.5f, -13, 4.5f, 16); Block(1, -12, 4.5f, 15); Block(6.5f, -8, 4.5f, 11);
                Shape(new(-10, 1), new(11, 1), new(11, 6), new(5, 14), new(-6, 14), new(-10, 8));
                Shape(new(-10, 3), new(-15, -3), new(-12, -6), new(-7, 0));
                break;
            case GameIcon.Skip:
                Shape(new(-14, -11), new(-2, 0), new(-14, 11));
                Shape(new(-3, -11), new(9, 0), new(-3, 11));
                Block(10, -11, 4, 22);
                break;
            case GameIcon.WhiteFlag:
                // Surrender flag: a pole with a waving cloth.
                Block(-12, -14, 3.5f, 29);
                Shape(new(-8, -13), new(-2, -11), new(4, -13), new(9, -14), new(14, -12), new(14, 2),
                    new(9, 0), new(4, 1), new(-2, 3), new(-8, 1));
                break;
            case GameIcon.Alert:
                // Warning sign: a solid triangle with a cut-out exclamation mark.
                Shape(new(0, -14), new(15, 13), new(-15, 13));
                c.DrawRect(new(-2, -5, 4, 10), RelickVisuals.Playmat.Surface);
                c.DrawRect(new(-2, 7, 4, 3.5f), RelickVisuals.Playmat.Surface);
                break;
            case GameIcon.Check:
                c.DrawPolyline(new Vector2[] { new(-12, 1), new(-4, 10), new(13, -9) }, color, 5, true);
                break;
            case GameIcon.Pencil:
                // Edit: a slanted pencil with its tip at the lower left.
                Shape(new(6, -14), new(14, -6), new(-5, 13), new(-13, 5));
                Shape(new(-13, 7), new(-7, 13), new(-15, 15));
                break;
            case GameIcon.Copy:
                // Duplicate: a sheet behind another.
                Stroke(new(-6, -8), new(-6, -14), new(13, -14), new(13, 6), new(7, 6));
                Shape(new(-13, -7), new(5, -7), new(5, 14), new(-13, 14));
                break;
            case GameIcon.Trash:
                Block(-13, -11, 26, 3); Block(-4, -15, 8, 4);
                Shape(new(-10, -6), new(10, -6), new(8, 14), new(-8, 14));
                break;
            case GameIcon.Bench:
                // Side deck: a bench seen from the front.
                Block(-14, -6, 28, 5); Block(-14, 2, 28, 4);
                Block(-12, 6, 3, 8); Block(9, 6, 3, 8);
                break;
            case GameIcon.Hourglass:
                // Waiting: two glass bulbs between a top and a bottom bar.
                Block(-11, -14, 22, 3); Block(-11, 11, 22, 3);
                Shape(new(-8, -11), new(8, -11), new(1.5f, 0), new(-1.5f, 0));
                Shape(new(-1.5f, 0), new(1.5f, 0), new(8, 11), new(-8, 11));
                break;
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One * canvasScale);
    }
}

public partial class ArenaBackground : Control
{
    private ShaderMaterial? _material;
    private ColorRect? _mat;
    private float _seam = -1, _boardRight = 1, _active;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/arena_playmat.gdshader") };
        _mat = new ColorRect { Name = "PlaymatSurface", Material = _material, MouseFilter = MouseFilterEnum.Ignore, Color = RelickVisuals.Playmat.Background };
        AddChild(_mat); _mat.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += Apply;
        Apply();
    }

    /// <summary>Places the glowing divider between both fields; a negative seam draws a neutral mat.</summary>
    internal void Present(float seam, float boardRight, int activeSide)
    {
        if (Mathf.IsEqualApprox(seam, _seam) && Mathf.IsEqualApprox(boardRight, _boardRight) && Mathf.IsEqualApprox(activeSide, _active)) return;
        _seam = seam; _boardRight = boardRight; _active = activeSide;
        Apply();
    }

    private void Apply()
    {
        if (_material == null) return;
        var mat = RelickVisuals.Playmat;
        _material.SetShaderParameter("size", Size);
        _material.SetShaderParameter("seam", _seam);
        _material.SetShaderParameter("board_right", _boardRight);
        _material.SetShaderParameter("active_side", _active);
        _material.SetShaderParameter("base_color", mat.Background);
        _material.SetShaderParameter("trace_color", mat.Trace);
        _material.SetShaderParameter("own_color", RelickVisuals.State.Own);
        _material.SetShaderParameter("rival_color", RelickVisuals.State.Rival);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var mat = RelickVisuals.Playmat;
        if (mat.Artwork != null) DrawTextureRect(mat.Artwork, new Rect2(Vector2.Zero, Size), false);
    }
}

public partial class DexterityBadge : Control
{
    public int Amount { get; init; }
    public Color Accent { get; init; }
    public override void _Ready()
    {
        CustomMinimumSize = new(32, 32);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Pass;
        AccessibilityName = $"Destreza · {Amount}";
        TooltipText = $"Destreza · {Amount}\nGasta una ficha para sumar +1 a uno de tus dados. No se puede usar con 1 ni para alcanzar 6.";
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var center = Size / 2;
        var radius = Math.Min(Size.X, Size.Y) / 2 - 2;
        var points = Enumerable.Range(0, 7)
            .Select(index => center + Vector2.FromAngle(-Mathf.Pi / 2 + index * Mathf.Tau / 7) * radius).ToArray();
        var edge = Amount > 0 ? Accent : BoardArt.Tint(BoardArt.Muted, 0.55f);
        if (Amount > 0) RelickVisuals.Shadow(this, new Rect2(center - Vector2.One * radius * 0.7f, Vector2.One * radius * 1.4f), BoardArt.Tint(Accent, 0.35f), 6, (int)radius);
        DrawColoredPolygon(points, new Color("0b1020"));
        DrawColoredPolygon(points, BoardArt.Tint(Accent, Amount > 0 ? 0.22f : 0.05f));
        DrawPolyline([.. points, points[0]], edge, 2, true);
        var value = Amount.ToString();
        var font = ThemeDB.FallbackFont;
        var fontSize = 17;
        while (fontSize > 9 && font.GetStringSize(value, fontSize: fontSize).X > radius * 1.4f) fontSize--;
        var baseline = center.Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2;
        BoardArt.CenterText(this, value, new(center.X, baseline), fontSize, Amount > 0 ? BoardArt.Ink : BoardArt.Muted);
    }
}

public partial class ResourceBadge : Control
{
    public int Amount { get; set; }
    /// <summary>Set before the badge enters the tree to turn the counter into a button.</summary>
    public Action? Activated { get; set; }
    public bool Interactive => Activated != null;
    private bool _hover, _held;

    public override void _Ready()
    {
        CustomMinimumSize = new(94, 30);
        Resized += QueueRedraw;
        if (Activated == null)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            AccessibilityName = $"Relick Points · {Amount}";
            return;
        }
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.All;
        AccessibilityName = $"Relick Points · {Amount}. Efectos de Relikia";
        TooltipText = $"Tienes {Amount} PR. Pulsa para ver los efectos de Relikia.";
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; _held = false; QueueRedraw(); };
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Activated == null) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed)
            {
                _held = true; GrabFocus(); QueueRedraw(); AcceptEvent();
            }
            else
            {
                var activate = _held && new Rect2(Vector2.Zero, Size).HasPoint(mouse.Position);
                _held = false; QueueRedraw(); AcceptEvent();
                if (activate) Activated();
            }
            return;
        }
        if (@event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is Key.Enter or Key.Space)
        {
            Activated(); AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var active = Interactive && (_hover || _held || HasFocus());
        RelickVisuals.Shadow(this, rect.Grow(-3), BoardArt.Tint(BoardArt.Gold, active ? 0.38f : Amount > 0 ? 0.22f : 0.06f), active ? 10 : 6);
        BoardArt.Plate(this, rect, active ? new Color("242536") : new Color("191a26"),
            BoardArt.Tint(BoardArt.Gold, Amount > 0 ? active ? 1f : 0.85f : active ? 0.8f : 0.4f), 8, active ? 2 : 1);
        BoardArt.Icon(this, GameIcon.Gem, new(15, Size.Y / 2), 13, BoardArt.Gold);
        var text = $"{Amount} PR";
        var font = RelickVisuals.Strong;
        var fontSize = 18;
        var available = Size.X - (Interactive ? 44 : 34);
        while (fontSize > 9 && font.GetStringSize(text, fontSize: fontSize).X > available) fontSize--;
        var width = font.GetStringSize(text, fontSize: fontSize).X;
        var center = Interactive ? Size.X / 2 + 3 : (Size.X + 22) / 2;
        var baseline = Size.Y / 2 + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2;
        DrawString(font, new(center - width / 2, baseline), text, HorizontalAlignment.Left, -1, fontSize, BoardArt.Gold);
        if (!Interactive) return;
        var x = Size.X - 12; var y = Size.Y / 2;
        DrawPolyline([new(x - 4, y - 2), new(x, y + 2), new(x + 4, y - 2)],
            BoardArt.Tint(BoardArt.Gold, active ? 1f : 0.65f), 1.6f, true);
    }
}

public partial class LifeMeter : Control
{
    public int Health { get; private set; }
    public int Maximum { get; private set; } = 1;
    public float DisplayedRatio { get; private set; }
    public bool IsAnimating => _elapsed < _duration;
    public Color FillColor => ColorForRatio(DisplayedRatio);
    /// <summary>Board heroes stack the count over the bar to fit beside the portrait.</summary>
    public bool Stacked
    {
        get => _stacked;
        set { _stacked = value; CustomMinimumSize = value ? new(104, 56) : new(190, 44); QueueRedraw(); }
    }
    private bool _stacked;
    /// <summary>
    /// The player already fell to 0 once. Relick then ends the duel on the next damage at 0, and a healed
    /// player who drops back to 0 loses as well; both are decisive, so the meter says so in words.
    /// </summary>
    public bool ReachedZero
    {
        get => _reachedZero;
        set { if (_reachedZero == value) return; _reachedZero = value; Describe(); QueueRedraw(); }
    }
    private bool _reachedZero;
    /// <summary>At 0 lives the next damage ends the duel; the drained bar becomes the warning.</summary>
    public bool FinalBlow => Health == 0 && _initialized;
    internal const string FinalBlowCaption = "GOLPE FINAL";
    private bool _initialized;
    private float _fromRatio;
    private float _targetRatio;
    private double _elapsed;
    private double _duration;

    public static Color ColorForRatio(float ratio) => ratio > 0.5f ? new("4fe39a") : ratio > 0.2f ? new("ffc857") : RelickVisuals.State.Danger;

    public void SetHealth(int health, int maximum, bool animate = true)
    {
        maximum = Math.Max(1, maximum);
        health = Math.Clamp(health, 0, maximum);
        if (_initialized && health == Health && maximum == Maximum && (animate || !IsAnimating)) return;
        Health = health;
        Maximum = maximum;
        Describe();
        _targetRatio = (float)Health / Maximum;
        _fromRatio = DisplayedRatio;
        _elapsed = 0;
        _duration = animate && _initialized ? 0.25 + Math.Abs(_targetRatio - _fromRatio) * 0.65 : 0;
        _initialized = true;
        if (_duration == 0) DisplayedRatio = _targetRatio;
        QueueRedraw();
    }

    private void Describe() => TooltipText = $"{Health} de {Maximum} vidas" + (Health == 0
        ? "\nGolpe final: sigues en juego, pero el próximo daño termina el duelo."
        : _reachedZero ? "\nYa llegó a 0 vidas: si vuelve a 0, pierde el duelo." : "");

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        Resized += QueueRedraw;
    }

    public override void _Process(double delta) => AdvancePlayback(delta);

    public void AdvancePlayback(double delta)
    {
        if (!IsAnimating) return;
        _elapsed = Math.Min(_duration, _elapsed + Math.Max(0, delta));
        var t = (float)(_elapsed / _duration);
        var eased = t * t * (3 - 2 * t);
        DisplayedRatio = Mathf.Lerp(_fromRatio, _targetRatio, eased);
        if (!IsAnimating) DisplayedRatio = _targetRatio;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var color = FillColor;
        var danger = Health > 0 && Health * 5 <= Maximum;
        RelickVisuals.Shadow(this, new Rect2(8, 14, 18, 18), BoardArt.Tint(color, danger ? 0.55f : 0.32f), 9, 9);
        BoardArt.Icon(this, GameIcon.Heart, new(17, 23), 23, color);
        var value = Health.ToString();
        var max = $"/ {Maximum}";
        var fontSize = 32;
        var strong = RelickVisuals.Strong;
        float Width(string text, int size) => ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        float StrongWidth(string text, int size) => strong.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        var room = Stacked ? Size.X - 40 : Size.X * 0.43f - 36;
        while (fontSize > 12 && StrongWidth(value, fontSize) + Width(max, Math.Max(10, fontSize / 2)) + 6 > room) fontSize--;
        DrawString(strong, new(36, 34), value, HorizontalAlignment.Left, -1, fontSize, BoardArt.Ink);
        var maxStart = 36 + StrongWidth(value, fontSize) + 6;
        BoardArt.Text(this, max, new(maxStart, 33), Math.Max(10, fontSize / 2), BoardArt.Muted);
        var start = Stacked ? 0 : Math.Max(112, maxStart + Width(max, Math.Max(10, fontSize / 2)) + 14);
        if (!Stacked) BoardArt.Text(this, Health == 0 ? FinalBlowCaption : "VIDAS", new(start, 15), 9, color);
        var track = Stacked ? new Rect2(0, 40, Math.Max(1, Size.X), 16) : new Rect2(start, 22, Math.Max(1, Size.X - start), 16);
        var alarm = RelickVisuals.State.Danger;
        // Having fallen to 0 once, the bar keeps a red edge: dropping to 0 again ends the duel.
        BoardArt.Plate(this, track, new Color("060911"), _reachedZero && Health > 0 ? BoardArt.Tint(alarm, 0.85f) : new Color("3d4a6b"), 8, _reachedZero && Health > 0 ? 2 : 1);
        if (Stacked && Health == 0 && !IsAnimating)
        {
            // The board stacks the count over the bar and has no caption line: the empty bar carries the warning.
            BoardArt.Plate(this, track, BoardArt.Tint(alarm, 0.28f), alarm, 6, 2);
            var font = RelickVisuals.Strong;
            var size = 12;
            while (size > 9 && font.GetStringSize(FinalBlowCaption, fontSize: size).X > track.Size.X - 10) size--;
            var width = font.GetStringSize(FinalBlowCaption, fontSize: size).X;
            var baseline = track.GetCenter().Y + (font.GetAscent(size) - font.GetDescent(size)) / 2;
            DrawString(font, new(track.GetCenter().X - width / 2, baseline), FinalBlowCaption, HorizontalAlignment.Left, -1, size, BoardArt.Ink);
            return;
        }
        var inner = new Rect2(track.Position + new Vector2(3, 3), new Vector2(Math.Max(0, track.Size.X - 6), 10));
        // The lost stretch lingers in white while the bar drains: damage reads at a glance.
        if (IsAnimating && _fromRatio > DisplayedRatio)
            DrawRect(new Rect2(inner.Position.X + inner.Size.X * DisplayedRatio, inner.Position.Y, inner.Size.X * (_fromRatio - DisplayedRatio), inner.Size.Y),
                BoardArt.Tint(BoardArt.Ink, 0.85f));
        var fill = new Rect2(inner.Position, new Vector2(inner.Size.X * DisplayedRatio, inner.Size.Y));
        if (fill.Size.X > 0)
        {
            RelickVisuals.Shadow(this, fill, BoardArt.Tint(color, 0.35f), 5, 3);
            var top = color.Lightened(0.28f); var bottom = color.Darkened(0.22f);
            DrawPolygon([fill.Position, new(fill.End.X, fill.Position.Y), fill.End, new(fill.Position.X, fill.End.Y)], [top, top, bottom, bottom]);
            if (fill.Size.X > 10) DrawLine(fill.Position + new Vector2(2, 2), fill.Position + new Vector2(fill.Size.X - 2, 2), BoardArt.Tint(Colors.White, 0.45f), 1, true);
        }
        // One notch per life keeps the continuous bar countable.
        if (Maximum is > 1 and <= 16)
            for (var i = 1; i < Maximum; i++)
            {
                var x = inner.Position.X + inner.Size.X * i / Maximum;
                DrawLine(new(x, inner.Position.Y - 1), new(x, inner.End.Y + 1), new Color("060911"), 2);
            }
    }
}

public partial class SkillTile : Button
{
    // The hand card canvas is 126 × 184; rotating it gives this exact footprint.
    public static readonly Vector2 CardSize = new(184, 126);
    internal const string LessonCaption = "ENSEÑAR AQUÍ";
    public CardDefinition? Skill { get; set; }
    public float PresentationScale { get; set; } = 1;
    public int Slot { get; set; }
    public int SkillIndex { get; set; } = -1;
    public bool Chosen { get; set; }
    public bool Targetable { get; set; }
    public int Counters { get; set; }
    /// <summary>The empty space the next skill would be taught in, lit only while teaching is authorized.</summary>
    public bool NextLesson { get; set; }
    public bool HasSkillArtwork => _skillArt != null;
    internal bool AwaitingLesson { get; private set; }
    private Texture2D? _skillArt;
    public Color Accent { get; set; }

    internal void SetLessonPending(bool pending)
    {
        AwaitingLesson = pending;
        QueueRedraw();
    }

    public override void _Ready()
    {
        CustomMinimumSize = CardSize * PresentationScale;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        _skillArt = SkillArtwork.Load(Skill);
        TooltipText = Skill == null ? $"Enseñar una habilidad · {GameEngine.SkillSlotCosts[Slot]} PR." : Skill.Text + (Skill.Id == CardCatalog.Destiny.Id ? $"\nEsta copia: {Counters}/2 contadores." : Skill.DamagePerReroll > 0 ? $"\nEsta copia: {Counters} aumentos · {Skill.NormalDamageAt(Counters)} normal / {Skill.CriticalDamageAt(Counters)} crítico." : "");
    }
    public override void _Draw()
    {
        var size = Size / PresentationScale;
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);
        var bounds = new Rect2(0, 0, size.X, size.Y);
        if (Skill == null || AwaitingLesson)
        {
            // An empty space only says its cost, and quietly: occupied cards, lives and decisions come first.
            // The space a skill can be taught in right now is lit in its owner's colour instead.
            var invite = NextLesson || AwaitingLesson;
            BoardArt.Socket(this, bounds, Accent, invite ? 1.6f : 0.8f);
            var cost = $"{GameEngine.SkillSlotCosts[Slot]} PR";
            var costSize = invite ? 24 : 18;
            var baseline = size.Y / 2 + (RelickVisuals.Display.GetAscent(costSize) - RelickVisuals.Display.GetDescent(costSize)) / 2;
            if (NextLesson && !AwaitingLesson)
            {
                baseline += 8;
                BoardArt.Title(this, LessonCaption, new(size.X / 2, baseline - costSize - 4), 13, BoardArt.Tint(Accent, 0.9f), centered: true);
            }
            BoardArt.Title(this, cost, new(size.X / 2, baseline), costSize, invite ? BoardArt.Tint(Accent, 0.95f) : BoardArt.Tint(BoardArt.Gold, 0.36f), centered: true);
        }
        else if (_skillArt != null)
        {
            var fit = Math.Min((size.X - 4) / _skillArt.GetWidth(), (size.Y - 4) / _skillArt.GetHeight());
            var artSize = _skillArt.GetSize() * fit;
            var art = new Rect2((size - artSize) / 2, artSize);
            RelickVisuals.Shadow(this, new Rect2(art.Position + new Vector2(0, 3), art.Size).Grow(-2), new Color(0, 0, 0, 0.55f), 9);
            DrawTextureRect(_skillArt, art, false);

            if (Skill.Id == CardCatalog.Destiny.Id)
            {
                BoardArt.Plate(this, new Rect2(8, size.Y - 24, 114, 20), RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame, 4);
                BoardArt.Text(this, $"CONTADORES {Counters}/2", new(14, size.Y - 10), 10, Counters >= 2 ? new Color("ef7890") : BoardArt.Ink);
            }
            else if (Skill.DamagePerReroll > 0)
            {
                BoardArt.Plate(this, new Rect2(8, size.Y - 24, 156, 20), RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame, 4);
                BoardArt.Text(this, $"NORMAL {Skill.NormalDamageAt(Counters)} · CRÍTICO {Skill.CriticalDamageAt(Counters)}", new(14, size.Y - 10), 10, BoardArt.Ink);
            }
        }
        else
        {
            BoardArt.Plate(this, bounds, RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 9);
            DrawLine(new(14, 2), new(size.X - 14, 2), Accent, 3, true);
            BoardArt.Text(this, "HABILIDAD", new(12, 20), 9, Accent);
            BoardArt.CenterText(this, Skill.Name, new(size.X / 2, 47), 19, BoardArt.Ink);
            BoardArt.Icon(this, GameIcon.Bolt, new(34, 83), 32, Accent, PresentationScale);
            BoardArt.Icon(this, GameIcon.Sword, new(65, 76), 14, BoardArt.Muted, PresentationScale);
            BoardArt.Text(this, $"{(Skill.Id == CardCatalog.Magnet.Id ? "1D6" : Skill.NormalDamage.ToString())} daño", new(78, 81), 14, BoardArt.Ink);
            BoardArt.Text(this, $"{Skill.CriticalDamage} crítico", new(60, 105), 12, BoardArt.Gold);
            BoardArt.Icon(this, GameIcon.Gem, new(size.X - 52, 102), 12, BoardArt.Gold, PresentationScale);
            BoardArt.Text(this, Skill.Reward.ToString(), new(size.X - 42, 107), 12, BoardArt.Gold);
        }
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, size), Chosen, Targetable, !Disabled && IsHovered(), HasFocus());
    }
}


public partial class ExchangeTrack : VBoxContainer
{
    public bool Compact { get; set; }
    public string OwnerCaption { get; set; } = "";
    public IReadOnlyList<CardDefinition> ExchangeCards { get; set; } = Array.Empty<CardDefinition>();
    public int Filled => ExchangeCards.Count;
    public CardDefinition? BonusCard { get; set; }
    public bool Preparing { get; set; }
    public bool BonusEffectUsed { get; set; }
    public IReadOnlyCollection<int> ExchangeEffectsUsed { get; set; } = Array.Empty<int>();
    public Color Accent { get; set; }
    public ZoneCardButton BonusButton { get; private set; } = null!;
    public ZoneCardButton[] ExchangeButtons { get; private set; } = null!;
    /// <summary>Board rows give the track a fixed footprint instead of the centre column's width.</summary>
    public float TrackWidth { get; init; }
    /// <summary>The rival's lane reads from the outside in: Bonus first, toward their field's edge.</summary>
    public bool Mirrored { get; init; }
    public event Action? BonusInspected;
    public event Action<int>? ExchangeInspected;
    /// <summary>The desktop board places the zones itself; the track only owns and names them.</summary>
    public bool Detached { get; init; }
    private bool _built;

    public override void _Ready() => Build();

    /// <summary>Creates the zones. A detached track hands them to the board instead of laying them out.</summary>
    public void Build()
    {
        if (_built) return;
        _built = true;
        Node row;
        if (Detached)
        {
            MouseFilter = MouseFilterEnum.Ignore;
            row = new Node();
        }
        else
        {
            CustomMinimumSize = new(TrackWidth > 0 ? TrackWidth : Compact ? 252 : 280, 0);
            AddThemeConstantOverride("separation", 4);
            var label = new Label { Text = $"{OwnerCaption}  ·  {Filled}/2 EN CANJE", MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = Mirrored ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", Accent);
            AddChild(label);
            var box = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            box.AddThemeConstantOverride("separation", 6);
            AddChild(box);
            row = box;
        }
        ExchangeButtons = new ZoneCardButton[2];
        for (var slot = 0; slot < 2; slot++)
        {
            var index = slot;
            var button = new ZoneCardButton
            {
                Name = Detached ? $"{Name}ExchangeCard{slot}" : $"ExchangeCard{slot}", Caption = "CANJE", Accent = Accent, Compact = Compact,
                Layout = ZoneCardLayout.Landscape, EffectUsed = ExchangeEffectsUsed.Contains(slot), SizeFlagsStretchRatio = 1.25f,
                CustomMinimumSize = new(0, TrackWidth > 0 ? 86 : 0),
                Card = slot < ExchangeCards.Count ? ExchangeCards[slot] : null
            };
            button.Pressed += () => ExchangeInspected?.Invoke(index);
            ExchangeButtons[slot] = button;
            row.AddChild(button);
        }
        BonusButton = new ZoneCardButton
        {
            Name = Detached ? $"{Name}BonusCard" : "BonusCard", Caption = "BONUS", Card = Preparing ? null : BonusCard, Compact = Compact,
            Layout = ZoneCardLayout.Portrait, EffectUsed = BonusEffectUsed, Accent = BoardArt.Gold
        };
        BonusButton.Pressed += () => BonusInspected?.Invoke();
        row.AddChild(BonusButton);
        if (Mirrored) row.MoveChild(BonusButton, 0);
        if (!Detached) return;
        foreach (var zone in row.GetChildren()) row.RemoveChild(zone);
        row.Free();
    }
}

public enum ZoneCardLayout { Portrait, Landscape }

/// <summary>Inspectable card in a public board zone.</summary>
public partial class ZoneCardButton : Button
{
    public bool Targetable { get; set; }
    public bool Chosen { get; set; }
    public bool Compact { get; set; }
    public bool LargePreview { get; init; }
    /// <summary>Fixed column width for board rows; zero keeps the legacy expanding slot.</summary>
    public float SlotWidth { get; init; }
    public ZoneCardLayout Layout { get; init; }
    public bool EffectUsed { get; init; }
    public CardDefinition? Card { get; set; }
    public string Caption { get; set; } = "";
    public string StatusText { get; init; } = "";
    public Color Accent { get; set; }
    private Texture2D? _artwork;
    private Texture2D? _exchangePreview;
    private Texture2D? Artwork => _exchangePreview ?? _artwork;
    private string CardStatus => EffectUsed ? "USADA" : StatusText;
    private string? _previousEquipmentStatus;
    internal bool AwaitingEquipment { get; private set; }
    internal bool AwaitingExchange { get; private set; }
    internal bool HasExchangePreview => _exchangePreview != null;

    internal void SetEquipmentPending(bool pending, string? previousStatus)
    {
        AwaitingEquipment = pending;
        _previousEquipmentStatus = pending ? previousStatus : null;
        QueueRedraw();
    }

    internal void SetExchangePending(bool pending)
    {
        AwaitingExchange = pending;
        QueueRedraw();
    }

    internal void SetExchangePreview(string? id)
    {
        if (Card != null) return;
        _exchangePreview = id == null ? null : BoardArt.LoadCardArtwork(CardCatalog.Get(id));
        QueueRedraw();
    }

    internal Rect2 CardRect => ArtworkRect(Artwork);

    internal Rect2 ArtworkRect(Texture2D? artwork)
    {
        var area = new Rect2(4, 4, Size.X - 8, Size.Y - 8);
        if (artwork == null) return area;
        if (ArtworkRotation(artwork) == 0) return BoardArt.FitArtwork(artwork, area);
        var unrotated = BoardArt.FitArtwork(artwork, new Rect2(0, 0, area.Size.Y, area.Size.X));
        var extent = new Vector2(unrotated.Size.Y, unrotated.Size.X);
        return new Rect2(area.GetCenter() - extent / 2, extent);
    }

    private float ArtworkRotation(Texture2D artwork) => Layout switch
    {
        ZoneCardLayout.Landscape when artwork.GetWidth() < artwork.GetHeight() => Mathf.Pi / 2,
        ZoneCardLayout.Portrait when artwork.GetWidth() > artwork.GetHeight() => -Mathf.Pi / 2,
        _ => 0
    };

    public override void _Ready()
    {
        CustomMinimumSize = new(SlotWidth > 0 ? SlotWidth : Compact ? 80 : 88, Math.Max(CustomMinimumSize.Y, Compact && LargePreview ? 112 : 86));
        SizeFlagsHorizontal = SlotWidth > 0 ? SizeFlags.Fill : SizeFlags.ExpandFill;
        Disabled = Card == null;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (Card != null) _artwork = BoardArt.LoadCardArtwork(Card);
        if (Card != null)
        {
            var description = $"{Caption} · {Card.Name}" + (CardStatus.Length > 0 ? $" · {CardStatus}" : "");
            AccessibilityName = description;
            TooltipText = $"{description}\n{Card.Text}\nPulsa para ampliar y consultar el efecto.";
        }
        Resized += QueueRedraw;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var bounds = new Rect2(0, 0, Size.X, Size.Y);
        var showCard = !AwaitingExchange && (!AwaitingEquipment || _previousEquipmentStatus != null) && (Card != null || HasExchangePreview);
        BoardArt.Socket(this, bounds, Accent, AwaitingEquipment ? 1.6f : showCard ? 0.55f : 1);
        if (!showCard)
        {
            var caption = AwaitingEquipment ? "EQUIPO" : Caption;
            BoardArt.CenterText(this, caption, new(Size.X / 2, 15), 9, BoardArt.Tint(AwaitingEquipment ? Accent : BoardArt.Muted, AwaitingEquipment ? 1 : 0.8f));
            if (Size.Y > 60)
                BoardArt.Icon(this, Caption.StartsWith("BONUS") ? GameIcon.Gift : Caption.StartsWith("CANJE") ? GameIcon.Exchange :
                    Caption.StartsWith("ORO") ? GameIcon.Gem : Caption.StartsWith("ÁREA") ? GameIcon.Flag : GameIcon.Shield, Size / 2 + new Vector2(0, 5), 20, BoardArt.Tint(Accent, 0.16f));
        }
        else if (Artwork is { } artwork)
        {
            var rect = CardRect;
            var rotation = ArtworkRotation(artwork);
            RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 2), rect.Size).Grow(-2), new Color(0, 0, 0, 0.55f), 7);
            if (rotation != 0)
            {
                var origin = rotation > 0 ? new Vector2(rect.End.X, rect.Position.Y) : new Vector2(rect.Position.X, rect.End.Y);
                DrawSetTransform(origin, rotation);
                DrawTextureRect(artwork, new Rect2(0, 0, rect.Size.Y, rect.Size.X), false);
                DrawSetTransform(Vector2.Zero);
            }
            else DrawTextureRect(artwork, rect, false);
        }
        else if (Card != null)
            BoardArt.Icon(this, BoardArt.CardIcon(Card), Size / 2, 32, Accent);
        var status = AwaitingEquipment ? _previousEquipmentStatus : CardStatus;
        if (showCard && status is { Length: > 0 }) BoardArt.CardBadge(this, Size, status);
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), Chosen, Targetable, !Disabled && IsHovered(), HasFocus());
    }
}

public partial class AttachmentStack : Button
{
    public IReadOnlyList<string> Cards { get; init; } = [];
    /// <summary>Board rows widen the slot so the fanned copies keep the row's full height.</summary>
    public float SlotWidth { get; init; }
    /// <summary>On the desktop board the top card fills the zone; the count tells how many lie under it.</summary>
    public bool Board { get; init; }
    private int Amount => Cards.Count;
    private Texture2D[] _previews = [];
    internal Rect2 CardRect => _previews.Length == 0 ? new Rect2(Vector2.Zero, Size) : PreviewRect(_previews.Length - 1);

    private Rect2 PreviewRect(int index)
    {
        if (Board) return BoardArt.FitArtwork(_previews[index], new Rect2(4, 4, Size.X - 8, Size.Y - 8));
        const float offset = 14;
        var spread = (_previews.Length - 1) * offset;
        var area = new Rect2(4 + index * offset, 4, Size.X - 8 - spread, Size.Y - 8);
        return BoardArt.FitArtwork(_previews[index], area);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(SlotWidth > 0 ? SlotWidth : 92, 86);
        Disabled = Amount == 0;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        AccessibilityName = $"Sobre el Relikario: {Amount} {(Amount == 1 ? "carta" : "cartas")}";
        TooltipText = $"{AccessibilityName}. Sin límite. Pulsa para consultar todas las cartas y sus efectos.";
        _previews = Cards.TakeLast(3).Select(CardCatalog.Get).Select(BoardArt.LoadCardArtwork).OfType<Texture2D>().ToArray();
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        Resized += QueueRedraw;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var accent = new Color("b5a2fa");
        BoardArt.Socket(this, new Rect2(0, 0, Size.X, Size.Y), accent, Amount == 0 ? 1 : 0.55f);
        if (Amount == 0)
        {
            BoardArt.CenterText(this, "SOBRE RELIKARIO", new(Size.X / 2, 15), 8, BoardArt.Tint(BoardArt.Muted, 0.8f));
            if (Size.Y > 60) BoardArt.Icon(this, GameIcon.Flag, Size / 2 + new Vector2(0, 5), 20, BoardArt.Tint(accent, 0.16f));
        }
        else if (_previews.Length == 0)
            BoardArt.Icon(this, GameIcon.Cards, Size / 2, 32, BoardArt.Muted);
        for (var i = Board ? Math.Max(0, _previews.Length - 1) : 0; i < _previews.Length; i++)
        {
            var rect = PreviewRect(i);
            RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 2), rect.Size).Grow(-2), new Color(0, 0, 0, 0.5f), 6);
            DrawTextureRect(_previews[i], rect, false);
        }
        if (Amount > 1) BoardArt.CardBadge(this, Size, $"×{Amount}");
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), false, false, !Disabled && IsHovered(), HasFocus());
    }
}

/// <summary>Original owner of a visible hand card; used by the future sleeve customisation.</summary>
public sealed record CardOwner(int Seat, string Name);

public partial class HandCard : Button
{
    public CardDefinition Card { get; set; } = null!;
    internal bool InFan { get; init; }
    /// <summary>How far a fanned card rises under the pointer. Larger lifts also magnify the card.</summary>
    internal float Lift { get; init; } = 9;
    public float PresentationScale { get; set; } = 1;
    private Vector2 DrawSize => Size / PresentationScale;
    /// <summary>Shows a skill upright in its own landscape format instead of rotating it into the hand's portrait footprint.</summary>
    public bool Landscape { get; init; }
    private bool Wide => Landscape && HasSkillArtwork;
    /// <summary>The card face in the card's own units; the remaining height leaves room to rise.</summary>
    private Vector2 Face => Wide ? new(175, 124) : new(DrawSize.X - 2, 175);
    public bool Chosen
    {
        get => _chosen;
        set { if (_chosen == value) return; _chosen = value; if (IsInsideTree() && Lifts) { ZIndex = RestingLayer; SetProcess(true); } }
    }
    private bool _chosen;
    // The lowered desktop hand hides the effect box: a selected card stays up like a hovered one.
    private bool Lifts => InFan && Lift > 9;
    private int RestingLayer => IsHovered() ? 2 : HasFocus() || Lifts && _chosen ? 1 : 0;
    private float Zoom => Lifts && _artwork != null ? 1 + 0.08f * _hover : 1;
    /// <summary>How far above its resting place the card is drawn, in its own units.</summary>
    internal float DrawnLift => (_hover * (InFan ? Lift : 5) + 175 * (Zoom - 1)) * PresentationScale;
    public bool Mulligan { get; set; }
    /// <summary>The card has at least one play authorized by the server in the current snapshot.</summary>
    public bool Playable { get; set; }
    public CardOwner? OriginalOwner { get; set; }
    public int? ViewerSeat { get; set; }
    /// <summary>False when the surrounding section already names the owner.</summary>
    public bool ShowOwnerBadge { get; set; } = true;
    private float _hover;
    private Texture2D? _artwork;
    public bool HasSkillArtwork => Card.Kind == CardKind.Skill && _artwork != null;
    public bool HasAttackArtwork => Card.Kind == CardKind.Attack && _artwork != null;
    private bool OwnerIsForeign => OriginalOwner is { } owner && ViewerSeat is { } viewer && owner.Seat != viewer;
    public override void _Ready()
    {
        if (Card.Kind == CardKind.Attack)
            _artwork = BoardArt.LoadCardArtwork(Card) ?? BoardArt.LoadCardArtwork(CardCatalog.Attack)
                ?? throw new InvalidOperationException($"No se pudo cargar la ilustración de {Card.Name}.");
        if (Card.Kind is CardKind.Equipment or CardKind.Code) _artwork = GD.Load<Texture2D>($"res://assets/cards/{Card.Id}-clean.png");
        if (Card.Kind == CardKind.Skill) _artwork = SkillArtwork.Load(Card);
        CustomMinimumSize = (Wide ? new Vector2(177, 133) : new Vector2(126, 184)) * PresentationScale;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = OriginalOwner is { } owner
            ? $"{Card.Name}\n{Card.Text}\nDueño original: {owner.Name}" + (OwnerIsForeign ? " · carta prestada" : "")
            : $"{Card.Name}\n{Card.Text}";
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        SetProcess(false);
        if (Lifts && _chosen) { _hover = 1; ZIndex = 1; }
        FocusEntered += UpdatePreview; FocusExited += UpdatePreview;
        MouseEntered += UpdatePreview; MouseExited += UpdatePreview;
    }

    private void UpdatePreview()
    {
        // Change the draw order without moving the hit area under the pointer.
        if (InFan) ZIndex = RestingLayer;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var target = IsHovered() || InFan && HasFocus() || Lifts && _chosen ? 1f : 0f;
        if (Math.Abs(_hover - target) < 0.001f) { _hover = target; SetProcess(false); return; }
        _hover = Mathf.Lerp(_hover, target, Math.Min(1, (float)delta * 14));
        QueueRedraw();
    }
    public override void _Draw()
    {
        var accent = BoardArt.CardColor(Card);
        var icon = BoardArt.CardIcon(Card);
        var y = 7 - _hover * (InFan ? Lift : 5);
        var r = new Rect2(new Vector2(1, y), Face);
        // A raised card grows from its lower edge, so neighbours keep their place in the fan.
        var zoom = Zoom;
        var scale = PresentationScale * zoom;
        var origin = new Vector2(DrawSize.X / 2, r.End.Y) * PresentationScale * (1 - zoom);
        DrawSetTransform(origin, 0, Vector2.One * scale);
        RelickVisuals.Shadow(this, new Rect2(r.Position + new Vector2(0, 4 + 5 * _hover), r.Size).Grow(-3), new Color(0, 0, 0, 0.5f), (int)(9 + 7 * _hover), 8);
        if (_artwork != null)
        {
            if (_hover > 0.01f && !Chosen) RelickVisuals.Shadow(this, r.Grow(-2), BoardArt.Tint(accent, 0.45f * _hover), 12, 8);
            if (Wide)
            {
                DrawTextureRect(_artwork, r, false);
                DrawSelection(y, accent);
                DrawOwner(y);
                return;
            }
            if (Card.Kind == CardKind.Skill)
            {
                // Rotate landscape skills into the portrait footprint of the hand.
                DrawSetTransform(origin + new Vector2(r.Position.X, r.End.Y) * scale, -Mathf.Pi / 2, Vector2.One * scale);
                DrawTextureRect(_artwork, new Rect2(0, 0, r.Size.Y, r.Size.X), false);
                DrawSetTransform(origin, 0, Vector2.One * scale);

                DrawSelection(y, accent);
                DrawOwner(y);
                return;
            }
            var imageSize = _artwork.GetSize();
            var imageScale = Math.Min(r.Size.X / imageSize.X, r.Size.Y / imageSize.Y);
            var extent = imageSize * imageScale;
            DrawTextureRect(_artwork, new Rect2(r.Position + (r.Size - extent) / 2, extent), false);

            DrawSelection(y, accent);
            DrawOwner(y);
            return;
        }
        BoardArt.Plate(this, r, RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 12, Chosen ? 2 : 1);
        DrawLine(new Vector2(15, y + 1), new Vector2(DrawSize.X - 15, y + 1), accent, 3, true);
        BoardArt.Text(this, Card.Kind == CardKind.Attachment ? "SOBRE RELIKARIO" : BoardArt.CardType(Card), new(12, y + 21), 9, accent);
        for (var i = 3; i >= 1; i--) DrawCircle(new Vector2(DrawSize.X / 2, y + 70), i * 12, BoardArt.Tint(accent, 0.035f));
        var center = new Vector2(DrawSize.X / 2, y + 69);
        DrawPolyline(new Vector2[] { center + new Vector2(0, -38), center + new Vector2(39, 0), center + new Vector2(0, 38), center + new Vector2(-39, 0), center + new Vector2(0, -38) }, BoardArt.Tint(accent, 0.2f), 1, true);
        BoardArt.Icon(this, icon, center, 43, accent, PresentationScale);
        var name = Card.Name;
        var fontSize = name.Length > 11 ? 14 : 17;
        while (fontSize > 11 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > DrawSize.X - 18) fontSize--;
        BoardArt.CenterText(this, name, new(DrawSize.X / 2, y + 122), fontSize, BoardArt.Ink);
        DrawSelection(y, accent);
        DrawOwner(y);
    }

    // A sleeve edge marks every card whose owner is known; outside a labelled memory block,
    // the rival's cards also carry a readable badge before selection.
    private void DrawOwner(float y)
    {
        if (OriginalOwner is not { } owner) return;
        var foreign = OwnerIsForeign;
        var color = foreign ? RelickVisuals.State.Rival : RelickVisuals.State.Own;
        var left = 3f; var right = Face.X - 1; var bottom = y + Face.Y;
        DrawLine(new Vector2(left, y + 8), new Vector2(left, bottom - 8), color, foreign ? 4 : 3, true);
        DrawLine(new Vector2(left + 4, y + 2), new Vector2(right - 4, y + 2), color, foreign ? 3 : 2, true);
        if (!foreign || !ShowOwnerBadge) return;
        var badge = new Rect2(8, bottom - 21, 48, 16);
        BoardArt.Plate(this, badge, new Color("0b1020"), color, 4, 1);
        BoardArt.CenterText(this, "RIVAL", new(badge.GetCenter().X, badge.Position.Y + 12), 8, color);
    }

    private void DrawSelection(float y, Color accent)
    {
        var face = Face;
        var rect = new Rect2(new Vector2(1, y), face);
        if (Playable && !Chosen && !Mulligan) RelickVisuals.Playable(this, rect, Disabled);
        RelickVisuals.Interaction(this, rect, Chosen, false, !Disabled && IsHovered(), HasFocus());
        if (Chosen && Mulligan) BoardArt.CenterText(this, "CAMBIAR", new(1 + face.X / 2, y + face.Y * 0.57f), 9, RelickVisuals.State.Selected);
    }

}


public partial class IconView : Control
{
    public GameIcon Icon { get; set; }
    public Color Color { get; set; } = BoardArt.Ink;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw() => BoardArt.Icon(this, Icon, Size / 2, Math.Min(Size.X, Size.Y), Color);
}

/// <summary>Menu trigger shown as a symbol; its name stays available to tooltips and screen readers.</summary>
/// <summary>
/// The private COMBO preference drawn like the printed COMBO tag of the cards: a pale bevelled
/// plate with a raised hand while COMBO responses are asked, dimmed with a skip mark while passed.
/// </summary>
public partial class ComboToggle : Button
{
    private static readonly Color AskFace = new("f2f2f0"), AskEdge = new("4a4f59"), AskInk = new("1b1d22");
    private static readonly Color PassFace = new("b9bcc3"), PassEdge = new("2c3038"), PassInk = new("4b4f57");
    public bool Compact { get; init; }
    private float PlateWidth => Compact ? 132 : 160;
    /// <summary>The plate is drawn shorter than the 48 px touch target.</summary>
    private const float PlateHeight = 40;

    public override void _Ready()
    {
        ToggleMode = true;
        CustomMinimumSize = new(PlateWidth + 6, 48);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        Toggled += _ => Describe();
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        Describe();
    }

    /// <summary>Shows a state without emitting <see cref="BaseButton.Toggled"/>.</summary>
    public void ShowAsking(bool asking) { SetPressedNoSignal(asking); Describe(); }

    private void Describe()
    {
        AccessibilityName = ButtonPressed ? "COMBO: preguntar" : "COMBO: pasar";
        QueueRedraw();
    }

    public override void _Draw()
    {
        var asking = ButtonPressed;
        var h = Math.Min(Size.Y, PlateHeight); var top = (Size.Y - h) / 2; var bottom = top + h;
        var radius = h * 0.44f; var slant = h * 0.3f;
        var right = Math.Min(Size.X - 4, PlateWidth) - radius;
        var hover = !Disabled && IsHovered();
        var fade = Disabled ? 0.45f : 1;
        Color A(Color c, bool lift = false) { if (lift && hover) c = c.Lightened(0.12f); return new(c, c.A * fade); }
        Vector2[] Slanted(float left, float upper, float end, float lower) =>
            [new(left + slant, upper), new(end, upper), new(end - slant, lower), new(left, lower)];

        // The bevel shows heavier at the bottom, like the printed tag, under the face.
        DrawColoredPolygon(Slanted(0, top, right, bottom), A(asking ? AskEdge : PassEdge));
        DrawColoredPolygon(Slanted(3, top + 3, right - 3, bottom - 5), A(asking ? AskFace : PassFace, true));
        // The lettering stays clear of the badge, shrinking when the plate is narrow.
        var center = new Vector2(right - slant / 2, Size.Y / 2);
        var font = RelickVisuals.Display; var size = (int)(h * 0.46f);
        var left = slant * 0.6f; var width = center.X - radius - 3 - left;
        while (size > 10 && font.GetStringSize("COMBO", HorizontalAlignment.Left, -1, size).X > width) size--;
        var baseline = top + (h - 2) / 2 + (font.GetAscent(size) - font.GetDescent(size)) / 2;
        DrawString(font, new(left, baseline), "COMBO", HorizontalAlignment.Center, width, size, A(asking ? AskInk : PassInk));

        DrawCircle(center, radius, A(AskInk));
        DrawCircle(center, radius - 3, A(asking ? RelickVisuals.State.Own : RelickVisuals.State.Rival, true));
        BoardArt.Icon(this, asking ? GameIcon.Hand : GameIcon.Skip, center, radius * 1.05f, A(AskInk));
        if (HasFocus()) DrawFocus(Slanted(0, top, right, bottom), center, radius);
    }

    /// <summary>The focus ring follows the plate and the badge together, so it never cuts across the symbol.</summary>
    private void DrawFocus(Vector2[] plate, Vector2 center, float radius)
    {
        var circle = new Vector2[32];
        for (var i = 0; i < circle.Length; i++) circle[i] = center + Vector2.FromAngle(i * Mathf.Tau / circle.Length) * radius;
        static float Area(Vector2[] p) => Math.Abs(p.Select((v, i) => v.Cross(p[(i + 1) % p.Length])).Sum()) / 2;
        var outline = Geometry2D.MergePolygons(plate, circle).MaxBy(Area);
        if (outline == null) return;
        var ring = Geometry2D.OffsetPolygon(outline, 2.5f, Geometry2D.PolyJoinType.Round).MaxBy(Area);
        if (ring != null) DrawPolyline([.. ring, ring[0]], BoardArt.Gold, 2, true);
    }
}

/// <summary>
/// A framed button that says its action with a symbol. Its name stays in the tooltip and for screen
/// readers; use it for repeated secondary actions whose symbol is unambiguous (edit, duplicate, delete).
/// </summary>
public partial class FramedIconButton : Button
{
    public GameIcon Symbol { get; init; }

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }

    public override void _Draw() => BoardArt.Icon(this, Symbol, Size / 2, Math.Min(22, Math.Min(Size.X, Size.Y) - 20),
        GetThemeColor(Disabled ? "font_disabled_color" : IsHovered() ? "font_hover_color" : "font_color"));
}

/// <summary>A toolbar action shown as a symbol; its name stays available to tooltips and screen readers.</summary>
public partial class IconButton : Button
{
    public GameIcon Symbol { get; init; }

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }

    public override void _Draw() => BoardArt.Icon(this, Symbol, Size / 2, Math.Min(26, Math.Min(Size.X, Size.Y) - 18),
        Disabled ? BoardArt.Muted : IsHovered() ? Colors.White : BoardArt.Ink);
}

public partial class IconMenuButton : MenuButton
{
    public GameIcon Symbol { get; init; } = GameIcon.Gear;

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
    }

    public override void _Draw() => BoardArt.Icon(this, Symbol, Size / 2, Math.Min(26, Math.Min(Size.X, Size.Y) - 18),
        Disabled ? BoardArt.Muted : IsHovered() ? Colors.White : BoardArt.Ink);
}
