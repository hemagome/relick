using Godot;

namespace Relick;

/// <summary>Suggests an authorized phase change without moving or resizing its touch target.</summary>
public partial class AdvancePhaseButton : Button
{
    public bool Inviting { get; private set; }
    internal double PulseTime { get; private set; }
    internal float PulseOpacity => 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin((float)PulseTime * Mathf.Tau / 1.8f));

    public override void _Ready()
    {
        SetProcess(false);
        VisibilityChanged += () => { if (!IsVisibleInTree()) SetInvitation(false); };
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
        if (!Inviting) return;
        var rect = new Rect2(Vector2.One * 2, Size - Vector2.One * 4);
        BoardArt.Plate(this, rect, BoardArt.Tint(RelickVisuals.State.Selected, 0.05f),
            BoardArt.Tint(RelickVisuals.State.Selected, PulseOpacity), 6, 2);
        var start = new Vector2(10, Size.Y - 5);
        DrawLine(start, new(Size.X - 10, start.Y), BoardArt.Tint(RelickVisuals.State.Selected, PulseOpacity * 0.7f), 2, true);
    }
}
