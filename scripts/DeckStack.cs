using Godot;

namespace Relick;

/// <summary>Face-down deck; only its size is public.</summary>
public partial class DeckStack : Control
{
    public int Amount { get; set; }
    public Color Accent { get; set; }
    public bool HasBackArtwork => _back != null;
    private Texture2D? _back;
    public override void _Ready()
    {
        CustomMinimumSize = new(70, 116);
        MouseFilter = MouseFilterEnum.Pass;
        TooltipText = $"Mazo: {Amount} cartas restantes";
        _back = GD.Load<Texture2D>("res://assets/cards/card-back.png");
    }

    public Rect2 TopRect => new(new Vector2(5, 5), new Vector2(54, 77));

    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), RelickVisuals.Playmat.Surface, BoardArt.Tint(RelickVisuals.Playmat.Frame, Amount == 0 ? 0.45f : 1), 8);
        if (Amount > 0 && _back != null)
        {
            for (var layer = Math.Min(4, Amount - 1); layer > 0; layer--)
                BoardArt.Plate(this, new Rect2(TopRect.Position + new Vector2(layer * 1.6f, layer * 1.6f), TopRect.Size),
                    new Color("26334b"), new Color("73809c"), 3);
            DrawTextureRect(_back, TopRect, false);
        }
        else
        {
            BoardArt.Plate(this, TopRect, new Color("0d1422"), new Color("34405a"), 4);
            BoardArt.CenterText(this, "VACÍO", new(32, 47), 9, BoardArt.Muted);
        }
        BoardArt.CenterText(this, $"MAZO · {Amount}", new(Size.X / 2, 103), 10, Accent);
    }
}
