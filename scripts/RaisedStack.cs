using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// A pile of cards as a solid on the tabletop. The zones are painted on the table's plane, so a point above it is
/// drawn where the eye sees it on that plane: the footprint scaled away from the spot under the eye. The table's
/// projection then shows it exactly as a raised card, for either seat and at any tilt.
/// </summary>
internal static class RaisedStack
{
    /// <summary>One 0.3 mm card, with the 63 mm wide card as the board's ruler.</summary>
    internal const float CardThickness = BoardCanvas.CardWidth * .3f / 63;
    // Closer seams than this, in screen units, blur into a solid band.
    private const float SeamSpacing = 2.4f;
    private static readonly Color Paper = new("d5d8df");

    internal static Rect2 Area(Vector2 slot) => new(4, 4, Math.Max(1, slot.X - 8), Math.Max(1, slot.Y - 8));

    /// <summary>Where the upper face of a pile appears, given the footprint it rests on.</summary>
    internal static Rect2 Top(Control zone, Rect2 footprint, int cards)
    {
        if (cards < 2 || ProjectedTable.Of(zone) == null) return footprint;
        return Lift(footprint, EyeFoot(zone), cards * CardThickness);
    }

    internal static Color SleeveColor(string? sleeve)
    {
        var style = CosmeticCatalog.Sleeve(sleeve);
        return style.Printed ? Paper : new Color(style.Edge ?? style.Light!);
    }

    internal static void Draw(CanvasItem canvas, Control zone, Rect2 footprint, int cards, Color? color = null)
    {
        if (cards < 2 || !footprint.HasArea() || ProjectedTable.Of(zone) == null) return;
        var eye = EyeFoot(zone);
        var height = cards * CardThickness;
        var toBoard = zone.GetGlobalTransform();
        var paper = color ?? Paper;
        RelickVisuals.Shadow(canvas, footprint.Grow(-1), new Color(0, 0, 0, .7f), 7, 4);

        // A side is seen only when the eye is outside its plane.
        var min = footprint.Position; var max = footprint.End;
        if (eye.X < min.X) Side(min, new(min.X, max.Y));
        if (eye.X > max.X) Side(new(max.X, min.Y), max);
        if (eye.Y < min.Y) Side(min, new(max.X, min.Y));
        if (eye.Y > max.Y) Side(new(min.X, max.Y), max);

        void Side(Vector2 from, Vector2 to)
        {
            var upperFrom = Lift(from, eye, height);
            var upperTo = Lift(to, eye, height);
            // The near face catches the light in screen space, whichever way the owner faces.
            var facesViewer = Mathf.Abs((toBoard.BasisXform(to - from)).Y) < .01f;
            var fill = paper.Darkened(facesViewer ? .22f : .46f);
            canvas.DrawColoredPolygon([from, to, upperTo, upperFrom], fill);

            var screenFrom = BoardCanvas.Project(toBoard * from);
            var along = (BoardCanvas.Project(toBoard * to) - screenFrom).Normalized();
            var depth = Mathf.Abs(along.Cross(BoardCanvas.Project(toBoard * upperFrom) - screenFrom));
            var layers = Math.Min(cards, Math.Max(1, (int)(depth / SeamSpacing)));
            for (var layer = 1; layer < layers; layer++)
            {
                var level = height * layer / layers;
                canvas.DrawLine(Lift(from, eye, level), Lift(to, eye, level), fill.Darkened(.42f), 1, true);
            }
            canvas.DrawLine(from, to, fill.Darkened(.65f), 1, true);
        }
    }

    private static Vector2 EyeFoot(Control zone) => zone.GetGlobalTransform().AffineInverse() * BoardCanvas.EyeFoot;

    private static Vector2 Lift(Vector2 point, Vector2 eye, float height) =>
        eye + (point - eye) * (BoardCanvas.EyeHeight / (BoardCanvas.EyeHeight - height));

    private static Rect2 Lift(Rect2 rect, Vector2 eye, float height)
    {
        var position = Lift(rect.Position, eye, height);
        return new Rect2(position, Lift(rect.End, eye, height) - position);
    }
}
