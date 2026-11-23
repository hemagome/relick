using Godot;

namespace Relick;

public partial class HandFan : Control
{
    internal const float CardScale = 0.88f;
    /// <summary>Clear band above the cards so a raised card stays inside the hand's clip.</summary>
    internal float Headroom { get; init; } = 4;
    internal float MaximumAngle { get; init; } = 8;
    internal bool Spread { get; init; }
    /// <summary>Width kept free on each side when the cards spread out; a crowded hand may still use it.</summary>
    internal float SideMargin { get; init; }
    /// <summary>The rival's fan: it hangs from the top edge, so the arc and the tilt are mirrored.</summary>
    internal bool Mirrored { get; init; }
    internal bool HeldOffTable { get; init; }
    /// <summary>
    /// The hand's card count, kept beside the cards it counts: past the last card, inside the band the
    /// screen edge leaves visible. A fan that spreads across the board would otherwise leave it far away,
    /// over another zone of the field.
    /// </summary>
    internal Control? Counter { get; set; }
    /// <summary>
    /// For the rival's fan: how much of it shows below the top edge. The count sits at the top of that
    /// band, above the rival's field, whose first row the hanging cards already overlap.
    /// </summary>
    internal float VisibleBand { get; init; }

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
        if (cards.Length == 0) { PlaceCounter(null); return; }

        var face = cards[0].CustomMinimumSize;
        var pivot = face / 2;
        var step = face.X * (HeldOffTable ? .36f : .55f);
        // The minimum footprint keeps the tight overlap; a wide board lets the cards breathe.
        var spread = Spread && cards.Length > 1
            ? Math.Clamp((Size.X - 2 * SideMargin - face.X - 40) / (cards.Length - 1), step, face.X * 0.8f) : step;
        if (HeldOffTable && cards.Length > 1)
            spread = Math.Min(step, Math.Max(1, (Size.X - face.X - 90) / (cards.Length - 1)));
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
                var center = new Vector2(i * spacing, mirror * (HeldOffTable ? 16 : 8) * side * side);
                var pose = new Transform2D(mirror * side * maxAngle, center);
                poses[i] = pose;
                var rect = pose * new Rect2(-pivot, face);
                bounds = i == 0 ? rect : bounds.Merge(rect);
            }
            return bounds;
        }

        CustomMinimumSize = HeldOffTable ? face + new Vector2(12, 2) : Pose(step).Size.Ceil() + new Vector2(12, 2 + Headroom);
        var bounds = Pose(spread);
        // A mirrored fan rests its lowest edge on the bottom of the control, where the screen edge cuts it.
        var offset = new Vector2(Math.Max(Size.X, CustomMinimumSize.X) / 2 - bounds.GetCenter().X,
            Mirrored || HeldOffTable ? Math.Max(Size.Y, CustomMinimumSize.Y) - Headroom - bounds.End.Y : Headroom - bounds.Position.Y);
        for (var i = 0; i < cards.Length; i++)
        {
            var card = cards[i];
            card.Size = face;
            card.PivotOffset = pivot;
            card.Rotation = poses[i].Rotation;
            card.Position = offset + poses[i].Origin - pivot;
        }
        PlaceCounter(new Rect2(bounds.Position + offset, bounds.Size));
    }

    private void PlaceCounter(Rect2? cards)
    {
        if (Counter is not { } counter || !IsInstanceValid(counter)) return;
        var size = counter.GetCombinedMinimumSize();
        var area = Size.X > 0 ? Size : CustomMinimumSize;
        const float gap = 10, inset = 18, edge = 8;
        var x = cards is { } placed ? placed.End.X + gap : (area.X - size.X) / 2;
        // The own fan shows its top band. The rival's hangs from the top edge: its count sits just under
        // that edge, clear of the rival's first row of zones.
        var bottom = cards is { } shown ? shown.End.Y : area.Y;
        var y = Mirrored || HeldOffTable
            ? VisibleBand > 0 ? bottom - VisibleBand + edge : bottom - inset - size.Y
            : (cards?.Position.Y ?? Headroom) + inset;
        counter.Size = size;
        counter.Position = new Vector2(Math.Clamp(x, 0, Math.Max(0, area.X - size.X)), y).Round();
    }
}
