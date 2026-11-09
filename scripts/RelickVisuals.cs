using Godot;

namespace Relick;

// A playmat owns decoration. Interaction meanings stay fixed across all playmats.
public sealed record RelickPlaymat(Color Background, Color Surface, Color Raised, Color Frame, Color Trace,
    Texture2D? Artwork = null)
{
    public static RelickPlaymat Classic { get; } = new(new("101114"), new("191c22"), new("22262e"),
        new("515761"), new("6d8594"));
}

public static class RelickVisuals
{
    // Future theme selection can replace this before building the lobby/arena.
    public static RelickPlaymat Playmat { get; set; } = RelickPlaymat.Classic;
    public static class State
    {
        public static readonly Color Selected = new("f4c57a");
        public static readonly Color Target = new("65dce5");
        public static readonly Color Focus = new("f1f2f8");
        public static readonly Color Danger = new("ef6577");
        public static readonly Color Own = new("5ed6b3");
        public static readonly Color Rival = new("ef7890");
    }

    public static StyleBoxFlat Frame(Color fill, Color edge, int cut = 8, int width = 1) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = cut, CornerRadiusTopRight = cut, CornerRadiusBottomLeft = cut, CornerRadiusBottomRight = cut,
        CornerDetail = 1, AntiAliasing = true,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
    };

    public static Theme Controls()
    {
        var theme = new Theme { DefaultFontSize = 16 };
        theme.SetColor("font_color", "Label", BoardArt.Ink);
        foreach (var type in new[] { "Button", "LineEdit" })
        {
            theme.SetColor("font_color", type, BoardArt.Ink);
            theme.SetColor("font_disabled_color", type, BoardArt.Muted);
            theme.SetStylebox("normal", type, Frame(Playmat.Raised, Playmat.Frame));
            theme.SetStylebox("hover", type, Frame(Playmat.Raised.Lightened(0.12f), State.Focus));
            theme.SetStylebox("pressed", type, Frame(Playmat.Surface, State.Selected, 8, 2));
            theme.SetStylebox("disabled", type, Frame(Playmat.Surface, BoardArt.Tint(Playmat.Frame, 0.4f)));
            theme.SetStylebox("focus", type, Frame(Colors.Transparent, State.Focus, 8, 2));
        }
        theme.SetStylebox("read_only", "LineEdit", Frame(Playmat.Surface, Playmat.Frame));
        theme.SetColor("font_placeholder_color", "LineEdit", BoardArt.Muted);
        theme.SetColor("caret_color", "LineEdit", State.Focus);
        theme.SetColor("selection_color", "LineEdit", new("36545e"));
        theme.SetStylebox("panel", "PopupMenu", Frame(Playmat.Surface, Playmat.Frame));
        return theme;
    }

    public static void Primary(Button button, Color? accent = null)
    {
        var color = accent ?? State.Own;
        button.AddThemeStyleboxOverride("normal", Frame(color, color));
        button.AddThemeStyleboxOverride("hover", Frame(color.Lightened(0.15f), State.Focus));
        button.AddThemeStyleboxOverride("pressed", Frame(color.Darkened(0.18f), color));
        button.AddThemeStyleboxOverride("disabled", Frame(Playmat.Surface, Playmat.Frame));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            button.AddThemeColorOverride(state, new("10191b"));
    }

    public static TextureRect Logo(bool compact = false) => new()
    {
        Name = "RelickLogo", Texture = new AtlasTexture
        {
            Atlas = GD.Load<Texture2D>("res://assets/brand/relick-logo.png"), Region = new(124, 0, 890, 205)
        },
        CustomMinimumSize = new(compact ? 138 : 174, compact ? 32 : 40),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore
    };

    public static void Interaction(CanvasItem canvas, Rect2 rect, bool selected, bool target, bool hover, bool focus)
    {
        if (!selected && !target && !hover && !focus) return;
        var color = selected ? State.Selected : target ? State.Target : State.Focus;
        BoardArt.Plate(canvas, rect, Colors.Transparent, color, 5, selected || target || focus ? 3 : 1);
        if (selected)
        {
            var p = new Vector2(rect.End.X - 12, rect.Position.Y + 12);
            canvas.DrawRect(new(p - new Vector2(9, 9), new(18, 18)), color);
            canvas.DrawPolyline(new Vector2[] { p + new Vector2(-5, 0), p + new Vector2(-1, 4), p + new Vector2(5, -4) }, new Color("101114"), 3, true);
        }
        else if (target)
        {
            // Corner brackets distinguish an available target from a selected card.
            foreach (var (p, d) in new[] { (rect.Position + new Vector2(5, 5), 1), (rect.End - new Vector2(5, 5), -1) })
                canvas.DrawPolyline(new Vector2[] { p + new Vector2(0, 10 * d), p, p + new Vector2(10 * d, 0) }, color, 3, true);
        }
        if (focus) BoardArt.Plate(canvas, rect.Grow(-5), Colors.Transparent, State.Focus, 2);
    }
}
