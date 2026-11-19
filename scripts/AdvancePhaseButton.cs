using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Suggests an authorized phase change without moving or resizing its touch target.</summary>
public partial class AdvancePhaseButton : Button
{
    private StageAppearance _destination = StageAppearance.For(Phase.Final);

    /// <summary>Lit with the same steady yellow as an activatable card when advancing is the only move left.</summary>
    public bool Inviting { get; private set; }

    public override void _Ready()
    {
        VisibilityChanged += () => { if (!IsVisibleInTree()) SetInvitation(false); };
    }

    internal void Style(Phase destination, bool shortScreen)
    {
        _destination = StageAppearance.For(destination);
        var accent = _destination.Accent;
        var surface = RelickVisuals.Playmat;
        CustomMinimumSize = new(shortScreen ? 180 : 192, 48);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Alignment = HorizontalAlignment.Left;
        ClipText = true;
        AddThemeFontSizeOverride("font_size", shortScreen ? 14 : 15);

        StyleBoxFlat Frame(Color fill, Color border)
        {
            var frame = RelickVisuals.Frame(fill, border, 8, 2);
            frame.ContentMarginLeft = 38;
            frame.ContentMarginRight = 26;
            frame.ContentMarginTop = frame.ContentMarginBottom = 8;
            return frame;
        }

        AddThemeFontOverride("font", RelickVisuals.Strong);
        var normal = Frame(surface.Raised.Lerp(accent, 0.30f), accent);
        normal.ShadowColor = BoardArt.Tint(accent, 0.24f);
        normal.ShadowSize = 4;
        normal.ShadowOffset = new(0, 2);
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", Frame(surface.Raised.Lerp(accent, 0.34f), accent.Lightened(0.22f)));
        AddThemeStyleboxOverride("pressed", Frame(surface.Surface.Lerp(accent, 0.16f), accent));
        AddThemeStyleboxOverride("disabled", Frame(surface.Surface, BoardArt.Tint(surface.Frame, 0.65f)));
        AddThemeStyleboxOverride("focus", Frame(Colors.Transparent, RelickVisuals.State.Focus));
    }

    public void SetInvitation(bool inviting)
    {
        inviting &= !Disabled && IsVisibleInTree();
        if (Inviting == inviting) return;
        Inviting = inviting; QueueRedraw();
    }

    public override void _Draw()
    {
        var accent = Disabled ? BoardArt.Muted : _destination.Accent;
        var centerY = Size.Y / 2;
        // The invitation reuses the activatable-card light around the whole control; its hit area never moves.
        if (Inviting && !Disabled) RelickVisuals.Playable(this, new Rect2(Vector2.Zero, Size));
        BoardArt.Icon(this, _destination.Icon, new(20, centerY), 19, accent);
        var right = Size.X - 15;
        DrawPolyline([new(right - 3, centerY - 5), new(right + 2, centerY), new(right - 3, centerY + 5)], accent, 2, true);
    }
}
