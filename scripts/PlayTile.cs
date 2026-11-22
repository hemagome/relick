using Godot;
using Relick.Core;

namespace Relick;

/// <summary>One play on the desktop strip: its card, the colour and side of whoever made it and its key figure.</summary>
public partial class PlayTile : Button
{
    internal const float TileHeight = 56;
    private const float ThumbWidth = 34, ThumbHeight = 48;
    // One for every tile: the strip is rebuilt on each play, and each wrapper left to the finalizer is a chance
    // for Godot to abort while it unloads C# at exit.
    private static StyleBoxEmpty? _bare;

    internal CardDefinition? Card { get; init; }
    internal string? Relikario { get; init; }
    internal bool Mine { get; init; }
    internal bool Newest { get; init; }
    internal bool Canceled { get; init; }
    internal Color Accent { get; init; }
    internal DuelHistoryChip? Figure { get; init; }
    internal Color FigureColor { get; init; }
    /// <summary>The newest resolution, which may still be animating; routine ones after it leave the tile settled.</summary>
    internal bool Latest { get; set; }
    internal string Action { get; init; } = "";
    internal string Outcome { get; init; } = "";
    private Texture2D? _art;
    private bool _settled = true;

    /// <summary>While the play is still animating, neither the strip nor its tooltip tells how it ended.</summary>
    internal bool Settled
    {
        get => _settled;
        set { if (_settled == value) return; _settled = value; Describe(); QueueRedraw(); }
    }

    public override void _Ready()
    {
        _art = Card == null ? null : CardShowcase.Artwork(Card, Relikario);
        CustomMinimumSize = new(0, TileHeight);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" }) AddThemeStyleboxOverride(state, _bare ??= new StyleBoxEmpty());
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
        Describe();
    }

    private void Describe()
    {
        TooltipText = _settled && Outcome.Length > 0 ? $"{Action}\n{Outcome}" : Action;
        AccessibilityName = TooltipText;
    }

    /// <summary>The figure in the room beside the thumbnail: the resulting lives, PR without its unit, the die.</summary>
    internal static string ShortFigure(DuelHistoryChip chip) => chip.Kind switch
    {
        DuelHistoryChipKind.Health or DuelHistoryChipKind.Actions when chip.Label.Split(" → ") is [var from, var to] &&
            int.TryParse(from, out var before) && int.TryParse(to, out var after) => $"{(after > before ? "+" : "−")}{Math.Abs(after - before)}",
        DuelHistoryChipKind.Pr => chip.Label.Replace(" PR", "").Replace('-', '−'),
        DuelHistoryChipKind.Die => chip.Label.Split(' ')[^1],
        _ => chip.Label
    };

    public override void _Draw()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        DrawRect(bounds, BoardArt.Tint(Accent, IsHovered() ? 0.30f : Newest ? 0.22f : 0.07f));
        DrawRect(new Rect2(Mine ? 0 : Size.X - 3, 0, 3, Size.Y), Accent);
        var thumb = new Rect2(5, (Size.Y - ThumbHeight) / 2, ThumbWidth, ThumbHeight);
        if (_art != null) DrawTextureRect(_art, BoardArt.FitArtwork(_art, thumb), false, Canceled ? new Color(1, 1, 1, 0.4f) : Colors.White);
        else BoardArt.Icon(this, GameIcon.Bolt, thumb.GetCenter(), 20, Accent);
        if (Canceled && _settled) DrawLine(thumb.Position + new Vector2(3, 7), thumb.End - new Vector2(3, 7), RelickVisuals.State.Danger, 2, true);
        var center = (thumb.End.X + Size.X - 3) / 2;
        if (_settled && Figure is { } figure)
        {
            var icon = figure.Kind switch
            {
                DuelHistoryChipKind.Health => GameIcon.Heart, DuelHistoryChipKind.Actions => GameIcon.Bolt,
                DuelHistoryChipKind.Pr => GameIcon.Gem, DuelHistoryChipKind.Draw => GameIcon.Cards, _ => (GameIcon?)null
            };
            var font = RelickVisuals.Strong;
            if (icon is { } symbol) BoardArt.Icon(this, symbol, new(center, Size.Y / 2 - 9), 11, FigureColor);
            else DrawString(font, new(center - 15, Size.Y / 2 - 5), "D6", HorizontalAlignment.Center, 30, 9, FigureColor);
            DrawString(font, new(center - 15, Size.Y / 2 + 12), ShortFigure(figure), HorizontalAlignment.Center, 30, 11, FigureColor);
        }
        if (HasFocus()) DrawRect(bounds.Grow(-1), RelickVisuals.State.Focus, false, 2);
    }

    public override GodotObject? _MakeCustomTooltip(string forText)
    {
        var column = new VBoxContainer { CustomMinimumSize = new(250, 0) };
        if (_art != null)
            column.AddChild(new TextureRect { Texture = _art, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new(200, 260) });
        var text = ArenaPresentation.Text(forText, 13);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart; text.CustomMinimumSize = new(250, 0);
        column.AddChild(text);
        return column;
    }
}
