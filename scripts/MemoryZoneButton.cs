using Godot;
using Relick.Core;

namespace Relick;

/// <summary>One memory pile, with the face-down cards below any face-up cards.</summary>
public partial class MemoryZoneButton : Button
{
    /// <summary>The owner's cosmetic sleeve, shown on face-down corrupted cards.</summary>
    public string? Sleeve { get; init; }
    internal Texture2D? BackTexture => _back;
    public int CorruptedCount { get; init; }
    public string[] Cards { get; init; } = [];
    public string? RelikarioId { get; init; }
    public Color Accent { get; init; } = new("b5a2fa");
    private Texture2D? _back;
    private HandCard? _preview;
    private Control? _body;
    private int _pendingArrivals;
    private int _pendingCorruptedArrivals;
    internal int DisplayedFaceUp => Math.Max(0, Cards.Length - _pendingArrivals);
    internal int DisplayedFaceDown => Math.Max(0, CorruptedCount - _pendingCorruptedArrivals);
    internal int DisplayedAmount => DisplayedFaceUp + DisplayedFaceDown;
    private bool ShowBack => DisplayedFaceUp == 0 && DisplayedFaceDown > 0 && _back != null;
    /// <summary>On the desktop board the pile fills its zone, like every other card.</summary>
    public bool Board { get; init; }
    internal bool RaisedPile { get; init; }
    // Legacy docks keep the compact counter; board rows show the top card as a real pile.
    private bool Tall => Size.Y > 96;
    // Flights land on the final height; the pile on screen only counts the cards that arrived.
    public Rect2 CardRect => RaisedPile ? RaisedStack.Top(this, Footprint, Cards.Length + CorruptedCount) : Footprint;
    private Rect2 PresentedRect => RaisedPile ? RaisedStack.Top(this, Footprint, DisplayedAmount) : Footprint;

    public override bool _HasPoint(Vector2 point) =>
        new Rect2(Vector2.Zero, Size).HasPoint(point) || RaisedPile && PresentedRect.HasPoint(point);

    private Rect2 Footprint
    {
        get
        {
            if (Board)
                return BoardArt.FitArtwork(new Vector2(126, 175), new Rect2(4, 4, Size.X - 8, Size.Y - 8));
            if (!Tall) return new(6, 18, 26, 36.8f);
            var height = Size.Y - 48;
            var extent = new Vector2(height * 126 / 184, height);
            return new Rect2(new Vector2((Size.X - extent.X) / 2, 20), extent);
        }
    }

    private void PlacePreview()
    {
        if (_preview == null) return;
        var rect = PresentedRect;
        var scale = rect.Size.Y / 175f;
        _preview.Scale = new(scale, scale);
        _preview.Position = rect.Position - new Vector2(1, 7) * scale;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new(Math.Max(70, CustomMinimumSize.X), 58);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        if (RaisedPile)
        {
            // Behind the top card, which is itself drawn behind the pile's labels.
            _body = new Control { Name = "PileBody", MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
            _body.Draw += () => RaisedStack.Draw(_body, this, Footprint, DisplayedAmount, RaisedStack.SleeveColor(Sleeve));
            AddChild(_body);
            ZIndex = 1;
        }
        if (CorruptedCount > 0) _back = SleeveArt.Back(Sleeve);
        UpdatePile();
        Resized += () => { PlacePreview(); _body?.QueueRedraw(); QueueRedraw(); };
        MouseEntered += QueueRedraw; MouseExited += QueueRedraw;
        FocusEntered += QueueRedraw; FocusExited += QueueRedraw;
    }

    internal void ChangePendingArrivals(int change, bool faceDown)
    {
        if (faceDown) _pendingCorruptedArrivals = Math.Max(0, _pendingCorruptedArrivals + change);
        else _pendingArrivals = Math.Max(0, _pendingArrivals + change);
        UpdatePile();
    }

    private void UpdatePile()
    {
        SetPreview(DisplayedFaceUp == 0 ? null : CardCatalog.Get(Cards[DisplayedFaceUp - 1]));
        PlacePreview();
        _body?.QueueRedraw();
        TooltipText = $"Memoria · {DisplayedFaceUp} boca arriba · {DisplayedFaceDown} boca abajo en el fondo. Pulsa para consultar.";
        QueueRedraw();
    }

    private void SetPreview(CardDefinition? card)
    {
        if (_preview?.Card == card) return;
        if (_preview != null)
        {
            RemoveChild(_preview);
            _preview.QueueFree();
            _preview = null;
        }
        if (card != null)
        {
            var preview = new HandCard { Name = "MemoryTopCard", Card = card, RelikarioId = RelikarioId, Disabled = true, ShowBehindParent = Board,
                Size = new(126, 184), Scale = new(0.2f, 0.2f), Position = new(6, 17) };
            preview.Ready += () => { preview.MouseFilter = MouseFilterEnum.Ignore; preview.SetProcess(false); };
            AddChild(preview); _preview = preview;
            PlacePreview();
        }
    }

    public override void _Draw()
    {
        if (Board) { DrawBoardPile(); return; }
        var amount = DisplayedAmount;
        BoardArt.Socket(this, new Rect2(Vector2.Zero, Size), Accent, amount == 0 ? 1 : 0.55f);
        BoardArt.CenterText(this, "MEMORIA", new(Size.X / 2, 13), 8, Accent);
        RelickVisuals.Interaction(this, new Rect2(Vector2.Zero, Size), false, false, IsHovered(), HasFocus());
        var thumb = CardRect;
        if (Tall && amount > 0)
        {
            var layers = Math.Min(3, amount - 1);
            RelickVisuals.Shadow(this, new Rect2(thumb.Position + new Vector2(layers * 1.5f, layers * 1.5f + 2), thumb.Size).Grow(-2), new Color(0, 0, 0, 0.55f), 7);
            for (var layer = layers; layer > 0; layer--)
                BoardArt.Plate(this, new Rect2(thumb.Position + new Vector2(layer * 1.5f, layer * 1.5f), thumb.Size),
                    new Color("1d2740"), new Color("5f6f96"), 3);
        }
        if (ShowBack)
        {
            DrawTextureRect(_back!, BoardArt.FitArtwork(_back!, thumb), false);
        }
        else if (_preview == null)
        {
            if (!Tall) BoardArt.Plate(this, thumb, new Color("0d1322"), Accent.Darkened(0.5f), 3);
            BoardArt.Icon(this, GameIcon.Memory, thumb.GetCenter(), Tall ? 20 : 17, BoardArt.Tint(Accent, Tall ? 0.16f : 0.8f));
        }
        if (!Tall)
        {
            BoardArt.CenterText(this, amount.ToString(), new(50, DisplayedFaceDown > 0 ? 37 : 43), amount < 100 ? 21 : 15, BoardArt.Ink);
            if (DisplayedFaceDown > 0) BoardArt.CenterText(this, $"{DisplayedFaceDown} ↓", new(50, 51), 10, Accent);
            return;
        }
        var text = CountText();
        var width = Math.Max(26, ThemeDB.FallbackFont.GetStringSize(text, fontSize: 14).X + 14);
        var chip = new Rect2((Size.X - width) / 2, Size.Y - 24, width, 19);
        BoardArt.Plate(this, chip, new Color(0, 0, 0, 0.5f), BoardArt.Tint(Accent, 0.45f), 4);
        BoardArt.CenterText(this, text, new(Size.X / 2, Size.Y - 10), 14, BoardArt.Ink);
    }

    // The top card is a child drawn behind this pile, so the socket would dim it: an occupied
    // pile only adds its name and count over the card.
    private void DrawBoardPile()
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        const string caption = "MEMORIA";
        var card = PresentedRect;
        var amount = DisplayedAmount;
        if (amount == 0)
        {
            BoardArt.Socket(this, bounds, Accent);
            BoardArt.CenterText(this, caption, new(Size.X / 2, 15), 9, BoardArt.Tint(Accent, 0.9f));
            BoardArt.Icon(this, GameIcon.Memory, Size / 2 + new Vector2(0, 5), 20, BoardArt.Tint(Accent, 0.16f));
        }
        else
        {
            if (ShowBack) DrawTextureRect(_back!, BoardArt.FitArtwork(_back!, card), false);
            BoardArt.Outline(this, card.Grow(2), BoardArt.Tint(RelickVisuals.Playmat.Frame, 0.55f), 6);
            var width = ThemeDB.FallbackFont.GetStringSize(caption, fontSize: 9).X + 14;
            var tab = RaisedPile
                ? new Rect2(card.GetCenter().X - width / 2, card.Position.Y - 4, width, 16)
                : new Rect2((Size.X - width) / 2, 0, width, 16);
            BoardArt.Plate(this, tab, new Color("0b1020"), BoardArt.Tint(Accent, 0.7f), 4);
            BoardArt.CenterText(this, caption, new(tab.GetCenter().X, tab.Position.Y + 12), 9, Accent);
        }
        var text = CountText();
        var chipWidth = Math.Max(28, ThemeDB.FallbackFont.GetStringSize(text, fontSize: 14).X + 14);
        var chip = RaisedPile && amount > 0
            ? new Rect2(card.GetCenter().X - chipWidth / 2, card.End.Y - 24, chipWidth, 20)
            : new Rect2((Size.X - chipWidth) / 2, Size.Y - 26, chipWidth, 20);
        BoardArt.Plate(this, chip, new Color("0b1020"), BoardArt.Tint(Accent, 0.6f), 4);
        BoardArt.CenterText(this, text, new(chip.GetCenter().X, chip.Position.Y + 15), 14, BoardArt.Ink);
        RelickVisuals.Interaction(this, bounds, false, false, IsHovered(), HasFocus());
    }

    private string CountText() => DisplayedFaceDown > 0 ? $"{DisplayedAmount} · {DisplayedFaceDown} ↓" : DisplayedAmount.ToString();
}
