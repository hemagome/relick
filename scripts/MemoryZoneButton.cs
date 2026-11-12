using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Separate, inspectable memory piles. Corrupted cards expose no identity.</summary>
public partial class MemoryZoneButton : Button
{
    public int Amount { get; init; }
    public bool Corrupted { get; init; }
    public CardDefinition? TopCard { get; init; }
    public Color Accent { get; init; } = new("b5a2fa");
    private Texture2D? _back;
    private HandCard? _preview;
    // Legacy docks keep the compact counter; board rows show the top card as a real pile.
    private bool Tall => Size.Y > 96;
    public Rect2 CardRect
    {
        get
        {
            if (!Tall) return new(6, 18, 26, 36.8f);
            var height = Size.Y - 48;
            var extent = new Vector2(height * 126 / 184, height);
            return new Rect2(new Vector2((Size.X - extent.X) / 2, 20), extent);
        }
    }

    private void PlacePreview()
    {
        if (_preview == null) return;
        var rect = CardRect;
        var scale = rect.Size.Y / 175f;
        _preview.Scale = new(scale, scale);
        _preview.Position = rect.Position - new Vector2(1, 7) * scale;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(Math.Max(70, CustomMinimumSize.X), 58);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = Corrupted ? $"Cartas corruptas: {Amount}. Boca abajo. Pulsa para consultar esta zona."
            : $"Memoria: {Amount} carta(s) boca arriba. Pulsa para ver las cartas y sus efectos.";
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (Corrupted && Amount > 0) _back = GD.Load<Texture2D>("res://assets/cards/card-back.png");
        if (!Corrupted && TopCard != null)
        {
            var preview = new HandCard { Name = "MemoryTopCard", Card = TopCard, Disabled = true,
                Size = new(126, 184), Scale = new(0.2f, 0.2f), Position = new(6, 17) };
            preview.Ready += () => { preview.MouseFilter = MouseFilterEnum.Ignore; preview.SetProcess(false); };
            AddChild(preview); _preview = preview;
        }
        Resized += () => { PlacePreview(); QueueRedraw(); };
        PlacePreview();
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _Draw()
    {
        BoardArt.Socket(this, new Rect2(Vector2.Zero, Size), Accent, Amount == 0 ? 1 : 0.55f);
        BoardArt.CenterText(this, Corrupted ? "CORRUPTAS" : "MEMORIA", new(Size.X / 2, 13), 8, Accent);
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), false, false, IsHovered(), HasFocus());
        var thumb = CardRect;
        if (Tall && Amount > 0)
        {
            var layers = Math.Min(3, Amount - 1);
            RelickVisuals.Shadow(this, new Rect2(thumb.Position + new Vector2(layers * 1.5f, layers * 1.5f + 2), thumb.Size).Grow(-2), new Color(0, 0, 0, 0.55f), 7);
            for (var layer = layers; layer > 0; layer--)
                BoardArt.Plate(this, new Rect2(thumb.Position + new Vector2(layer * 1.5f, layer * 1.5f), thumb.Size),
                    new Color("1d2740"), new Color("5f6f96"), 3);
        }
        if (_back != null)
        {
            var scale = Math.Min(thumb.Size.X / _back.GetWidth(), thumb.Size.Y / _back.GetHeight());
            var extent = _back.GetSize() * scale;
            DrawTextureRect(_back, new Rect2(thumb.Position + (thumb.Size - extent) / 2, extent), false);
        }
        else if (Corrupted || TopCard == null)
        {
            if (!Tall) BoardArt.Plate(this, thumb, new Color("0d1322"), Accent.Darkened(0.5f), 3);
            BoardArt.Icon(this, Corrupted ? GameIcon.Cards : GameIcon.Memory, thumb.GetCenter(), Tall ? 20 : 17, BoardArt.Tint(Accent, Tall ? 0.16f : 0.8f));
        }
        if (!Tall)
        {
            BoardArt.CenterText(this, Amount.ToString(), new(50, 43), Amount < 100 ? 21 : 15, BoardArt.Ink);
            return;
        }
        var text = Amount.ToString();
        var width = Math.Max(26, ThemeDB.FallbackFont.GetStringSize(text, fontSize: 14).X + 14);
        var chip = new Rect2((Size.X - width) / 2, Size.Y - 24, width, 19);
        BoardArt.Plate(this, chip, new Color(0, 0, 0, 0.5f), BoardArt.Tint(Accent, 0.45f), 4);
        BoardArt.CenterText(this, text, new(Size.X / 2, Size.Y - 10), 14, BoardArt.Ink);
    }
}
