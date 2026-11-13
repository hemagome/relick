using Godot;

namespace Relick;

public partial class HandFan : Control
{
    internal const float CardScale = 0.88f;
    /// <summary>Clear band above the cards so a raised card stays inside the hand's clip.</summary>
    internal float Headroom { get; init; } = 4;
    internal float MaximumAngle { get; init; } = 8;
    internal bool Spread { get; init; }
    /// <summary>The rival's fan: it hangs from the top edge, so the arc and the tilt are mirrored.</summary>
    internal bool Mirrored { get; init; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Resized += ArrangeCards;
        ArrangeCards();
    }

    private void ArrangeCards()
    {
        var cards = GetChildren().OfType<Button>().ToArray();
        if (cards.Length == 0) return;

        var face = cards[0].CustomMinimumSize;
        var pivot = face / 2;
        var step = face.X * 0.55f;
        // The minimum footprint keeps the tight overlap; a wide board lets the cards breathe.
        var spread = Spread && cards.Length > 1
            ? Math.Clamp((Size.X - face.X - 40) / (cards.Length - 1), step, face.X * 0.8f) : step;
        var halfCount = (cards.Length - 1) / 2f;
        var maxAngle = Mathf.DegToRad(Math.Min(MaximumAngle, halfCount * 7));
        var poses = new Transform2D[cards.Length];
        Rect2 Pose(float spacing)
        {
            var bounds = new Rect2();
            for (var i = 0; i < cards.Length; i++)
            {
                var side = (i - halfCount) / Math.Max(0.5f, halfCount);
                var mirror = Mirrored ? -1 : 1;
                var center = new Vector2(i * spacing, mirror * 8 * side * side);
                var pose = new Transform2D(mirror * side * maxAngle, center);
                poses[i] = pose;
                var rect = pose * new Rect2(-pivot, face);
                bounds = i == 0 ? rect : bounds.Merge(rect);
            }
            return bounds;
        }

        CustomMinimumSize = Pose(step).Size.Ceil() + new Vector2(12, 2 + Headroom);
        var bounds = Pose(spread);
        // A mirrored fan rests its lowest edge on the bottom of the control, where the screen edge cuts it.
        var offset = new Vector2(Math.Max(Size.X, CustomMinimumSize.X) / 2 - bounds.GetCenter().X,
            Mirrored ? Math.Max(Size.Y, CustomMinimumSize.Y) - Headroom - bounds.End.Y : Headroom - bounds.Position.Y);
        for (var i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            card.Size = face;
            card.PivotOffset = pivot;
            card.Rotation = poses[i].Rotation;
            card.Position = offset + poses[i].Origin - pivot;
        }
    }
}
