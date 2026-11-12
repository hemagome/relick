using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Receives only public card data; hidden card identities never enter this widget.</summary>
public partial class OpponentHandCard : Button
{
    public CardDefinition? PublicCard { get; init; }
    public Vector2 FaceSize { get; init; } = new(54, 76.4f);
    public bool IsFaceDown => PublicCard == null;
    public bool HasBackArtwork => _back != null;
    public bool Targetable { get; set; }
    public bool Chosen { get; set; }
    private Texture2D? _back;

    public override void _Ready()
    {
        CustomMinimumSize = FaceSize;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        TooltipText = PublicCard == null ? "Carta boca abajo del rival. Su contenido está oculto."
            : $"REVELADA · {PublicCard.Name}\n{PublicCard.Text}\nPulsa para consultar.";
        if (PublicCard == null)
            _back = GD.Load<Texture2D>("res://assets/cards/card-back.png")
                ?? throw new InvalidOperationException("No se pudo cargar el reverso de las cartas.");
        else
        {
            var scale = Math.Min(FaceSize.X / 126, FaceSize.Y / 184);
            var front = new HandCard
            {
                Name = "RevealedFront", Card = PublicCard, Disabled = true, ShowBehindParent = true, Size = new(126, 184), Scale = new(scale, scale),
                Position = (FaceSize - new Vector2(126, 184) * scale) / 2
            };
            front.Ready += () => { front.MouseFilter = MouseFilterEnum.Ignore; front.SetProcess(false); };
            AddChild(front);
        }
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    public override void _Draw()
    {
        if (_back != null)
        {
            var scale = Math.Min(Size.X / _back.GetWidth(), Size.Y / _back.GetHeight());
            var extent = _back.GetSize() * scale;
            var rect = new Rect2((Size - extent) / 2, extent);
            RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 2), rect.Size).Grow(-2), new Color(0, 0, 0, 0.5f), 6);
            DrawTextureRect(_back, rect, false);
        }
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), Chosen, Targetable, IsHovered(), HasFocus());
    }
}
