using Godot;

namespace Relick;

public partial class HandFan : Control
{
    internal const float CardScale = 0.88f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Resized += ArrangeCards;
        ArrangeCards();
    }

    private void ArrangeCards()
    {
        var cards = GetChildren().OfType<HandCard>().ToArray();
        if (cards.Length == 0) return;

        var face = cards[0].CustomMinimumSize;
        var pivot = face / 2;
        var step = face.X * 0.55f;
        var halfCount = (cards.Length - 1) / 2f;
        var maxAngle = Mathf.DegToRad(Math.Min(8f, halfCount * 7));
        var poses = new Transform2D[cards.Length];
        var bounds = new Rect2();
        for (var i = 0; i < cards.Length; i++)
        {
            var side = (i - halfCount) / Math.Max(0.5f, halfCount);
            var center = new Vector2(i * step, 8 * side * side);
            var pose = new Transform2D(side * maxAngle, center);
            poses[i] = pose;
            var rect = pose * new Rect2(-pivot, face);
            bounds = i == 0 ? rect : bounds.Merge(rect);
        }

        CustomMinimumSize = bounds.Size.Ceil() + new Vector2(12, 6);
        var offset = new Vector2(Math.Max(Size.X, CustomMinimumSize.X) / 2 - bounds.GetCenter().X,
            4 - bounds.Position.Y);
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
