using Godot;

namespace Relick;

/// <summary>
/// The shared language of the screens outside the duel. A screen takes its colours, text sizes and corners from here
/// instead of writing its own. Mint is the one main action, gold is selection, cyan informs, coral warns or destroys.
/// </summary>
public static class MenuVisuals
{
    /// <summary>Menus always wear the classic cloth; a player's playmat dresses only their arena.</summary>
    public static class Tone
    {
        public static readonly Color Background = RelickPlaymat.Classic.Background;
        public static readonly Color Surface = RelickPlaymat.Classic.Surface;
        public static readonly Color Raised = RelickPlaymat.Classic.Raised;
        public static readonly Color Edge = RelickPlaymat.Classic.Frame;
        public static readonly Color Trace = RelickPlaymat.Classic.Trace;
        /// <summary>Recessed areas: card grids, empty portraits.</summary>
        public static readonly Color Well = Background.Lerp(Surface, 0.6f);
        /// <summary>Lines between panels that touch each other.</summary>
        public static readonly Color Divider = Surface.Lerp(Edge, 0.5f);
        /// <summary>The fill behind a selected tab or tile.</summary>
        public static readonly Color Chosen = Raised.Lerp(BoardArt.Gold, 0.08f);
        /// <summary>Long text, a step quieter than titles.</summary>
        public static readonly Color Reading = BoardArt.Ink.Lerp(BoardArt.Muted, 0.45f);
    }

    /// <summary>The only text sizes of the menus.</summary>
    public static class Type
    {
        public const int Caption = 12, Body = 14, Control = 16, Subtitle = 20, Title = 28, Hero = 40;
        public static readonly int[] Scale = [Caption, Body, Control, Subtitle, Title, Hero];
    }

    /// <summary>The only corner cuts of the menus.</summary>
    public static class Cut
    {
        public const int Badge = 4, Control = 8, Panel = 12;
    }

    public const int TouchTarget = 48;

    /// <summary>The same way back on every screen; its tooltip says where it leads.</summary>
    public static Button Back(Node parent, string name, string destination, Action pressed)
    {
        var button = ArenaPresentation.Button(parent, "‹ Volver", pressed);
        button.Name = name;
        button.CustomMinimumSize = new(104, TouchTarget);
        button.SizeFlagsVertical = Godot.Control.SizeFlags.ShrinkCenter;
        button.TooltipText = button.AccessibilityName = $"Volver a {destination}";
        return button;
    }
}
