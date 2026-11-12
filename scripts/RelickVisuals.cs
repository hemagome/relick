using Godot;

namespace Relick;

// A playmat owns decoration. Interaction meanings stay fixed across all playmats.
public sealed record RelickPlaymat(Color Background, Color Surface, Color Raised, Color Frame, Color Trace,
    Texture2D? Artwork = null)
{
    public static RelickPlaymat Classic { get; } = new(new("070a14"), new("121829"), new("1c2540"),
        new("46557d"), new("6f8fd0"));
}

public static class RelickVisuals
{
    // Future theme selection can replace this before building the lobby/arena.
    public static RelickPlaymat Playmat { get; set; } = RelickPlaymat.Classic;
    public static class State
    {
        public static readonly Color Selected = new("ffc86b");
        public static readonly Color Target = new("5fe0f2");
        public static readonly Color Focus = new("f2f4fb");
        public static readonly Color Danger = new("ff5a6e");
        public static readonly Color Own = new("4fe3b8");
        public static readonly Color Rival = new("ff6f8e");
    }

    private static Font? _display, _strong;
    private static readonly Dictionary<(Color, Color, Color, Color, int, int), ImageTexture> FrameTextures = new();
    private static readonly StyleBoxFlat ShadowBox = new() { BgColor = Colors.Transparent, DrawCenter = false };

    /// <summary>Slanted, heavy capitals for titles: echoes the forward lean of the Relick logotype.</summary>
    public static Font Display => _display ??= new FontVariation
    {
        BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.85f, SpacingGlyph = 1,
        VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(0.19f, 1), Vector2.Zero)
    };

    /// <summary>Upright semibold for buttons and values.</summary>
    public static Font Strong => _strong ??= new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.45f };

    public static StyleBoxFlat Frame(Color fill, Color edge, int cut = 8, int width = 1) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = cut, CornerRadiusTopRight = cut, CornerRadiusBottomLeft = cut, CornerRadiusBottomRight = cut,
        CornerDetail = 1, AntiAliasing = true,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
    };

    /// <summary>
    /// Cut-corner plate with a vertical gradient, a lit top edge and an optional outer glow.
    /// The nine-patch is generated once per palette entry; never call it with animated colours.
    /// </summary>
    public static StyleBoxTexture Glass(Color top, Color bottom, Color edge, int cut = 9, int glow = 0, Color? glowColor = null)
    {
        const int size = 64;
        var halo = glowColor ?? Colors.Transparent;
        var key = (top, bottom, edge, halo, cut, glow);
        if (!FrameTextures.TryGetValue(key, out var texture))
        {
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            var half = size / 2f - glow - 0.5f;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var px = Math.Abs(x + 0.5f - size / 2f);
                    var py = Math.Abs(y + 0.5f - size / 2f);
                    // Signed distance to an octagon: negative inside the plate.
                    var distance = Math.Max(Math.Max(px - half, py - half), (px + py - (2 * half - cut)) * 0.7071f);
                    var inside = Math.Clamp(0.5f - distance, 0, 1);
                    var t = Math.Clamp((y - glow) / (float)(size - 2 * glow - 1), 0, 1);
                    var color = top.Lerp(bottom, t);
                    // One bright pixel under the top border sells the bevel.
                    if (distance < -1.6f && y - glow is >= 2 and < 3) color = color.Lerp(Colors.White, 0.10f * color.A);
                    color = color.Lerp(edge, Math.Clamp(distance + 1.6f, 0, 1));
                    color.A *= inside;
                    if (glow > 0 && inside < 1)
                    {
                        var falloff = Mathf.Exp(-distance * distance / (glow * glow * 0.22f)) * halo.A * (1 - inside);
                        var alpha = color.A + falloff * (1 - color.A);
                        if (alpha > 0)
                            color = new Color((color.R * color.A + halo.R * falloff * (1 - color.A)) / alpha,
                                (color.G * color.A + halo.G * falloff * (1 - color.A)) / alpha,
                                (color.B * color.A + halo.B * falloff * (1 - color.A)) / alpha, alpha);
                    }
                    image.SetPixel(x, y, color);
                }
            texture = ImageTexture.CreateFromImage(image);
            FrameTextures[key] = texture;
        }
        var margin = Math.Min(28, glow + cut + 5);
        var box = new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginLeft = margin, TextureMarginRight = margin, TextureMarginTop = margin, TextureMarginBottom = margin,
            ExpandMarginLeft = glow, ExpandMarginRight = glow, ExpandMarginTop = glow, ExpandMarginBottom = glow,
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
        };
        return box;
    }

    /// <summary>Floating surface for inspectors, docks and dialogs.</summary>
    public static StyleBoxTexture Surface(Color? accent = null, bool lifted = true)
    {
        var edge = accent is { } tint ? Playmat.Frame.Lerp(tint, 0.55f) : Playmat.Frame;
        return Glass(BoardArt.Tint(Playmat.Raised, 0.94f), BoardArt.Tint(Playmat.Surface, 0.94f), edge, 10,
            lifted ? 8 : 0, new Color(0, 0, 0, 0.42f));
    }

    /// <summary>Translucent half of the playmat owned by one duelist; the active one is lit by its accent.</summary>
    public static StyleBoxTexture Field(Color accent, bool active)
    {
        var top = Playmat.Surface.Lerp(accent, active ? 0.10f : 0.03f);
        var box = Glass(BoardArt.Tint(top, 0.62f), BoardArt.Tint(Playmat.Background.Lerp(accent, active ? 0.05f : 0.01f), 0.70f),
            active ? BoardArt.Tint(accent, 0.75f) : BoardArt.Tint(Playmat.Frame, 0.75f), 12,
            active ? 8 : 0, BoardArt.Tint(accent, 0.22f));
        box.ContentMarginLeft = box.ContentMarginRight = 10;
        box.ContentMarginTop = box.ContentMarginBottom = 8;
        return box;
    }

    /// <summary>Soft drop shadow or coloured halo behind a custom-drawn card.</summary>
    public static void Shadow(CanvasItem canvas, Rect2 rect, Color color, int size = 8, int radius = 6)
    {
        // Drawing is immediate, so one box serves every caller, including per-frame animated colours.
        var box = ShadowBox;
        if (box.ShadowColor != color) box.ShadowColor = color;
        if (box.ShadowSize != size) box.ShadowSize = size;
        if (box.CornerRadiusTopLeft != radius) box.SetCornerRadiusAll(radius);
        box.Draw(canvas.GetCanvasItem(), rect);
    }

    public static Theme Controls()
    {
        var theme = new Theme { DefaultFontSize = 16 };
        var surface = Playmat;
        theme.SetColor("font_color", "Label", BoardArt.Ink);
        foreach (var type in new[] { "Button", "MenuButton", "LineEdit" })
        {
            var field = type == "LineEdit";
            theme.SetColor("font_color", type, BoardArt.Ink);
            theme.SetColor("font_hover_color", type, Colors.White);
            theme.SetColor("font_disabled_color", type, BoardArt.Tint(BoardArt.Muted, 0.7f));
            if (!field) theme.SetFont("font", type, Strong);
            theme.SetStylebox("normal", type, field
                ? Glass(surface.Background.Lightened(0.03f), surface.Surface, surface.Frame, 8)
                : Glass(surface.Raised.Lightened(0.10f), surface.Raised.Darkened(0.18f), surface.Frame.Lightened(0.08f), 8, 4, new Color(0, 0, 0, 0.35f)));
            theme.SetStylebox("hover", type, Glass(surface.Raised.Lightened(0.22f), surface.Raised, State.Focus, 8, 4, BoardArt.Tint(State.Target, 0.30f)));
            theme.SetStylebox("pressed", type, Glass(surface.Surface, surface.Raised.Darkened(0.1f), State.Selected, 8, 4, BoardArt.Tint(State.Selected, 0.28f)));
            theme.SetStylebox("disabled", type, Glass(BoardArt.Tint(surface.Surface, 0.75f), BoardArt.Tint(surface.Surface, 0.75f), BoardArt.Tint(surface.Frame, 0.4f), 8));
            theme.SetStylebox("focus", type, Frame(Colors.Transparent, State.Focus, 8, 2));
        }
        theme.SetStylebox("read_only", "LineEdit", Glass(surface.Surface, surface.Surface, surface.Frame, 8));
        theme.SetColor("font_placeholder_color", "LineEdit", BoardArt.Muted);
        theme.SetColor("caret_color", "LineEdit", State.Focus);
        theme.SetColor("selection_color", "LineEdit", new("2d5a70"));

        var popup = Surface();
        popup.ContentMarginLeft = popup.ContentMarginRight = 8;
        theme.SetStylebox("panel", "PopupMenu", popup);
        theme.SetStylebox("hover", "PopupMenu", Frame(BoardArt.Tint(State.Target, 0.16f), BoardArt.Tint(State.Target, 0.55f), 6));
        theme.SetColor("font_color", "PopupMenu", BoardArt.Ink);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetColor("font_disabled_color", "PopupMenu", BoardArt.Tint(BoardArt.Muted, 0.6f));

        var tooltip = Glass(new Color("1a2340"), new Color("0f1528"), BoardArt.Tint(State.Target, 0.6f), 6, 6, new Color(0, 0, 0, 0.5f));
        tooltip.ContentMarginLeft = tooltip.ContentMarginRight = 12; tooltip.ContentMarginTop = tooltip.ContentMarginBottom = 8;
        theme.SetStylebox("panel", "TooltipPanel", tooltip);
        theme.SetColor("font_color", "TooltipLabel", BoardArt.Ink);
        theme.SetFontSize("font_size", "TooltipLabel", 14);

        // Dialogs are embedded windows: give them the same glass as the docks.
        var window = Glass(surface.Raised, surface.Surface, surface.Frame.Lightened(0.15f), 10, 10, new Color(0, 0, 0, 0.55f));
        window.ExpandMarginTop = 10 + 34; window.TextureMarginTop = 28;
        theme.SetStylebox("embedded_border", "Window", window);
        theme.SetStylebox("embedded_unfocused_border", "Window", window);
        theme.SetColor("title_color", "Window", BoardArt.Gold);
        theme.SetFont("title_font", "Window", Display);
        theme.SetFontSize("title_font_size", "Window", 17);
        theme.SetConstant("title_height", "Window", 34);
        theme.SetStylebox("panel", "AcceptDialog", new StyleBoxFlat { BgColor = surface.Surface.Lerp(surface.Background, 0.35f),
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 12 });

        var separator = new StyleBoxLine { Color = BoardArt.Tint(surface.Frame, 0.6f), Thickness = 1 };
        theme.SetStylebox("separator", "HSeparator", separator);

        var grabber = new StyleBoxFlat { BgColor = BoardArt.Tint(surface.Trace, 0.55f), ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3 };
        grabber.SetCornerRadiusAll(3);
        var track = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.18f), ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 3, ContentMarginBottom = 3 };
        track.SetCornerRadiusAll(3);
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, track);
            theme.SetStylebox("grabber", type, grabber);
            theme.SetStylebox("grabber_highlight", type, grabber);
            theme.SetStylebox("grabber_pressed", type, grabber);
        }
        return theme;
    }

    public static void Primary(Button button, Color? accent = null)
    {
        var color = accent ?? State.Own;
        button.AddThemeStyleboxOverride("normal", Glass(color.Lightened(0.22f), color.Darkened(0.10f), color.Lightened(0.5f), 8, 7, BoardArt.Tint(color, 0.40f)));
        button.AddThemeStyleboxOverride("hover", Glass(color.Lightened(0.42f), color, Colors.White, 8, 7, BoardArt.Tint(color, 0.65f)));
        button.AddThemeStyleboxOverride("pressed", Glass(color.Darkened(0.12f), color.Darkened(0.26f), color, 8, 7, BoardArt.Tint(color, 0.25f)));
        button.AddThemeStyleboxOverride("disabled", Glass(BoardArt.Tint(Playmat.Surface, 0.8f), BoardArt.Tint(Playmat.Surface, 0.8f), BoardArt.Tint(Playmat.Frame, 0.5f), 8, 7, Colors.Transparent));
        foreach (var state in new[] { "font_color", "font_hover_color", "font_pressed_color" })
            button.AddThemeColorOverride(state, new("06141a"));
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
        if (selected || target)
        {
            // Targets breathe so they read as invitations; a selection holds a steady halo.
            var pulse = target && !selected ? 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 1000f * Mathf.Tau / 1.4f) : 1;
            Shadow(canvas, rect, BoardArt.Tint(color, selected ? 0.55f : 0.22f + 0.28f * pulse), selected ? 12 : 10);
        }
        else if (hover) Shadow(canvas, rect, BoardArt.Tint(color, 0.22f), 8);
        BoardArt.Outline(canvas, rect, color, 5, selected || target || focus ? 3 : 1);
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
        if (focus) BoardArt.Outline(canvas, rect.Grow(-5), State.Focus, 5, 2);
    }
}
