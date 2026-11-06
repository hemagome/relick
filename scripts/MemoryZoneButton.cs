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
    public Rect2 CardRect => new(6, 18, 26, 36.8f);

    public override void _Ready()
    {
        CustomMinimumSize = new(70, 58);
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
            AddChild(preview);
        }
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _Draw()
    {
        BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), new Color(Corrupted ? "201c32" : "111a2c"),
            IsHovered() || HasFocus() ? Accent : Accent.Darkened(0.65f), 8);
        BoardArt.CenterText(this, Corrupted ? "CORRUPTAS" : "MEMORIA", new(Size.X / 2, 12), 8, Accent);
        var thumb = CardRect;
        if (_back != null)
        {
            var scale = Math.Min(thumb.Size.X / _back.GetWidth(), thumb.Size.Y / _back.GetHeight());
            var extent = _back.GetSize() * scale;
            DrawTextureRect(_back, new Rect2(thumb.Position + (thumb.Size - extent) / 2, extent), false);
        }
        else if (Corrupted || TopCard == null)
        {
            BoardArt.Plate(this, thumb, new Color("172238"), Accent.Darkened(0.5f), 3);
            BoardArt.Icon(this, Corrupted ? GameIcon.Cards : GameIcon.Memory, thumb.GetCenter(), 17, Accent.Darkened(0.2f));
        }
        BoardArt.CenterText(this, Amount.ToString(), new(50, 43), Amount < 100 ? 21 : 15, BoardArt.Ink);
    }
}
