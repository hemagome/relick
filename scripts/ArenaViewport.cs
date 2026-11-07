using Godot;

namespace Relick;

public static class ArenaViewport
{
    public static bool Compact(Control control) => control.Size.X < 1100;
    public static bool Short(Control control) => control.Size.Y < 600;

    public static void Configure(Window window)
    {
        if (OS.GetCmdlineUserArgs().Contains("--shared-arena-test")) return;
        var mobile = OS.HasFeature("mobile");
        var density = mobile ? Math.Clamp(DisplayServer.ScreenGetDpi() / 160f, 1, 4) : 1;
        var size = window.Size;
        var requested = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-test-size="));
        if (requested != null)
        {
            var dimensions = requested.Split('=', 2)[1].Split('x');
            window.ContentScaleSize = new(int.Parse(dimensions[0]), int.Parse(dimensions[1]));
            return;
        }
        if (DisplayServer.GetName() == "headless") return;
        if (size.X <= 0 || size.Y <= 0) return;
        var logical = mobile || size.X < 1100
            ? new Vector2I(Math.Max(320, (int)(size.X / density)), Math.Max(280, (int)(size.Y / density)))
            : new Vector2I(1440, 1024);
        if (window.ContentScaleSize != logical) window.ContentScaleSize = logical;
    }

    public static void ApplyInsets(Control root, int horizontal, int vertical)
    {
        root.OffsetLeft = horizontal; root.OffsetRight = -horizontal;
        root.OffsetTop = vertical; root.OffsetBottom = -vertical;
        if (!OS.HasFeature("mobile")) return;
        var safe = DisplayServer.GetDisplaySafeArea();
        if (safe.Size.X <= 0 || safe.Size.Y <= 0) return;
        var window = root.GetWindow();
        var scale = (Vector2)window.ContentScaleSize / (Vector2)window.Size;
        root.OffsetLeft += Math.Max(0, safe.Position.X) * scale.X;
        root.OffsetRight -= Math.Max(0, window.Size.X - safe.End.X) * scale.X;
        root.OffsetTop += Math.Max(0, safe.Position.Y) * scale.Y;
        root.OffsetBottom -= Math.Max(0, window.Size.Y - safe.End.Y) * scale.Y;
    }

    public static void FitDialog(Window dialog, Vector2 available)
    {
        dialog.MinSize = new Vector2I(Math.Min(dialog.MinSize.X, Math.Max(280, (int)available.X - 24)),
            Math.Min(dialog.MinSize.Y, Math.Max(200, (int)available.Y - 24)));
        dialog.PopupCenteredClamped(new Vector2I((int)available.X - 24, (int)available.Y - 24), 0.95f);
    }

    public static void EnlargeTouchTargets(Node node)
    {
        if (node is Button button)
        {
            button.CustomMinimumSize = new(button.CustomMinimumSize.X, Math.Max(48, button.CustomMinimumSize.Y));
            button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            if (button is MenuButton menu) TouchMenu(menu.GetPopup());
        }
        foreach (var child in node.GetChildren()) EnlargeTouchTargets(child);
    }

    public static void TouchMenu(PopupMenu menu)
    {
        menu.AddThemeFontSizeOverride("font_size", 18);
        menu.AddThemeConstantOverride("v_separation", 24);
    }

    public static void AdaptDialogContent(Control content, Vector2 available)
    {
        var width = Math.Max(260, available.X - 48);
        if (content is HBoxContainer row) row.Vertical = true;
        if (content is GridContainer grid) grid.Columns = Math.Max(1, (int)(width / 136));
        content.CustomMinimumSize = new(Math.Min(content.CustomMinimumSize.X, width), Math.Min(content.CustomMinimumSize.Y, Math.Max(140, available.Y - 130)));
        if (content is Label label) { label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; }
        foreach (var child in content.GetChildren().OfType<Control>()) AdaptDialogContent(child, available);
        EnlargeTouchTargets(content);
    }

    public static void PresentDialog(Window dialog, Control content, Vector2 available)
    {
        AdaptDialogContent(content, available);
        if (content is not ScrollContainer)
        {
            dialog.RemoveChild(content);
            var scroll = new ScrollContainer { CustomMinimumSize = new(Math.Max(260, available.X - 48), Math.Max(140, available.Y - 130)),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; scroll.AddChild(content); dialog.AddChild(scroll);
        }
        FitDialog(dialog, available);
    }
}
