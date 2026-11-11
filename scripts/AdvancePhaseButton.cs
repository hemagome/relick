using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Suggests an authorized phase change without moving or resizing its touch target.</summary>
public partial class AdvancePhaseButton : Button
{
    private StageAppearance _destination = StageAppearance.For(Phase.Final);

    public bool Inviting { get; private set; }
    internal double PulseTime { get; private set; }
    internal float PulseOpacity => 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin((float)PulseTime * Mathf.Tau / 1.8f));

    public override void _Ready()
    {
        SetProcess(false);
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

        var normal = Frame(surface.Raised.Lerp(accent, 0.24f), accent);
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
        Inviting = inviting; PulseTime = 0;
        SetProcess(inviting); QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Disabled || !IsVisibleInTree()) { SetInvitation(false); return; }
        PulseTime = (PulseTime + Math.Max(0, delta)) % 1.8;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var accent = Disabled ? BoardArt.Muted : _destination.Accent;
        var centerY = Size.Y / 2;
        BoardArt.Icon(this, _destination.Icon, new(20, centerY), 19, accent);
        var right = Size.X - 15;
        DrawPolyline([new(right - 3, centerY - 5), new(right + 2, centerY), new(right - 3, centerY + 5)], accent, 2, true);
        if (!Inviting) return;
        var start = new Vector2(10, Size.Y - 5);
        DrawLine(start, new(Size.X - 10, start.Y), BoardArt.Tint(accent.Lightened(0.45f), PulseOpacity), 2, true);
    }
}
