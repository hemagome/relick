using Godot;
using Relick.Core;

namespace Relick;

/// <summary>Receives only public card data; hidden card identities never enter this widget.</summary>
public partial class OpponentHandCard : Button
{
    /// <summary>The rival's cosmetic sleeve, shown while the card is face down.</summary>
    public string? Sleeve { get; init; }
    internal Texture2D? BackTexture => _back;
    public CardDefinition? PublicCard { get; init; }
    public CardOwner? OriginalOwner { get; init; }
    public int ViewerSeat { get; init; } = -1;
    public Vector2 FaceSize { get; init; } = new(54, 76.4f);
    public bool IsFaceDown => PublicCard == null;
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
            : OriginalOwner is { } owner
                ? $"REVELADA · {PublicCard.Name}\n{PublicCard.Text}\nDueño original: {owner.Name}" +
                  (owner.Seat == ViewerSeat ? " · tu carta" : "") + "\nPulsa para consultar."
                : $"REVELADA · {PublicCard.Name}\n{PublicCard.Text}\nPulsa para consultar.";
        if (PublicCard == null)
            _back = SleeveArt.Back(Sleeve);
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
        if (_back != null || OriginalOwner is not { } owner || ViewerSeat < 0) return;
        var mine = owner.Seat == ViewerSeat;
        var accent = mine ? RelickVisuals.State.Own : RelickVisuals.State.Rival;
        BoardArt.Outline(this, new Rect2(Vector2.One, Size - Vector2.One * 2), accent, 5, mine ? 2 : 1);
        if (!mine) return;
        var badge = new Rect2(Size.X / 2 - 18, Size.Y - 18, 36, 13);
        BoardArt.Plate(this, badge, new Color("0b1020"), accent, 3, 1);
        BoardArt.CenterText(this, "TUYA", new(badge.GetCenter().X, badge.Position.Y + 10), 7, accent);
    }
}
