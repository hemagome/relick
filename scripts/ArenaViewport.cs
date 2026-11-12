using Godot;

namespace Relick;

public static class ArenaViewport
{
    public static bool Compact(Control control) => control.Size.X < 1100;
    public static bool Short(Control control) => control.Size.Y < 600;
    /// <summary>Room for the full board: both fields, the hand and the reading rail without scrolling.</summary>
    public static bool Desktop(Control control) => control.Size.X >= 1400 && control.Size.Y >= 980;

    // One landscape canvas, including when a desktop window is resized taller than wide.
    public static Vector2I LandscapeSize(Vector2I size) => new(
        Math.Max(700, Math.Max(size.X, size.Y)), Math.Max(390, Math.Min(size.X, size.Y)));

    public static void Configure(Window window)
    {
        if (OS.GetCmdlineUserArgs().Contains("--pvp-ui-test") ||
            OS.GetCmdlineUserArgs().Any(a => a is "--music-test" or "--audio-test") ||
            OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--lobby-preview-dir="))) return;
        var mobile = OS.HasFeature("mobile");
        var density = mobile ? Math.Clamp(DisplayServer.ScreenGetDpi() / 160f, 1, 4) : 1;
        var size = window.Size;
        var requested = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ui-test-size="));
        if (requested != null)
        {
            var dimensions = requested.Split('=', 2)[1].Split('x');
            window.ContentScaleSize = LandscapeSize(new(int.Parse(dimensions[0]), int.Parse(dimensions[1])));
            return;
        }
        if (DisplayServer.GetName() == "headless") return;
        if (size.X <= 0 || size.Y <= 0) return;
        // Desktop keeps a 1024-high canvas and widens it to the window, so wide monitors show no bands.
        var logical = mobile || size.X < 1100
            ? LandscapeSize(new((int)(size.X / density), (int)(size.Y / density)))
            : new Vector2I(Math.Clamp((int)Math.Round(1024.0 * size.X / size.Y), 1440, 2280), 1024);
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

    public static void TouchMenu(PopupMenu menu)
    {
        menu.AddThemeFontSizeOverride("font_size", 18);
        menu.AddThemeConstantOverride("v_separation", 24);
    }
}
