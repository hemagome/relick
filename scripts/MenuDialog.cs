using Godot;

namespace Relick;

/// <summary>
/// Dialogs outside the duel: as big as their content, scrolling only when the screen is smaller.
/// The duel's readers (memory, history) keep <see cref="ArenaViewport.FitDialog"/>, which gives them the whole screen.
/// </summary>
internal static class MenuDialog
{
    // An embedded window draws its title above its own rectangle.
    internal const int TitleBar = 44, Edge = 12;
    private const string BodyName = "MenuDialogBody";

    public static Label Message(string text)
    {
        var label = ArenaPresentation.Text(text, MenuVisuals.Type.Control);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    /// <summary>The main answer is mint, or coral when it destroys something; the others stay secondary.</summary>
    public static void Style(AcceptDialog dialog, bool destructive = false)
    {
        dialog.AddThemeColorOverride("title_color", BoardArt.Ink);
        dialog.AddThemeFontSizeOverride("title_font_size", MenuVisuals.Type.Subtitle);
        // The dialog sizes its own buttons from these constants and overwrites their minimum size.
        dialog.AddThemeConstantOverride("buttons_min_width", 120);
        dialog.AddThemeConstantOverride("buttons_min_height", MenuVisuals.TouchTarget);
        RelickVisuals.Primary(dialog.GetOkButton(), destructive ? RelickVisuals.State.Danger : null);
    }

    /// <summary>Puts the content in the dialog and keeps both inside the screen.</summary>
    public static void Fill(AcceptDialog dialog, Control content, float width, bool destructive = false)
    {
        Style(dialog, destructive);
        var body = new ScrollContainer
        {
            Name = BodyName, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true,
            CustomMinimumSize = new(width, 160)
        };
        body.SetMeta("width", width);
        dialog.AddChild(body);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.AddChild(content);
        // Wrapped text knows its height only once it has a width, so the dialog follows its content.
        content.MinimumSizeChanged += () => Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(dialog) && dialog.Visible) Fit(dialog);
        }).CallDeferred();
    }

    public static void Show(AcceptDialog dialog)
    {
        dialog.PopupCentered();
        Fit(dialog);
    }

    private static void Fit(AcceptDialog dialog)
    {
        if (dialog.GetParent()?.GetViewport() is not { } screen || dialog.GetNodeOrNull<ScrollContainer>(BodyName) is not { } body) return;
        var available = screen.GetVisibleRect().Size;
        var width = Math.Min(body.GetMeta("width").AsSingle(), available.X - 2 * Edge - 32);
        // Everything the dialog needs besides the content: its margins and its buttons.
        var chrome = dialog.GetContentsMinimumSize().Y - body.GetCombinedMinimumSize().Y;
        var height = Math.Min(body.GetChild<Control>(0).GetCombinedMinimumSize().Y, available.Y - TitleBar - Edge - chrome);
        body.CustomMinimumSize = new(width, Math.Max(40, height));
        var size = dialog.GetContentsMinimumSize().Ceil();
        dialog.Size = (Vector2I)size;
        dialog.Position = new((int)((available.X - size.X) / 2),
            (int)Math.Clamp((available.Y - size.Y + TitleBar) / 2, TitleBar, Math.Max(TitleBar, available.Y - size.Y - Edge)));
    }
}
