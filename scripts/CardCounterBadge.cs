using Godot;
using Relick.Core;

namespace Relick;

internal readonly record struct CardCounters(int Count, int? Limit)
{
    public string Description => Limit is int limit ? $"{Count} de {limit} contadores" : $"Aumentos de daño: {Count}";

    public static CardCounters? For(CardDefinition? card, int? count)
    {
        if (card == null || count is not int value) return null;
        return card.Id switch
        {
            var id when id == CardCatalog.Destiny.Id => new(value, CardCatalog.DestinyCounterLimit),
            var id when id == CardCatalog.TurboSword.Id => new(value, CardCatalog.TurboSwordCounterLimit),
            var id when id == CardCatalog.IronwingRoll.Id => new(value, CardCatalog.IronwingRollCounterLimit),
            var id when id == CardCatalog.Sniper.Id => new(value, CardCatalog.SniperCounterLimit),
            var id when id == CardCatalog.SuperMagnet.Id => new(value, CardCatalog.SuperMagnetCounterLimit),
            _ when card.DamagePerReroll > 0 => new(value, null),
            _ => null
        };
    }
}

/// <summary>The same counter symbols are drawn on the field and in the card inspector.</summary>
internal partial class CardCounterBadge : Control
{
    private const float Height = 18;
    private const int FontSize = 12;
    internal CardCounters? Counters { get; private set; }

    internal void Present(CardCounters? counters)
    {
        Counters = counters;
        Visible = counters.HasValue;
        TooltipText = AccessibilityName = counters?.Description ?? "";
        if (counters is { } value) CustomMinimumSize = Measure(value);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Counters is { } counters) Paint(this, new Rect2(Vector2.Zero, Measure(counters)), counters);
    }

    private static Vector2 Measure(CardCounters counters) => new(counters.Limit is int limit ? 10 + limit * 12 :
        22 + Mathf.Ceil(RelickVisuals.Strong.GetStringSize(counters.Count.ToString(), fontSize: FontSize).X), Height);

    internal static Rect2 OnCard(Rect2 card, CardCounters counters)
    {
        var size = Measure(counters);
        size *= Math.Clamp((card.Size.X - 8) / size.X, 0f, 1f);
        return new Rect2(card.Position + new Vector2(4, card.Size.Y * 0.70f - size.Y), size);
    }

    internal static void Paint(CanvasItem canvas, Rect2 rect, CardCounters counters)
    {
        if (!rect.HasArea()) return;
        var scale = rect.Size.Y / Height;
        var limit = counters.Limit;
        var color = limit is int max && counters.Count >= max ? RelickVisuals.State.Danger.Lightened(.2f) : BoardArt.Gold;
        BoardArt.Plate(canvas, rect, new Color("10182c"), BoardArt.Tint(color, .65f), 4);
        var radius = 4 * scale;
        for (var index = 0; index < (limit ?? 1); index++)
        {
            var center = rect.Position + new Vector2((11 + 12 * index) * scale, rect.Size.Y / 2);
            Vector2[] diamond = [center + new Vector2(0, -radius), center + new Vector2(radius, 0),
                center + new Vector2(0, radius), center + new Vector2(-radius, 0)];
            if (index < counters.Count) canvas.DrawColoredPolygon(diamond, color);
            canvas.DrawPolyline([.. diamond, diamond[0]], index < counters.Count ? color : BoardArt.Muted, 1, true);
        }
        if (limit.HasValue) return;
        var font = RelickVisuals.Strong;
        var fontSize = Math.Max(1, Mathf.RoundToInt(FontSize * scale));
        var baseline = rect.GetCenter().Y + (font.GetAscent(fontSize) - font.GetDescent(fontSize)) / 2;
        canvas.DrawString(font, new Vector2(rect.Position.X + 19 * scale, baseline), counters.Count.ToString(),
            fontSize: fontSize, modulate: BoardArt.Ink);
    }
}
