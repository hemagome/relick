using Godot;

namespace Relick;

/// <summary>Face-down deck; only its size is public.</summary>
public partial class DeckStack : Control
{
    /// <summary>The owner's cosmetic sleeve (CosmeticCatalog id).</summary>
    public string? Sleeve { get; init; }
    internal Texture2D? BackTexture => _back;
    public int Amount { get; set; }
    public Color Accent { get; set; }
    private Texture2D? _back;
    private int _pendingArrivals;
    internal int PresentedAmount => Amount - _pendingArrivals;
    /// <summary>On the desktop board the pile fills its zone, like every other card.</summary>
    public bool Board { get; init; }
    internal bool RaisedPile { get; init; }
    public override void _Ready()
    {
        CustomMinimumSize = new(Math.Max(70, CustomMinimumSize.X), 116);
        MouseFilter = MouseFilterEnum.Pass;
        DescribeDeck();
        _back = SleeveArt.Back(Sleeve);
        Resized += QueueRedraw;
    }

    internal void ChangePendingArrivals(int delta)
    {
        _pendingArrivals = Math.Clamp(_pendingArrivals + delta, 0, Amount);
        DescribeDeck();
        QueueRedraw();
    }

    private void DescribeDeck() => TooltipText = $"Mazo: {PresentedAmount} cartas restantes";

    // Legacy docks keep the 70×116 footprint; board rows stretch the pile to their height.
    public Rect2 TopRect
    {
        get
        {
            if (Board)
            {
                var fit = BoardArt.FitArtwork(new Vector2(54, 77), RaisedPile ? BoardArt.StackArea(Size) : new Rect2(4, 4, Size.X - 12, Size.Y - 12));
                return fit;
            }
            if (Size.Y <= 118) return new(new Vector2(5, 5), new Vector2(54, 77));
            var height = Math.Min(Size.Y - 34, (Size.X - 14) * 77 / 54);
            var extent = new Vector2(height * 54 / 77, height);
            return new Rect2(new Vector2((Size.X - extent.X) / 2 - 2, 6 + (Size.Y - 34 - height) / 2), extent);
        }
    }

    public override void _Draw()
    {
        var amount = PresentedAmount;
        BoardArt.Socket(this, new Rect2(Vector2.Zero, Size), Accent, amount == 0 ? 1 : 0.55f);
        var top = TopRect;
        if (amount > 0 && _back != null)
        {
            if (RaisedPile) BoardArt.StackEdges(this, top, amount, Accent, deck: true);
            else
            {
                var layers = Math.Min(5, Math.Max(1, amount / 6));
                RelickVisuals.Shadow(this, new Rect2(top.Position + new Vector2(layers * 1.5f, layers * 1.5f + 2), top.Size).Grow(-2), new Color(0, 0, 0, 0.6f), 8);
                for (var layer = layers; layer > 0; layer--)
                    BoardArt.Plate(this, new Rect2(top.Position + new Vector2(layer * 1.5f, layer * 1.5f), top.Size),
                        new Color("1d2740"), new Color("5f6f96"), 3);
            }
            DrawTextureRect(_back, top, false);
        }
        else
        {
            BoardArt.Icon(this, GameIcon.Cards, top.GetCenter(), 20, BoardArt.Tint(Accent, 0.16f));
        }
        var caption = amount.ToString();
        var width = ThemeDB.FallbackFont.GetStringSize(caption, fontSize: 10).X + 14;
        var chip = new Rect2((Size.X - width) / 2, Size.Y - 22, width, 17);
        BoardArt.Plate(this, chip, Board ? new Color("0b1020") : new Color(0, 0, 0, 0.45f), BoardArt.Tint(Accent, Board ? 0.6f : 0.4f), 4);
        BoardArt.CenterText(this, caption, new(Size.X / 2, Size.Y - 10), 10, Accent);
    }
}
