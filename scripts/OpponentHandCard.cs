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
    public string? RelikarioId { get; init; }
    internal string? ArtworkRelikario => OriginalOwner?.RelikarioId ?? RelikarioId;
    public int ViewerSeat { get; init; } = -1;
    public Vector2 FaceSize { get; init; } = new(54, 76.4f);
    public bool IsFaceDown => PublicCard == null;
    /// <summary>Shown face up by an effect (not merely known): the card carries the eye badge.</summary>
    public bool Revealed { get; init; }
    public bool Armed { get; init; }
    /// <summary>The rival's hand hangs from the top of the screen: a revealed card rises downwards, towards the board.</summary>
    public float RevealDirection { get; init; } = 1;
    /// <summary>Only the board fan leaves room around the card; a scrolling strip would clip it, so it turns in place there.</summary>
    public bool RevealRoom { get; init; }
    public bool Targetable { get; set; }
    public bool Chosen { get; set; }
    private Texture2D? _back;
    private HandCard? _front;
    private Texture2D? _turningBack;
    private float _frontScale = 1;
    private float _reveal = -1;
    internal bool PlayingReveal => _reveal >= 0;
    internal Rect2 VisibleFaceRect => _back != null ? BoardArt.FitArtwork(_back, new Rect2(Vector2.Zero, Size)) :
        _front != null ? _front.GetTransform() * _front.VisibleFaceRect : new Rect2(Vector2.Zero, Size);

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
        if (Armed) TooltipText += "\nSe activa gratis al inicio de su próxima etapa de acciones si sigue en su mano.";
        if (PublicCard == null)
            _back = SleeveArt.Back(Sleeve);
        else
        {
            var scale = Math.Min(FaceSize.X / 126, FaceSize.Y / 184);
            var front = new HandCard
            {
                Name = "RevealedFront", Card = PublicCard, Disabled = true, ShowBehindParent = true, Size = new(126, 184), Scale = new(scale, scale),
                Position = (FaceSize - new Vector2(126, 184) * scale) / 2, RelikarioId = ArtworkRelikario
            };
            front.Ready += () => { front.MouseFilter = MouseFilterEnum.Ignore; front.SetProcess(false); };
            AddChild(front); _front = front; _frontScale = scale;
        }
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    /// <summary>The card leaves the hand towards the board, turns from its sleeve to its face and settles back.</summary>
    public void PlayReveal(bool reducedMotion)
    {
        if (reducedMotion || _front == null) return;
        _turningBack = SleeveArt.Back(Sleeve);
        _reveal = 0; ZIndex = 3; SetProcess(true); Pose();
    }

    public override void _Process(double delta)
    {
        if (_reveal < 0) { SetProcess(false); return; }
        _reveal += (float)delta / HandCard.RevealSeconds;
        if (_reveal >= 1) { _reveal = -1; ZIndex = 0; _turningBack = null; SetProcess(false); }
        Pose();
    }

    // The small rival card grows more than ours so the face can be read on its way.
    private float Grow => RevealRoom ? 1 + 0.9f * HandCard.RevealEnvelope(_reveal) : 1;
    private Vector2 Rise => RevealRoom ? new(0, RevealDirection * FaceSize.Y * 0.8f * HandCard.RevealEnvelope(_reveal)) : Vector2.Zero;

    private void Pose()
    {
        if (_front == null) return;
        var (squash, turned) = HandCard.RevealTurn(_reveal);
        _front.Visible = turned;
        var size = new Vector2(126, 184) * _frontScale * Grow;
        _front.Scale = new Vector2(_frontScale * Grow * squash, _frontScale * Grow);
        _front.Position = FaceSize / 2 + Rise - new Vector2(size.X * squash, size.Y) / 2;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var (squash, turned) = HandCard.RevealTurn(_reveal);
        if (_back != null || _turningBack != null && !turned)
        {
            var back = _back ?? _turningBack!;
            var rect = BoardArt.FitArtwork(back, new Rect2(Vector2.Zero, Size));
            var center = rect.GetCenter() + Rise;
            DrawSetTransform(center, 0, new Vector2(Grow * squash, Grow));
            rect = new Rect2(-rect.Size / 2, rect.Size);
            RelickVisuals.Shadow(this, new Rect2(rect.Position + new Vector2(0, 2), rect.Size).Grow(-2), new Color(0, 0, 0, 0.5f), 6);
            DrawTextureRect(back, rect, false);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        var rise = HandCard.RevealEnvelope(_reveal);
        if (rise > 0 && _front != null && turned)
        {
            var face = _front.GetTransform() * _front.VisibleFaceRect;
            RelickVisuals.Shadow(this, face.Grow(2), BoardArt.Tint(BoardArt.Gold, 0.55f * rise), 10, 6);
        }
        if (Revealed && _front != null && turned)
        {
            // The rival's fan only shows the lower band of each card, so the badge sits at the bottom corner.
            var face = _front.GetTransform() * _front.VisibleFaceRect;
            var size = Math.Clamp(FaceSize.X / 110, 0.42f, 0.8f);
            HandCard.RevealBadge(this, new Vector2(face.End.X + 3, face.End.Y - HandCard.RevealBadgeHeight(size) + 3), Armed, size);
        }
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), Chosen, Targetable, IsHovered(), HasFocus());
    }
}
