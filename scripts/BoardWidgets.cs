using Godot;
using Relick.Core;

namespace Relick;

public enum GameIcon { Sword, Bolt, Heart, Cards, Memory, Gem, Shield, Exchange, Gift, Reload, Flag }

/// <summary>Shared vector symbols: readable at card, counter and toolbar sizes.</summary>
public static class BoardArt
{
    private static readonly Vector2[] HeartPoints = Enumerable.Range(0, 48).Select(i =>
    {
        var t = i * Mathf.Tau / 48;
        return new Vector2(14 * Mathf.Pow(Mathf.Sin(t), 3), -(13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t)) * 0.8f);
    }).ToArray();
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
    public static void Plate(CanvasItem c, Rect2 rect, Color fill, Color edge, int radius = 12, int border = 1)
    {
        c.DrawStyleBox(new StyleBoxFlat
        {
            BgColor = fill, BorderColor = edge,
            BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border, BorderWidthBottom = border,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius
        }, rect);
    }
    public static void Text(CanvasItem c, string text, Vector2 pos, int size, Color color, float width = -1)
        => c.DrawString(ThemeDB.FallbackFont, pos, text, HorizontalAlignment.Left, width, size, color);
    public static void CenterText(CanvasItem c, string text, Vector2 pos, int size, Color color)
    {
        var width = ThemeDB.FallbackFont.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        Text(c, text, pos - new Vector2(width / 2, 0), size, color);
    }
    public static void Icon(CanvasItem c, GameIcon icon, Vector2 center, float size, Color color)
    {
        c.DrawSetTransform(center, 0, Vector2.One * (size / 32));
        void Line(float x, float y, float a, float b, float w = 2) => c.DrawLine(new(x, y), new(a, b), color, w, true);
        void Rect(float x, float y, float w, float h) => c.DrawRect(new(x, y, w, h), color, false, 1.8f);
        switch (icon)
        {
            case GameIcon.Sword:
                c.DrawColoredPolygon(new Vector2[] { new(-4, 2), new(6, -11), new(12, -13), new(11, -6), new(-1, 5) }, color);
                Line(-8, -1, 2, 9, 3); Line(-4, 6, -11, 13, 4); Line(-13, 10, -9, 14, 2);
                break;
            case GameIcon.Bolt:
                c.DrawColoredPolygon(new Vector2[] { new(0, -14), new(-11, 3), new(-2, 3), new(-5, 14), new(12, -5), new(3, -5), new(7, -14) }, color);
                break;
            case GameIcon.Heart:
                c.DrawColoredPolygon(HeartPoints, color);
                break;
            case GameIcon.Cards:
                Rect(-12, -12, 17, 23); Rect(-7, -8, 18, 23); Line(-3, -1, 6, -1); Line(-3, 4, 6, 4);
                break;
            case GameIcon.Memory:
                c.DrawArc(Vector2.Zero, 12, 0, Mathf.Tau, 40, color, 2, true); Line(0, -8, 0, 1); Line(0, 1, 6, 5);
                break;
            case GameIcon.Gem:
                var gem = new Vector2[] { new(-12, -5), new(-6, -12), new(6, -12), new(12, -5), new(0, 14) };
                c.DrawPolyline(gem.Append(gem[0]).ToArray(), color, 2, true);
                Line(-12, -5, 12, -5); Line(-6, -12, 0, 14); Line(6, -12, 0, 14);
                break;
            case GameIcon.Shield:
                var shield = new Vector2[] { new(-11, -12), new(11, -12), new(10, 3), new(0, 14), new(-10, 3), new(-11, -12) };
                c.DrawPolyline(shield, color, 2, true); Line(0, -7, 0, 6); Line(-5, 0, 5, 0);
                break;
            case GameIcon.Exchange:
                Line(-12, -6, 11, -6); Line(11, -6, 5, -12); Line(11, -6, 5, 0);
                Line(12, 7, -11, 7); Line(-11, 7, -5, 1); Line(-11, 7, -5, 13);
                break;
            case GameIcon.Gift:
                Rect(-11, -2, 22, 15); Rect(-13, -7, 26, 6); Line(0, -7, 0, 13);
                c.DrawArc(new(-4, -11), 4, 0, Mathf.Tau, 20, color, 2, true);
                c.DrawArc(new(4, -11), 4, 0, Mathf.Tau, 20, color, 2, true);
                break;
            case GameIcon.Reload:
                c.DrawArc(Vector2.Zero, 11, -Mathf.Pi * 0.6f, Mathf.Pi, 32, color, 2.5f, true);
                Line(-11, 0, -14, -6); Line(-11, 0, -5, -3); Line(-5, -1, 5, -1); Line(0, -6, 0, 4);
                break;
            case GameIcon.Flag:
                Line(-9, 14, -9, -14); Line(-9, -12, 10, -10); Line(10, -10, 4, -3); Line(4, -3, -9, -5);
                break;
        }
        c.DrawSetTransform(Vector2.Zero);
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
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("0b1020"));
        for (var i = 0; i < 14; i++)
        {
            var radius = (14 - i) * 40f;
            DrawCircle(new Vector2(Size.X * 0.16f, Size.Y * 0.36f), radius, new Color(0.12f, 0.42f, 0.4f, 0.008f));
            DrawCircle(new Vector2(Size.X * 0.86f, Size.Y * 0.34f), radius, new Color(0.55f, 0.18f, 0.3f, 0.008f));
        }
        for (var x = -Size.Y; x < Size.X; x += 72)
            DrawLine(new Vector2(x, 0), new Vector2(x + Size.Y, Size.Y), new Color(0.55f, 0.65f, 0.85f, 0.022f), 1, true);
        var center = new Vector2(Size.X / 2, Size.Y * 0.39f);
        foreach (var radius in new[] { 135, 190, 255, 340 })
            DrawArc(center, radius, 0, Mathf.Tau, 120, new Color(0.8f, 0.67f, 0.42f, 0.045f), 1, true);
    }
}

public partial class ResourceBadge : Control
{
    public int Amount { get; set; }
    public override void _Ready() { CustomMinimumSize = new(94, 30); MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), new Color("2c2630"), new Color("6b5340"), 8);
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

    public static Color ColorForRatio(float ratio) => new(ratio > 0.5f ? "58d69a" : ratio > 0.2f ? "f1c75b" : "ef6577");

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
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), new Color("111a2c"), new Color("2a3650"), 8);
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
        _skillArt = SkillArtwork.Load(Skill);
        TooltipText = Skill == null ? $"Enseñar una habilidad · {GameEngine.SkillSlotCosts[Slot]} PR." : Skill.Text + (Skill.Id == CardCatalog.Destiny.Id ? $"\nEsta copia: {Counters}/2 contadores." : Skill.DamagePerReroll > 0 ? $"\nEsta copia: {Counters} aumentos · {Skill.NormalDamageAt(Counters)} normal / {Skill.CriticalDamageAt(Counters)} crítico." : "");
    }
    public override void _Draw()
    {
        var size = Size / PresentationScale;
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);
        var edge = Chosen || Targetable ? Accent : new Color("34425e");
        var fill = Chosen ? BoardArt.Tint(Accent, 0.13f) : new Color("11192b");
        BoardArt.Plate(this, new Rect2(0, 0, size.X, size.Y), fill, edge, 9, Chosen ? 2 : 1);
        if (Skill == null) BoardArt.CenterText(this, $"{GameEngine.SkillSlotCosts[Slot]} PR", new(size.X / 2, size.Y / 2 + 8), 22, BoardArt.Muted);
        else if (_skillArt != null)
        {
            var fit = Math.Min((size.X - 4) / _skillArt.GetWidth(), (size.Y - 4) / _skillArt.GetHeight());
            var artSize = _skillArt.GetSize() * fit;
            DrawTextureRect(_skillArt, new Rect2((size - artSize) / 2, artSize), false);
            if (Chosen || Targetable) BoardArt.Plate(this, new Rect2(Vector2.Zero, size), Colors.Transparent, Chosen ? BoardArt.Gold : Accent, 5, Chosen ? 3 : 2);
            if (Skill.Id == CardCatalog.Destiny.Id)
            {
                BoardArt.Plate(this, new Rect2(8, size.Y - 24, 114, 20), new Color("111a2c"), Accent, 4);
                BoardArt.Text(this, $"CONTADORES {Counters}/2", new(14, size.Y - 10), 10, Counters >= 2 ? new Color("ef7890") : BoardArt.Ink);
            }
            else if (Skill.DamagePerReroll > 0)
            {
                BoardArt.Plate(this, new Rect2(8, size.Y - 24, 156, 20), new Color("111a2c"), Accent, 4);
                BoardArt.Text(this, $"NORMAL {Skill.NormalDamageAt(Counters)} · CRÍTICO {Skill.CriticalDamageAt(Counters)}", new(14, size.Y - 10), 10, BoardArt.Ink);
            }
        }
        else
        {
            DrawLine(new(14, 2), new(size.X - 14, 2), Accent, 3, true);
            BoardArt.Text(this, "HABILIDAD", new(12, 20), 9, Accent);
            BoardArt.CenterText(this, Skill.Name, new(size.X / 2, 47), 19, BoardArt.Ink);
            BoardArt.Icon(this, GameIcon.Bolt, new(34, 83), 32, Accent);
            BoardArt.Icon(this, GameIcon.Sword, new(65, 76), 14, BoardArt.Muted);
            BoardArt.Text(this, $"{(Skill.Id == CardCatalog.Magnet.Id ? "1D6" : Skill.NormalDamage.ToString())} daño", new(78, 81), 14, BoardArt.Ink);
            BoardArt.Text(this, $"{Skill.CriticalDamage} crítico", new(60, 105), 12, BoardArt.Gold);
            BoardArt.Icon(this, GameIcon.Gem, new(size.X - 52, 102), 12, BoardArt.Gold);
            BoardArt.Text(this, Skill.Reward.ToString(), new(size.X - 42, 107), 12, BoardArt.Gold);
        }
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
            Accent = BoardArt.Gold, EmptyCaption = Preparing ? "Por colocar" : "Sin carta"
        };
        BonusButton.Pressed += () => BonusInspected?.Invoke();
        row.AddChild(BonusButton);
    }
}

/// <summary>Inspectable card shared by the exchange and Bonus zones.</summary>
public partial class ZoneCardButton : Button
{
    public bool Targetable { get; set; }
    public bool Chosen { get; set; }
    public bool Compact { get; set; }
    public CardDefinition? Card { get; set; }
    public string Caption { get; set; } = "";
    public string EmptyCaption { get; set; } = "Vacío";
    public Color Accent { get; set; }
    private Texture2D? _artwork;
    public override void _Ready()
    {
        CustomMinimumSize = new(Compact ? 80 : 88, 86);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Disabled = Card == null;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (Card?.Kind == CardKind.Attack) _artwork = GD.Load<Texture2D>("res://assets/cards/attack-clean.png");
        TooltipText = Card == null ? $"{Caption}: {EmptyCaption}." : $"{Caption} · {Card.Name}\n{Card.Text}\nPulsa para ampliar y consultar el efecto.";
        Resized += QueueRedraw;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var edge = Chosen ? BoardArt.Gold : Card != null ? Accent.Darkened(IsHovered() || HasFocus() || Targetable ? 0 : 0.55f) : new Color("303b51");
        BoardArt.Plate(this, new Rect2(0, 0, Size.X, Size.Y), Card == null ? new Color("111a2c") : new Color("172238"), edge, 8);
        BoardArt.CenterText(this, Caption, new(Size.X / 2, 13), 9, Card == null ? BoardArt.Muted : Accent);
        var thumb = new Rect2((Size.X - 28) / 2, 20, 28, 39);
        var cardColor = BoardArt.CardColor(Card);
        if (_artwork != null)
        {
            var scale = Math.Min(thumb.Size.X / _artwork.GetWidth(), thumb.Size.Y / _artwork.GetHeight());
            var extent = _artwork.GetSize() * scale;
            DrawTextureRect(_artwork, new Rect2(thumb.Position + (thumb.Size - extent) / 2, extent), false);
        }
        else
        {
            BoardArt.Plate(this, thumb, BoardArt.Tint(cardColor, Card != null ? 0.12f : 0.03f), Card != null ? cardColor.Darkened(0.3f) : new Color("3b4961"), 4);
            var icon = Card != null ? BoardArt.CardIcon(Card) : Caption == "BONUS" ? GameIcon.Gift : GameIcon.Cards;
            BoardArt.Icon(this, icon, thumb.GetCenter(), 19, Card != null ? cardColor : BoardArt.Muted);
        }
        var name = Card?.Name ?? EmptyCaption;
        var fontSize = 11;
        while (fontSize > 8 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > Size.X - 10) fontSize--;
        while (name.Length > 1 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > Size.X - 10) name = name[..^2] + "…";
        BoardArt.CenterText(this, name, new(Size.X / 2, 76), fontSize, Card != null ? BoardArt.Ink : BoardArt.Muted);
    }
}

public partial class AttachmentStack : Button
{
    public int Amount { get; set; }
    public override void _Ready()
    {
        CustomMinimumSize = new(92, 86);
        Disabled = Amount == 0;
        MouseDefaultCursorShape = Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        TooltipText = $"Sobre el Relikario: {Amount} carta(s). Sin límite. Pulsa para consultar todas las cartas y sus efectos.";
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }
    public override void _Draw()
    {
        var accent = new Color("b5a2fa");
        BoardArt.Plate(this, new Rect2(0, 0, Size.X, Size.Y), new Color("172238"), Amount == 0 ? new Color("303b51") : accent.Darkened(IsHovered() || HasFocus() ? 0 : 0.55f), 8);
        BoardArt.CenterText(this, "SOBRE RELIKARIO", new(Size.X / 2, 13), 8, accent);
        for (var i = Math.Min(2, Math.Max(0, Amount - 1)); i >= 0; i--)
        {
            var position = new Vector2((Size.X - 25) / 2 + i * 4 - 4, 22 + i * 3);
            BoardArt.Plate(this, new Rect2(position, new Vector2(25, 32)), new Color("282440"), accent.Darkened(0.25f), 4);
            if (i == 0) BoardArt.Icon(this, GameIcon.Flag, position + new Vector2(12.5f, 16), 17, accent);
        }
        BoardArt.CenterText(this, Amount == 0 ? "Vacío · ∞" : $"{Amount} carta{(Amount == 1 ? "" : "s")} · ∞", new(Size.X / 2, 76), 10, Amount == 0 ? BoardArt.Muted : BoardArt.Ink);
    }
}

public partial class HandCard : Button
{
    public CardDefinition Card { get; set; } = null!;
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
        MouseEntered += () => SetProcess(true);
        MouseExited += () => SetProcess(true);
    }
    public override void _Process(double delta)
    {
        var target = IsHovered() ? 1f : 0f;
        if (Math.Abs(_hover - target) < 0.001f) { _hover = target; SetProcess(false); return; }
        _hover = Mathf.Lerp(_hover, target, Math.Min(1, (float)delta * 14));
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);
        var accent = BoardArt.CardColor(Card);
        var icon = BoardArt.CardIcon(Card);
        var y = 7 - _hover * 5;
        var r = new Rect2(1, y, DrawSize.X - 2, 175);
        BoardArt.Plate(this, new Rect2(r.Position + new Vector2(0, 3), r.Size), new Color(0, 0, 0, 0.25f), Colors.Transparent, 12, 0);
        BoardArt.Plate(this, r, Chosen ? new Color("213247") : new Color("182238"), Chosen ? accent : new Color("3b4964"), 12, Chosen ? 2 : 1);
        if (_artwork != null)
        {
            if (Card.Kind == CardKind.Skill)
            {
                // Rotate landscape skills into the portrait footprint of the hand.
                DrawSetTransform(new Vector2(r.Position.X, r.End.Y) * PresentationScale, -Mathf.Pi / 2, Vector2.One * PresentationScale);
                DrawTextureRect(_artwork, new Rect2(0, 0, r.Size.Y, r.Size.X), false);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One * PresentationScale);
                if (Chosen) BoardArt.Plate(this, r, Colors.Transparent, accent, 6, 2);
                DrawSelection(y, accent);
                return;
            }
            var imageSize = _artwork.GetSize();
            var imageScale = Math.Min(r.Size.X / imageSize.X, r.Size.Y / imageSize.Y);
            var extent = imageSize * imageScale;
            DrawTextureRect(_artwork, new Rect2(r.Position + (r.Size - extent) / 2, extent), false);
            if (Chosen) BoardArt.Plate(this, r, Colors.Transparent, accent, 12, 2);
            DrawSelection(y, accent);
            return;
        }
        DrawLine(new Vector2(15, y + 1), new Vector2(DrawSize.X - 15, y + 1), accent, 3, true);
        BoardArt.Text(this, Card.Kind == CardKind.Attachment ? "SOBRE RELIKARIO" : BoardArt.CardType(Card), new(12, y + 21), 9, accent);
        for (var i = 3; i >= 1; i--) DrawCircle(new Vector2(DrawSize.X / 2, y + 70), i * 12, BoardArt.Tint(accent, 0.035f));
        var center = new Vector2(DrawSize.X / 2, y + 69);
        DrawPolyline(new Vector2[] { center + new Vector2(0, -38), center + new Vector2(39, 0), center + new Vector2(0, 38), center + new Vector2(-39, 0), center + new Vector2(0, -38) }, BoardArt.Tint(accent, 0.2f), 1, true);
        BoardArt.Icon(this, icon, center, 43, accent);
        var name = Card.Name;
        var fontSize = name.Length > 11 ? 14 : 17;
        while (fontSize > 11 && ThemeDB.FallbackFont.GetStringSize(name, HorizontalAlignment.Left, -1, fontSize).X > DrawSize.X - 18) fontSize--;
        BoardArt.CenterText(this, name, new(DrawSize.X / 2, y + 122), fontSize, BoardArt.Ink);
        DrawLine(new(12, y + 134), new(DrawSize.X - 12, y + 134), new Color("34405a"), 1);
        if (Card.Kind == CardKind.Skill)
        {
            BoardArt.Icon(this, GameIcon.Sword, new(16, y + 152), 13, accent);
            BoardArt.Text(this, (Card.Id == CardCatalog.Magnet.Id ? "1D6" : Card.NormalDamage.ToString()), new(26, y + 157), 14, BoardArt.Ink);
            BoardArt.Text(this, $"{Card.CriticalDamage} crítico", new(44, y + 156), 9, BoardArt.Gold);
            BoardArt.Icon(this, GameIcon.Gem, new(89, y + 152), 10, BoardArt.Gold);
            BoardArt.Text(this, $"{Card.Reward}", new(98, y + 156), 10, BoardArt.Gold);
        }
        else if (Card.Kind == CardKind.Code)
        {
            BoardArt.Icon(this, Card.Heal > 0 ? GameIcon.Heart : GameIcon.Cards, new(18, y + 152), 15, accent);
            BoardArt.Text(this, "+2", new(30, y + 157), 16, BoardArt.Ink);
            BoardArt.Text(this, "1 acción", new(DrawSize.X - 56, y + 156), 10, BoardArt.Muted);
        }
        else BoardArt.CenterText(this, Card.Kind == CardKind.Attack ? "Usa una habilidad" : "Carta de ejemplo", new(DrawSize.X / 2, y + 156), 11, BoardArt.Muted);
        DrawSelection(y, accent);
    }

    private void DrawSelection(float y, Color accent)
    {
        if (Chosen)
        {
            DrawCircle(new Vector2(DrawSize.X - 17, y + 18), 8, accent);
            DrawPolyline(new Vector2[] { new(DrawSize.X - 21, y + 18), new(DrawSize.X - 18, y + 21), new(DrawSize.X - 13, y + 15) }, new Color("0b1422"), 2, true);
            if (Mulligan) BoardArt.CenterText(this, "CAMBIAR", new(DrawSize.X / 2, y + 99), 9, accent);
        }
    }
}

public partial class IconView : Control
{
    public GameIcon Icon { get; set; }
    public Color Color { get; set; } = BoardArt.Ink;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw() => BoardArt.Icon(this, Icon, Size / 2, Math.Min(Size.X, Size.Y), Color);
}

public partial class PhaseBadge : Control
{
    public string Caption { get; set; } = "";
    public GameIcon Icon { get; set; }
    public bool Current { get; set; }
    public bool Complete { get; set; }
    public override void _Ready() { CustomMinimumSize = new(120, 34); MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        var color = Current ? BoardArt.Gold : Complete ? new Color("5ed6b3") : new Color("63718e");
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), Current ? new Color("302a2d") : new Color("111a2b"), Current ? new Color("9d794c") : new Color("253149"), 8);
        BoardArt.Icon(this, Icon, new(21, 17), 17, color);
        BoardArt.Text(this, Caption, new(38, 22), 12, color);
        if (Current) DrawCircle(new Vector2(Size.X - 13, 17), 3, color);
    }
}
