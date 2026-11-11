using Godot;
using Relick.Core;

namespace Relick;

public enum GameIcon { Sword, Bolt, Heart, Cards, Memory, Gem, Shield, Exchange, Gift, Reload, Flag, Speaker, SpeakerMuted }

/// <summary>Shared vector symbols: readable at card, counter and toolbar sizes.</summary>
public static class BoardArt
{
    public static readonly Color Ink = new("f1f2f8");
    public static readonly Color Muted = new("92a0bd");
    public static readonly Color Gold = new("f4c57a");
    public static Color CardColor(CardDefinition? card) => card?.Kind switch
    {
        CardKind.Attack => new("ef7890"), CardKind.Skill => new("5ed6b3"),
        CardKind.Attachment => new("b5a2fa"), CardKind.Equipment => new("81c8ef"), _ => Gold
    };
    public static GameIcon CardIcon(CardDefinition? card) => card?.Kind switch
    {
        CardKind.Attack => GameIcon.Sword, CardKind.Skill => GameIcon.Bolt,
        CardKind.Attachment => GameIcon.Flag, CardKind.Equipment => GameIcon.Shield,
        CardKind.Code => (card.Heal > 0 || card.HealFromDie) ? GameIcon.Heart : GameIcon.Reload, _ => GameIcon.Cards
    };
    public static string CardType(CardDefinition card) => card.Kind switch
    {
        CardKind.Attack => "ATAQUE", CardKind.Skill => "HABILIDAD", CardKind.Code => card.CodeType == CodeType.Play ? "CÓDIGO · JUGADA" : card.CodeType == CodeType.Object ? "CÓDIGO · OBJETO" : card.CodeType == CodeType.Character ? "CÓDIGO · PERSONAJE" : "CÓDIGO",
        CardKind.Attachment => "SOBRE EL RELIKARIO", _ => "EQUIPO"
    };
    public static Color Tint(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

    internal static Texture2D? LoadCardArtwork(CardDefinition card)
    {
        var path = $"res://assets/cards/{card.Id}-clean.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    internal static Rect2 FitArtwork(Texture2D artwork, Rect2 area)
    {
        var size = artwork.GetSize();
        var extent = size * Math.Min(area.Size.X / size.X, area.Size.Y / size.Y);
        return new Rect2(area.GetCenter() - extent / 2, extent);
    }

    public static void Plate(CanvasItem c, Rect2 rect, Color fill, Color edge, int radius = 12, int border = 1)
    {
        var cut = Math.Min(Math.Min(radius, 8), Math.Min(rect.Size.X, rect.Size.Y) / 2);
        var x = rect.Position.X; var y = rect.Position.Y; var r = rect.End.X; var b = rect.End.Y;
        var points = new Vector2[] { new(x + cut, y), new(r - cut, y), new(r, y + cut), new(r, b - cut),
            new(r - cut, b), new(x + cut, b), new(x, b - cut), new(x, y + cut) };
        if (fill.A > 0) c.DrawColoredPolygon(points, fill);
        if (border > 0 && edge.A > 0) c.DrawPolyline(points.Append(points[0]).ToArray(), edge, border, true);
    }

    public static void Text(CanvasItem c, string text, Vector2 pos, int size, Color color, float width = -1)
        => c.DrawString(ThemeDB.FallbackFont, pos, text, HorizontalAlignment.Left, width, size, color);
    public static void CenterText(CanvasItem c, string text, Vector2 pos, int size, Color color)
    {
        var width = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        Text(c, text, pos - new Vector2(width / 2, 0), size, color);
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
            case GameIcon.SpeakerMuted:
                Stroke(new(-5, -6), new(-1, -10), new(-1, -5));
                Stroke(new(-1, 6), new(-1, 10), new(-7, 4), new(-12, 4), new(-12, -4), new(-9, -4));
                c.DrawLine(new(-13, -12), new(11, 12), color, 3, true);
                break;
        }
        c.DrawSetTransform(Vector2.Zero, 0, Vector2.One * canvasScale);
    }
    public static void Die(CanvasItem c, Vector2 center, int face, float size, Color color)
    {
        var s = size / 2;
        Plate(c, new Rect2(center - Vector2.One * s, Vector2.One * size), Tint(color, 0.08f), Tint(color, 0.5f), 4);
        void Pip(float x, float y) => c.DrawCircle(center + new Vector2(x, y) * size * 0.23f, size * 0.055f, color);
        if (face % 2 == 1) Pip(0, 0);
        if (face >= 2) { Pip(-1, -1); Pip(1, 1); }
        if (face >= 4) { Pip(-1, 1); Pip(1, -1); }
        if (face == 6) { Pip(-1, 0); Pip(1, 0); }
    }
}

public partial class ArenaBackground : Control
{
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        var mat = RelickVisuals.Playmat;
        DrawRect(new Rect2(Vector2.Zero, Size), mat.Background);
        if (mat.Artwork != null) DrawTextureRect(mat.Artwork, new Rect2(Vector2.Zero, Size), false);
        // Low-contrast traces frame the play area without competing with card art.
        for (var i = 0; i < 7; i++)
        {
            var x = 18 + i * 25f;
            var y = 80 + i * 42f;
            var color = BoardArt.Tint(mat.Trace, 0.065f);
            var points = new Vector2[] { new(0, y), new(x, y), new(x + 26, y + 26), new(x + 26, Size.Y) };
            DrawPolyline(points, color, 1, true);
            DrawCircle(new(x, y), 2, color);
            DrawPolyline(points.Select(p => new Vector2(Size.X - p.X, Size.Y - p.Y)).ToArray(), color, 1, true);
        }

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
        DrawColoredPolygon(points, BoardArt.Tint(Accent, Amount > 0 ? 0.14f : 0.035f));
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
    public override void _Ready() { CustomMinimumSize = new(94, 30); MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), RelickVisuals.Playmat.Raised, RelickVisuals.Playmat.Frame, 8);
        BoardArt.Icon(this, GameIcon.Gem, new(17, Size.Y / 2), 18, BoardArt.Gold);
        BoardArt.Text(this, Amount.ToString(), new(32, 21), 18, BoardArt.Gold);
        BoardArt.Text(this, "PR", new(Size.X - 19, 20), 9, BoardArt.Gold);
    }
}

public partial class LifeMeter : Control
{
    public int Health { get; private set; }
    public int Maximum { get; private set; } = 1;
    public float DisplayedRatio { get; private set; }
    public bool IsAnimating => _elapsed < _duration;
    public Color FillColor => ColorForRatio(DisplayedRatio);
    private bool _initialized;
    private float _fromRatio;
    private float _targetRatio;
    private double _elapsed;
    private double _duration;

    public static Color ColorForRatio(float ratio) => ratio > 0.5f ? new("58d69a") : ratio > 0.2f ? new("f1c75b") : RelickVisuals.State.Danger;

    public void SetHealth(int health, int maximum, bool animate = true)
    {
        maximum = Math.Max(1, maximum);
        health = Math.Clamp(health, 0, maximum);
        if (_initialized && health == Health && maximum == Maximum && (animate || !IsAnimating)) return;
        Health = health;
        Maximum = maximum;
        TooltipText = $"{Health} de {Maximum} vidas";
        _targetRatio = (float)Health / Maximum;
        _fromRatio = DisplayedRatio;
        _elapsed = 0;
        _duration = animate && _initialized ? 0.25 + Math.Abs(_targetRatio - _fromRatio) * 0.65 : 0;
        _initialized = true;
        if (_duration == 0) DisplayedRatio = _targetRatio;
        QueueRedraw();
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(190, 44);
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
        BoardArt.Icon(this, GameIcon.Heart, new(17, 23), 23, color);
        var value = Health.ToString();
        var max = $"/ {Maximum}";
        var fontSize = 32;
        float Width(string text, int size) => ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        while (fontSize > 12 && Width(value, fontSize) + Width(max, Math.Max(10, fontSize / 2)) + 6 > Size.X * 0.43f - 36) fontSize--;
        BoardArt.Text(this, value, new(36, 34), fontSize, BoardArt.Ink);
        var maxStart = 36 + Width(value, fontSize) + 6;
        BoardArt.Text(this, max, new(maxStart, 33), Math.Max(10, fontSize / 2), BoardArt.Muted);
        var start = Math.Max(112, maxStart + Width(max, Math.Max(10, fontSize / 2)) + 14);
        BoardArt.Text(this, Health == 0 ? "GOLPE FINAL" : "VIDAS", new(start, 15), 9, color);
        var track = new Rect2(start, 22, Math.Max(1, Size.X - start), 16);
        BoardArt.Plate(this, track, new Color("0b1220"), new Color("536079"), 8);
        var fill = new Rect2(track.Position + new Vector2(3, 3), new Vector2(Math.Max(0, track.Size.X - 6) * DisplayedRatio, 10));
        if (fill.Size.X > 0)
        {
            BoardArt.Plate(this, fill, color, Colors.Transparent, (int)Math.Min(5, fill.Size.X / 2), 0);
            if (fill.Size.X > 10) DrawLine(fill.Position + new Vector2(5, 2), fill.Position + new Vector2(fill.Size.X - 5, 2), color.Lightened(0.3f), 1, true);
        }
    }
}

public partial class ZoneTile : Control
{
    public string Caption { get; set; } = "";
    public GameIcon Icon { get; set; }
    public int Amount { get; set; }
    public Color Accent { get; set; }
    public override void _Ready() { CustomMinimumSize = new(70, 46); MouseFilter = MouseFilterEnum.Pass; TooltipText = $"{Caption}: {Amount} cartas"; }
    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 8);
        BoardArt.Icon(this, Icon, new(16, 19), 18, Accent);
        BoardArt.Text(this, Amount.ToString(), new(32, 25), 21, BoardArt.Ink);
        BoardArt.CenterText(this, Caption, new(Size.X / 2, 39), 8, BoardArt.Muted);
    }
}

public partial class SkillTile : Button
{
    // The hand card canvas is 126 × 184; rotating it gives this exact footprint.
    public static readonly Vector2 CardSize = new(184, 126);
    public CardDefinition? Skill { get; set; }
    public float PresentationScale { get; set; } = 1;
    public int Slot { get; set; }
    public int SkillIndex { get; set; } = -1;
    public bool Chosen { get; set; }
    public bool Targetable { get; set; }
    public int Counters { get; set; }
    public bool HasSkillArtwork => _skillArt != null;
    private Texture2D? _skillArt;
    public Color Accent { get; set; }
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
        var edge = Skill == null ? BoardArt.Tint(RelickVisuals.Playmat.Frame, 0.45f) : RelickVisuals.Playmat.Frame;
        var fill = RelickVisuals.Playmat.Surface;
        BoardArt.Plate(this, new Rect2(0, 0, size.X, size.Y), fill, edge, 9, Chosen ? 2 : 1);
        if (Skill == null)
        {
            BoardArt.Icon(this, GameIcon.Bolt, size / 2, 24, BoardArt.Tint(BoardArt.Muted, 0.25f), PresentationScale);
            BoardArt.Text(this, $"{GameEngine.SkillSlotCosts[Slot]} PR", new(12, size.Y - 12), 12, BoardArt.Muted);
        }
        else if (_skillArt != null)
        {
            var fit = Math.Min((size.X - 4) / _skillArt.GetWidth(), (size.Y - 4) / _skillArt.GetHeight());
            var artSize = _skillArt.GetSize() * fit;
            DrawTextureRect(_skillArt, new Rect2((size - artSize) / 2, artSize), false);

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


public partial class DiceOutcomeStrip : Control
{
    public int Result { get; set; }
    public override void _Ready()
    {
        CustomMinimumSize = new(252, 42);
        MouseFilter = MouseFilterEnum.Pass;
        TooltipText = "D6: 1–3 fallo · 4 mitad del daño · 5 normal · 6 crítico.";
    }
    public override void _Draw()
    {
        var width = Size.X / 6;
        for (var i = 1; i <= 6; i++)
        {
            var color = i < 4 ? BoardArt.Muted : i == 4 ? BoardArt.Gold : i == 5 ? new Color("5ed6b3") : new Color("b5a2fa");
            var center = new Vector2(width * (i - 0.5f), 15);
            if (Result == i) BoardArt.Plate(this, new Rect2(width * (i - 1) + 1, 0, width - 2, 41), BoardArt.Tint(color, 0.14f), BoardArt.Tint(color, 0.6f), 6);
            BoardArt.Die(this, center, i, 21, color);
            BoardArt.CenterText(this, i < 4 ? "Fallo" : i == 4 ? "Mitad" : i == 5 ? "Normal" : "Crítico", new(center.X, 38), 8, color);
        }
    }
}

public partial class ExchangeTrack : VBoxContainer
{
    public bool Compact { get; set; }
    public string OwnerCaption { get; set; } = "";
    public IReadOnlyList<CardDefinition> ExchangeCards { get; set; } = Array.Empty<CardDefinition>();
    public int Filled => ExchangeCards.Count;
    public CardDefinition? BonusCard { get; set; }
    public bool BonusAvailable => BonusCard != null;
    public bool Preparing { get; set; }
    public bool BonusEffectUsed { get; set; }
    public IReadOnlyCollection<int> ExchangeEffectsUsed { get; set; } = Array.Empty<int>();
    public Color Accent { get; set; }
    public ZoneCardButton BonusButton { get; private set; } = null!;
    public ZoneCardButton[] ExchangeButtons { get; private set; } = null!;
    public event Action? BonusInspected;
    public event Action<int>? ExchangeInspected;
    public override void _Ready()
    {
        CustomMinimumSize = new(Compact ? 252 : 280, 0);
        AddThemeConstantOverride("separation", 4);
        var label = new Label { Text = $"{OwnerCaption}  ·  {Filled}/2 EN CANJE", MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_color", Accent);
        AddChild(label);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);
        ExchangeButtons = new ZoneCardButton[2];
        for (var slot = 0; slot < 2; slot++)
        {
            var index = slot;
            var button = new ZoneCardButton
            {
                Name = $"ExchangeCard{slot}", Caption = $"CANJE {slot + 1}" + (ExchangeEffectsUsed.Contains(slot) ? " · USADA" : ""), Accent = Accent, Compact = Compact,
                Card = slot < ExchangeCards.Count ? ExchangeCards[slot] : null
            };
            button.Pressed += () => ExchangeInspected?.Invoke(index);
            ExchangeButtons[slot] = button;
            row.AddChild(button);
        }
        BonusButton = new ZoneCardButton
        {
            Name = "BonusCard", Caption = "BONUS" + (BonusEffectUsed ? " · USADA" : ""), Card = Preparing ? null : BonusCard, Compact = Compact,
            Accent = BoardArt.Gold
        };
        BonusButton.Pressed += () => BonusInspected?.Invoke();
        row.AddChild(BonusButton);
    }
}

/// <summary>Inspectable card in a public board zone.</summary>
public partial class ZoneCardButton : Button
{
    public bool Targetable { get; set; }
    public bool Chosen { get; set; }
    public bool Compact { get; set; }
    public bool LargePreview { get; init; }
    public CardDefinition? Card { get; set; }
    public string Caption { get; set; } = "";
    public Color Accent { get; set; }
    private Texture2D? _artwork;

    internal Rect2 CardRect
    {
        get
        {
            var area = new Rect2(6, 20, Size.X - 12, Size.Y - 40);
            return _artwork == null ? area : BoardArt.FitArtwork(_artwork, area);
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(Compact ? 80 : 88, Compact && LargePreview ? 112 : 86);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Disabled = Card == null;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (Card != null) _artwork = BoardArt.LoadCardArtwork(Card);
        TooltipText = Card == null ? "" : $"{Caption} · {Card.Name}\n{Card.Text}\nPulsa para ampliar y consultar el efecto.";
        Resized += QueueRedraw;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var edge = Card != null ? RelickVisuals.Playmat.Frame : BoardArt.Tint(RelickVisuals.Playmat.Frame, 0.45f);
        BoardArt.Plate(this, new Rect2(0, 0, Size.X, Size.Y), RelickVisuals.Playmat.Surface, edge, 8);
        BoardArt.CenterText(this, Caption, new(Size.X / 2, 13), 9, Card == null ? BoardArt.Muted : Accent);
        if (_artwork != null) DrawTextureRect(_artwork, CardRect, false);
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), Chosen, Targetable, !Disabled && IsHovered(), HasFocus());
        if (Card == null) return;
        var name = Card.Name;
        var fontSize = 11;
        while (fontSize > 8 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > Size.X - 10) fontSize--;
        while (name.Length > 1 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > Size.X - 10) name = name[..^2] + "…";
        BoardArt.CenterText(this, name, new(Size.X / 2, Size.Y - 10), fontSize, BoardArt.Ink);
    }
}

public partial class AttachmentStack : Button
{
    public IReadOnlyList<string> Cards { get; init; } = [];
    private int Amount => Cards.Count;
    private Texture2D[] _previews = [];
    internal Rect2 CardRect => _previews.Length == 0 ? new Rect2(Vector2.Zero, Size) : PreviewRect(_previews.Length - 1);

    private Rect2 PreviewRect(int index)
    {
        const float offset = 16;
        var spread = (_previews.Length - 1) * offset;
        var area = new Rect2(6 + index * offset, 20, Size.X - 12 - spread, Size.Y - 40);
        return BoardArt.FitArtwork(_previews[index], area);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(92, 86);
        Disabled = Amount == 0;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        TooltipText = $"Sobre el Relikario: {Amount} carta(s). Sin límite. Pulsa para consultar todas las cartas y sus efectos.";
        _previews = Cards.TakeLast(3).Select(CardCatalog.Get).Select(BoardArt.LoadCardArtwork).OfType<Texture2D>().ToArray();
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        Resized += QueueRedraw;
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var accent = Amount == 0 ? BoardArt.Tint(BoardArt.Muted, 0.45f) : new Color("b5a2fa");
        BoardArt.Plate(this, new Rect2(0, 0, Size.X, Size.Y), RelickVisuals.Playmat.Surface, BoardArt.Tint(RelickVisuals.Playmat.Frame, Amount == 0 ? 0.45f : 1), 8);
        BoardArt.CenterText(this, "SOBRE RELIKARIO", new(Size.X / 2, 13), 8, accent);
        for (var i = 0; i < _previews.Length; i++) DrawTextureRect(_previews[i], PreviewRect(i), false);
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), false, false, !Disabled && IsHovered(), HasFocus());
        if (Amount > 0)
            BoardArt.CenterText(this, $"{Amount} carta{(Amount == 1 ? "" : "s")} · ∞", new(Size.X / 2, 76), 10, BoardArt.Ink);
    }
}

public partial class HandCard : Button
{
    public CardDefinition Card { get; set; } = null!;
    internal bool InFan { get; init; }
    public float PresentationScale { get; set; } = 1;
    private Vector2 DrawSize => Size / PresentationScale;
    public bool Chosen { get; set; }
    public bool Mulligan { get; set; }
    private float _hover;
    private Texture2D? _artwork;
    public bool HasSkillArtwork => Card.Kind == CardKind.Skill && _artwork != null;
    public bool HasCodeArtwork => Card.Kind == CardKind.Code && _artwork != null;
    public bool HasEquipmentArtwork => Card.Kind == CardKind.Equipment && _artwork != null;
    public bool HasAttackArtwork => Card.Kind == CardKind.Attack && _artwork != null;
    public override void _Ready()
    {
        if (Card.Kind == CardKind.Attack)
            _artwork = GD.Load<Texture2D>("res://assets/cards/attack-clean.png")
                ?? throw new InvalidOperationException("No se pudo cargar la ilustración de ataque.");
        if (Card.Kind is CardKind.Equipment or CardKind.Code) _artwork = GD.Load<Texture2D>($"res://assets/cards/{Card.Id}-clean.png");
        if (Card.Kind == CardKind.Skill) _artwork = SkillArtwork.Load(Card);
        CustomMinimumSize = new Vector2(126, 184) * PresentationScale;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = $"{Card.Name}\n{Card.Text}";
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        SetProcess(false);
        FocusEntered += UpdatePreview; FocusExited += UpdatePreview;
        MouseEntered += UpdatePreview; MouseExited += UpdatePreview;
    }

    private void UpdatePreview()
    {
        // Change the draw order without moving the hit area under the pointer.
        if (InFan) ZIndex = IsHovered() ? 2 : HasFocus() ? 1 : 0;
        SetProcess(true);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        var target = IsHovered() || InFan && HasFocus() ? 1f : 0f;
        if (Math.Abs(_hover - target) < 0.001f) { _hover = target; SetProcess(false); return; }
        _hover = Mathf.Lerp(_hover, target, Math.Min(1, (float)delta * 14));
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);
        var accent = BoardArt.CardColor(Card);
        var icon = BoardArt.CardIcon(Card);
        var y = 7 - _hover * (InFan ? 9 : 5);
        var r = new Rect2(1, y, DrawSize.X - 2, 175);
        BoardArt.Plate(this, new Rect2(r.Position + new Vector2(0, 3), r.Size), new Color(0, 0, 0, 0.25f), Colors.Transparent, 12, 0);
        BoardArt.Plate(this, r, RelickVisuals.Playmat.Surface, RelickVisuals.Playmat.Frame, 12, Chosen ? 2 : 1);
        if (_artwork != null)
        {
            if (Card.Kind == CardKind.Skill)
            {
                // Rotate landscape skills into the portrait footprint of the hand.
                DrawSetTransform(new Vector2(r.Position.X, r.End.Y) * PresentationScale, -Mathf.Pi / 2, Vector2.One * PresentationScale);
                DrawTextureRect(_artwork, new Rect2(0, 0, r.Size.Y, r.Size.X), false);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);

                DrawSelection(y, accent);
                return;
            }
            var imageSize = _artwork.GetSize();
            var imageScale = Math.Min(r.Size.X / imageSize.X, r.Size.Y / imageSize.Y);
            var extent = imageSize * imageScale;
            DrawTextureRect(_artwork, new Rect2(r.Position + (r.Size - extent) / 2, extent), false);

            DrawSelection(y, accent);
            return;
        }
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
        DrawLine(new(12, y + 134), new(DrawSize.X - 12, y + 134), new Color("34405a"), 1);
        if (Card.Kind == CardKind.Skill)
        {
            BoardArt.Icon(this, GameIcon.Sword, new(16, y + 152), 13, accent, PresentationScale);
            BoardArt.Text(this, (Card.Id == CardCatalog.Magnet.Id ? "1D6" : Card.NormalDamage.ToString()), new(26, y + 157), 14, BoardArt.Ink);
            BoardArt.Text(this, $"{Card.CriticalDamage} crítico", new(44, y + 156), 9, BoardArt.Gold);
            BoardArt.Icon(this, GameIcon.Gem, new(89, y + 152), 10, BoardArt.Gold, PresentationScale);
            BoardArt.Text(this, $"{Card.Reward}", new(98, y + 156), 10, BoardArt.Gold);
        }
        else if (Card.Kind == CardKind.Code)
        {
            BoardArt.Icon(this, Card.Heal > 0 ? GameIcon.Heart : GameIcon.Cards, new(18, y + 152), 15, accent, PresentationScale);
            BoardArt.Text(this, "+2", new(30, y + 157), 16, BoardArt.Ink);
            BoardArt.Text(this, "1 acción", new(DrawSize.X - 56, y + 156), 10, BoardArt.Muted);
        }
        else BoardArt.CenterText(this, Card.Kind == CardKind.Attack ? "Usa una habilidad" : "Carta de ejemplo", new(DrawSize.X / 2, y + 156), 11, BoardArt.Muted);
        DrawSelection(y, accent);
    }

    private void DrawSelection(float y, Color accent)
    {
        RelickVisuals.Interaction(this, new Rect2(1, y, DrawSize.X - 2, 175), Chosen, false, !Disabled && IsHovered(), HasFocus());
        if (Chosen && Mulligan) BoardArt.CenterText(this, "CAMBIAR", new(DrawSize.X / 2, y + 99), 9, RelickVisuals.State.Selected);
    }

}


public partial class IconView : Control
{
    public GameIcon Icon { get; set; }
    public Color Color { get; set; } = BoardArt.Ink;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw() => BoardArt.Icon(this, Icon, Size / 2, Math.Min(Size.X, Size.Y), Color);
}
