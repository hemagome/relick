using Godot;

namespace Relick;

/// <summary>A selectable face-down card without any card identity or effect text.</summary>
public partial class CardBackButton : Button
{
    public bool Chosen { get; set; }
    public bool HasBackArtwork => _back != null;
    private Texture2D? _back;

    public override void _Ready()
    {
        CustomMinimumSize = new(126, 184);
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "Carta corrupta boca abajo. Pulsa para seleccionarla sin revelar su contenido.";
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        _back = GD.Load<Texture2D>("res://assets/cards/card-back.png")
            ?? throw new InvalidOperationException("No se pudo cargar el reverso de las cartas.");
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _Draw()
    {
        if (_back == null) return;
        var bounds = new Rect2(2, 5, Size.X - 4, Size.Y - 10);
        var scale = Math.Min(bounds.Size.X / _back.GetWidth(), bounds.Size.Y / _back.GetHeight());
        var extent = _back.GetSize() * scale;
        DrawTextureRect(_back, new Rect2(bounds.Position + (bounds.Size - extent) / 2, extent), false);
        if (Chosen || IsHovered() || HasFocus())
            BoardArt.Plate(this, new Rect2(Vector2.Zero, Size), Colors.Transparent, new Color("b5a2fa"), 6, Chosen ? 3 : 1);
    }
}
