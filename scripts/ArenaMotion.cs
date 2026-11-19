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
        card.Rotation = Mathf.LerpAngle(sourceRotation, destinationRotation, eased) + Mathf.Sin(t * Mathf.Pi) * (player == 0 ? -0.12f : 0.12f);
    }

    /// <summary>A card taken from one hand into the other: plucked out with a tug, then swept across the table on a curve.</summary>
    public static void StealCard(Control card, Rect2 source, Rect2 destination, float t, float flip = 1,
        float sourceRotation = 0, float destinationRotation = 0, bool emphasize = true)
    {
        const float pluck = 0.22f;
        var from = source.GetCenter();
        var to = destination.GetCenter();
        var away = Math.Sign(to.Y - from.Y) is var sign && sign != 0 ? sign : 1;
        // Out of the fan toward the table first, shaking as it comes loose.
        var tug = from + new Vector2(0, away * source.Size.Y * 0.28f);
        Vector2 center;
        float rotation, grow;
        if (t < pluck)
        {
            var u = t / pluck;
            var eased = 1 - (1 - u) * (1 - u);
            center = from.Lerp(tug, eased);
            rotation = sourceRotation + Mathf.Sin(u * Mathf.Pi * 3) * 0.09f * (1 - u);
            grow = 1 + 0.08f * eased;
        }
        else
        {
            var v = (t - pluck) / (1 - pluck);
            var eased = v * v * (3 - 2 * v);
            // The curve bows out to one side so the card visibly crosses the board instead of dropping straight down.
            var bow = from.X <= to.X ? -1 : 1;
            var control = (tug + to) / 2 + new Vector2(bow * Math.Max(140, Math.Abs(to.Y - tug.Y) * 0.35f), 0);
            center = tug.Lerp(control, eased).Lerp(control.Lerp(to, eased), eased);
            rotation = Mathf.LerpAngle(sourceRotation, destinationRotation, eased) + Mathf.Sin(v * Mathf.Pi) * 0.32f * -bow;
            grow = 1.08f + 0.1f * Mathf.Sin(v * Mathf.Pi) - 0.08f * eased;
        }
        var size = source.Size.Lerp(destination.Size, Math.Clamp((t - pluck) / (1 - pluck), 0, 1)) * (emphasize ? grow : 1);
        card.PivotOffset = card.Size / 2;
        card.Scale = size / card.Size * new Vector2(flip, 1);
        card.Position = center - card.Size / 2;
        card.Rotation = rotation;
    }

    public static void ShuffleCard(Control card, Rect2 deck, float t, int layer, float rotation = 0)
    {
        var wave = Mathf.Sin(t * Mathf.Pi * 4 + layer * 0.7f) * Mathf.Sin(t * Mathf.Pi);
        card.Size = deck.Size; card.PivotOffset = card.Size / 2;
        card.Position = deck.Position + new Vector2(wave * 26, -Math.Abs(wave) * 12 - Mathf.Sin(t * Mathf.Pi) * layer * 3);
        card.Rotation = rotation + wave * 0.17f;
    }
}
