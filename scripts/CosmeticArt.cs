using Godot;
using Relick.Core;

namespace Relick;

/// <summary>
/// Card backs per sleeve. A coloured sleeve recolours the printed back as a duotone (its blacks become the
/// sleeve's dark tone and its whites the light one) and frames it with the sleeve's edge colour. Each back is
/// generated once, at a reduced size, and kept for the session.
/// </summary>
public static class SleeveArt
{
    private const string PrintedBack = "res://assets/cards/card-back.png";
    private const int Height = 512, Edge = 7;
    private static readonly Dictionary<string, Texture2D> Backs = [];

    public static Texture2D Back(string? sleeve)
    {
        var style = CosmeticCatalog.Sleeve(sleeve);
        if (Backs.TryGetValue(style.Id, out var cached)) return cached;
        var printed = GD.Load<Texture2D>(PrintedBack) ?? throw new InvalidOperationException("No se pudo cargar el reverso de las cartas.");
        if (style.Printed) return Backs[style.Id] = printed;

        var image = printed.GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        if (image.GetHeight() > Height) image.Resize(image.GetWidth() * Height / image.GetHeight(), Height, Image.Interpolation.Lanczos);
        var light = new Color(style.Light!); var dark = new Color(style.Dark!); var edge = new Color(style.Edge ?? style.Light!);
        var (width, height) = (image.GetWidth(), image.GetHeight());
        var data = image.GetData();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var framed = x < Edge || y < Edge || x >= width - Edge || y >= height - Edge;
                var lum = (0.2126f * data[i] + 0.7152f * data[i + 1] + 0.0722f * data[i + 2]) / 255f;
                var color = framed ? edge : dark.Lerp(light, lum);
                data[i] = (byte)(color.R * 255); data[i + 1] = (byte)(color.G * 255); data[i + 2] = (byte)(color.B * 255);
            }
        return Backs[style.Id] = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, data));
    }

    /// <summary>The sleeve of a seat in a snapshot, classic when the seat is unknown.</summary>
    public static string? Of(DuelSnapshot? snapshot, int seat) =>
        snapshot != null && seat is 0 or 1 && seat < snapshot.Players.Length ? snapshot.Players[seat].Sleeve : null;
}

/// <summary>A flat picture of a die in a <see cref="DiceStyle"/>, showing its five face, for pickers.</summary>
public partial class DiceSwatch : Control
{
    public string Style { get; set; } = CosmeticCatalog.DefaultDice;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var style = CosmeticCatalog.DiceFor(Style);
        var side = Math.Min(Size.X, Size.Y) - 4;
        var rect = new Rect2((Size - Vector2.One * side) / 2, Vector2.One * side);
        RelickVisuals.Shadow(this, rect.Grow(-2), new Color(0, 0, 0, 0.45f), 6, (int)(side * 0.2f));
        var body = new StyleBoxFlat { BgColor = new Color(style.Body), BorderColor = new Color(style.Body).Darkened(0.35f) };
        body.SetBorderWidthAll(2); body.SetCornerRadiusAll((int)(side * 0.2f)); body.AntiAliasing = true;
        body.Draw(GetCanvasItem(), rect);
        var pip = new Color(style.Pips); var radius = side * 0.09f; var c = rect.GetCenter(); var o = side * 0.25f;
        foreach (var offset in new[] { Vector2.Zero, new(-o, -o), new(o, o), new(-o, o), new(o, -o) })
            DrawCircle(c + offset, radius, pip);
    }
}
