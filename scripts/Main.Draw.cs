using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private record DrawRequest(int Player, int Slot, int BonusSource = -1, bool BonusDestination = false, string? CardId = null);
    private readonly Queue<DrawRequest> _drawQueue = new();
    private DrawRequest? _activeDraw;
    private Control? _drawOverlay;
    private Control? _flyingCard;
    private TextureRect? _flyingBack;
    private HandCard? _flyingFront;
    private double _drawTime;
    private int _drawLayoutFrames;
    private bool CardsInMotion => _activeDraw != null || _drawQueue.Count > 0;
    private const double DrawDuration = 0.62;

    public override void _Input(InputEvent @event)
    {
        if (CardsInMotion) GetViewport().SetInputAsHandled();
    }

    private void QueueCardDraw(int player, int slot)
    {
        _drawQueue.Enqueue(new(player, slot));
    }

    private ZoneCardButton? BonusTarget(int owner) =>
        (_root.FindChild($"BonusTrack{owner}", true, false) as ExchangeTrack)?.BonusButton;

    private Control? DrawTarget(DrawRequest request) => request.BonusDestination ? BonusTarget(request.Player) : _root.FindChild(
        request.Player == 0 ? $"HandCard{request.Slot}" : $"OpponentHandCard{request.Slot}", true, false) as Control;

    private static Rect2 BonusCardRect(Control zone) => new(zone.GlobalPosition + new Vector2((zone.Size.X - 28) / 2, 20), new(28, 39));

    private void PrepareDrawTargets()
    {
        if (!CardsInMotion) return;
        // Rendering rebuilds containers; retain the flight and hide the new destination nodes.
        if (_activeDraw != null && DrawTarget(_activeDraw) is { } active) active.Modulate = new(1, 1, 1, 0);
        foreach (var draw in _drawQueue)
            if (DrawTarget(draw) is { } target) target.Modulate = new(1, 1, 1, 0);
        _drawLayoutFrames = 2;
        if (_drawOverlay == null)
        {
            var layer = new CanvasLayer { Name = "CardDrawLayer", Layer = 10 };
            AddChild(layer);
            _drawOverlay = new Control { Name = "CardDrawOverlay", MouseFilter = MouseFilterEnum.Stop };
            layer.AddChild(_drawOverlay);
            _drawOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _drawOverlay.Show();
        // New cards remain visible even when the hand already requires scrolling.
        foreach (var player in _drawQueue.Where(d => !d.BonusDestination).Select(d => d.Player).Distinct())
        {
            var scroll = player == 0 ? _handScroll : _opponentHandScroll;
            scroll?.SetDeferred(ScrollContainer.PropertyName.ScrollHorizontal, int.MaxValue);
        }
    }

    private void UpdateDrawAnimations(double delta)
    {
        if (!CardsInMotion || _drawOverlay == null) return;
        if (_drawLayoutFrames-- > 0) return;
        if (_activeDraw == null)
        {
            _activeDraw = _drawQueue.Dequeue();
            _drawTime = 0;
            _flyingCard = new Control { Name = "DrawingCard", Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore };
            _drawOverlay.AddChild(_flyingCard);
            _flyingBack = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://assets/cards/card-back.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new(1, 7), Size = new(124, 175), MouseFilter = MouseFilterEnum.Ignore
            };
            _flyingCard.AddChild(_flyingBack);
            // Bonus identities are public; ordinary opponent draws remain hidden.
            var faceId = _activeDraw.CardId ?? (_activeDraw.Player == 0 && _activeDraw.Slot < _game.State.Players[0].Hand.Count
                ? _game.State.Players[0].Hand[_activeDraw.Slot] : null);
            if (faceId != null)
            {
                _flyingFront = new HandCard
                {
                    Card = CardCatalog.Get(faceId),
                    Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore, Visible = false
                };
                _flyingCard.AddChild(_flyingFront);
            }
        }
        var target = DrawTarget(_activeDraw);
        var deck = _root.FindChild($"DeckStack{_activeDraw.Player}", true, false) as DeckStack;
        if (target == null || deck == null) { FinishCardDraw(); return; }
        _drawTime += delta;
        var t = Mathf.Clamp((float)(_drawTime / DrawDuration), 0, 1);
        var eased = t * t * (3 - 2 * t);
        var sourceRect = _activeDraw.BonusSource >= 0 && BonusTarget(_activeDraw.BonusSource) is { } bonus
            ? BonusCardRect(bonus) : new Rect2(deck.GlobalPosition + deck.TopRect.Position, deck.TopRect.Size);
        var destination = _activeDraw.BonusDestination ? BonusCardRect(target) : target.GetGlobalRect();
        var center = sourceRect.GetCenter().Lerp(destination.GetCenter(), eased);
        center.Y -= Mathf.Sin(t * Mathf.Pi) * 66;
        var size = sourceRect.Size.Lerp(destination.Size, eased);
        var flip = 1f;
        if (_flyingFront != null)
        {
            var startsFaceUp = _activeDraw.BonusSource >= 0;
            var endsFaceUp = _activeDraw.BonusDestination || _activeDraw.Player == 0;
            var reveal = Mathf.Clamp((t - 0.55f) / 0.3f, 0, 1);
            var faceUp = startsFaceUp;
            if (startsFaceUp != endsFaceUp)
            {
                flip = Math.Max(0.035f, Math.Abs(Mathf.Cos(reveal * Mathf.Pi)));
                if (reveal >= 0.5f) faceUp = endsFaceUp;
            }
            _flyingFront.Visible = faceUp;
            _flyingBack!.Visible = !faceUp;
        }
        _flyingCard!.PivotOffset = _flyingCard.Size / 2;
        _flyingCard.Scale = size / _flyingCard.Size * new Vector2(flip, 1);
        _flyingCard.Position = center - _flyingCard.Size / 2;
        _flyingCard.Rotation = Mathf.Sin(t * Mathf.Pi) * (_activeDraw.Player == 0 ? -0.12f : 0.12f);
        if (t >= 1)
        {
            FinishCardDraw();
            if (!CardsInMotion) Render();
        }
    }

    private void FinishCardDraw()
    {
        if (_activeDraw != null && DrawTarget(_activeDraw) is { } target) target.Modulate = Colors.White;
        _flyingCard?.QueueFree();
        _flyingCard = null; _flyingBack = null; _flyingFront = null; _activeDraw = null;
        if (!CardsInMotion) _drawOverlay?.Hide();
    }

    private void ResetDrawAnimations()
    {
        _drawQueue.Clear();
        FinishCardDraw();
        _drawOverlay?.GetParent().QueueFree();
        _drawOverlay = null;
    }
}
