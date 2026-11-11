using Godot;

namespace Relick;

public static class ArenaMotion
{
    public static void PositionCard(Control card, Rect2 source, Rect2 destination, float t, int player, float flip = 1,
        float sourceRotation = 0, float destinationRotation = 0)
    {
        var eased = t * t * (3 - 2 * t);
        var center = source.GetCenter().Lerp(destination.GetCenter(), eased);
        center.Y -= Mathf.Sin(t * Mathf.Pi) * 66;
        card.PivotOffset = card.Size / 2;
        card.Scale = source.Size.Lerp(destination.Size, eased) / card.Size * new Vector2(flip, 1);
        card.Position = center - card.Size / 2;
        card.Rotation = Mathf.Lerp(sourceRotation, destinationRotation, eased) + Mathf.Sin(t * Mathf.Pi) * (player == 0 ? -0.12f : 0.12f);
    }

    public static void ShuffleCard(Control card, Rect2 deck, float t, int layer)
    {
        var wave = Mathf.Sin(t * Mathf.Pi * 4 + layer * 0.7f) * Mathf.Sin(t * Mathf.Pi);
        card.Size = deck.Size; card.PivotOffset = card.Size / 2;
        card.Position = deck.Position + new Vector2(wave * 26, -Math.Abs(wave) * 12 - Mathf.Sin(t * Mathf.Pi) * layer * 3);
        card.Rotation = wave * 0.17f;
    }
}
