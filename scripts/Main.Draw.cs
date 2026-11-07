using Godot;
using Relick.Core;

namespace Relick;

public partial class Main
{
    private record DrawRequest(int Player, int Slot, int BonusSource = -1, bool BonusDestination = false,
        string? CardId = null, bool ReturnToDeck = false, bool Shuffle = false, Rect2? Origin = null, double Duration = 0.62,
        bool InitialDeal = false, CardZone? PileSource = null, CardZone? PileDestination = null);
    private readonly Queue<DrawRequest> _drawQueue = new();
    private DrawRequest? _activeDraw;
    private Control? _drawOverlay;
    private Control? _flyingCard;
    private TextureRect? _flyingBack;
    private HandCard? _flyingFront;
    private DrawRequest? _pairedDraw;
    private Control? _pairedFlyingCard;
    private double _drawTime;
    private int _drawLayoutFrames;
    private bool CardsInMotion => _activeDraw != null || _drawQueue.Count > 0;
    private Control? _returnHandStage;
    private int _returnHandPlayer = -1;
    private readonly Dictionary<int, Control> _returnHandCards = new();
    private readonly List<TextureRect> _shuffleCards = new();

    public override void _Input(InputEvent @event)
    {
        if (_onlineLobby == null && CardsInMotion) GetViewport().SetInputAsHandled();
    }

    private void QueueCardDraw(int player, int slot)
    {
        _drawQueue.Enqueue(new(player, slot));
    }

    private void QueueInitialHands()
    {
        for (var slot = 0; slot < _game.State.Players.Max(p => p.Hand.Count); slot++)
            for (var player = 0; player < 2; player++)
                if (slot < _game.State.Players[player].Hand.Count)
                    _drawQueue.Enqueue(new(player, slot, Duration: 0.28, InitialDeal: true));
    }

    private TextureRect CardBack() => new()
    {
        Texture = GD.Load<Texture2D>("res://assets/cards/card-back.png"),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private void EnsureDrawOverlay()
    {
        if (_drawOverlay == null)
        {
            var layer = new CanvasLayer { Name = "CardDrawLayer", Layer = 10 };
            AddChild(layer);
            _drawOverlay = new Control { Name = "CardDrawOverlay", MouseFilter = MouseFilterEnum.Stop };
            layer.AddChild(_drawOverlay);
            _drawOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        _drawOverlay.Visible = _onlineLobby == null;
    }

    private void QueueCardReturn(int player, int slot, string id)
    {
        PreserveHandForDeparture(player);
        var scroll = player == 0 ? _handScroll : _opponentHandScroll;
        var origin = DrawTarget(new(player, slot))?.GetGlobalRect();
        if (origin is { } rect && scroll != null)
        {
            var bounds = scroll.GetGlobalRect();
            rect.Position = new(Mathf.Clamp(rect.Position.X, bounds.Position.X, Math.Max(bounds.Position.X, bounds.End.X - rect.Size.X)), rect.Position.Y);
            origin = rect;
        }
        _drawQueue.Enqueue(new(player, slot, CardId: player == 0 ? id : null, ReturnToDeck: true, Origin: origin, Duration: 0.4));
    }

    private void PreserveHandForDeparture(int player)
    {
        EnsureDrawOverlay();
        var scroll = player == 0 ? _handScroll : _opponentHandScroll;
        // Preserve the old hand until its selected cards have left. The rules already
        // contain the replacement hand when Render rebuilds the board.
        if (_returnHandStage == null && scroll != null)
        {
            _returnHandPlayer = player;
            _returnHandStage = new Control
            {
                Position = scroll.GlobalPosition, Size = scroll.Size, ClipContents = true,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _drawOverlay!.AddChild(_returnHandStage);
            for (var i = 0; i < _game.State.Players[player].Hand.Count; i++)
            {
                if (DrawTarget(new(player, i)) is not { } original) continue;
                Control copy = player == 0
                    ? new HandCard { Card = CardCatalog.Get(_game.State.Players[player].Hand[i]), MouseFilter = MouseFilterEnum.Ignore }
                    : CardBack();
                copy.Position = original.GlobalPosition - scroll.GlobalPosition;
                copy.Size = original.Size;
                _returnHandStage.AddChild(copy);
                _returnHandCards[i] = copy;
            }
        }
    }

    private ZoneCardButton? BonusTarget(int owner) =>
        (_root.FindChild($"BonusTrack{owner}", true, false) as ExchangeTrack)?.BonusButton;

    private Control? DrawTarget(DrawRequest request) => request.PileDestination is { } zone ? PileTarget(request.Player, zone) : request.ReturnToDeck || request.Shuffle ? null : request.BonusDestination ? BonusTarget(request.Player) : _root.FindChild(
        request.Player == 0 ? $"HandCard{request.Slot}" : $"OpponentHandCard{request.Slot}", true, false) as Control;

    private static Rect2 BonusCardRect(Control zone) => new(zone.GlobalPosition + new Vector2((zone.Size.X - 28) / 2, 20), new(28, 39));

    private void PrepareDrawTargets()
    {
        if (!CardsInMotion) return;
        // Rendering rebuilds containers; retain the flight and hide the new destination nodes.
        if (_activeDraw != null && DrawTarget(_activeDraw) is { } active) active.Modulate = new(1, 1, 1, 0);
        if (_pairedDraw != null && DrawTarget(_pairedDraw) is { } paired) paired.Modulate = new(1, 1, 1, 0);
        foreach (var draw in _drawQueue)
            if (DrawTarget(draw) is { } target) target.Modulate = new(1, 1, 1, 0);
        _drawLayoutFrames = 2;
        EnsureDrawOverlay();
        if (_returnHandStage != null)
            for (var slot = 0; slot < _game.State.Players[_returnHandPlayer].Hand.Count; slot++)
                if (DrawTarget(new(_returnHandPlayer, slot)) is { } card) card.Modulate = new(1, 1, 1, 0);
        // New cards remain visible even when the hand already requires scrolling.
        foreach (var player in _drawQueue.Where(d => !d.BonusDestination && !d.ReturnToDeck && !d.Shuffle && d.PileDestination == null).Select(d => d.Player).Distinct())
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
            // Only the initial deal shares a clock: both players receive each slot together.
            if (_activeDraw is { InitialDeal: true, Player: 0 } &&
                _drawQueue.TryPeek(out var next) && next is { InitialDeal: true, Player: 1 } && next.Slot == _activeDraw.Slot)
            {
                _pairedDraw = _drawQueue.Dequeue();
                _pairedFlyingCard = new Control { Name = "OpponentOpeningCard", Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore };
                _drawOverlay.AddChild(_pairedFlyingCard);
                var back = CardBack();
                back.Position = new(1, 7); back.Size = new(124, 175);
                _pairedFlyingCard.AddChild(back);
            }
            if (LeavesHand(_activeDraw) && _activeDraw.Player == _returnHandPlayer && _returnHandCards.Remove(_activeDraw.Slot, out var returning)) returning.QueueFree();
            if (_activeDraw.Shuffle)
            {
                for (var i = 0; i < 4; i++)
                {
                    var back = CardBack();
                    _drawOverlay.AddChild(back);
                    _shuffleCards.Add(back);
                }
            }
            else
            {
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
                var faceId = _activeDraw.CardId ?? (!_activeDraw.ReturnToDeck && _activeDraw.Player == 0 && _activeDraw.Slot < _game.State.Players[0].Hand.Count
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
        }
        var target = DrawTarget(_activeDraw);
        var deck = _root.FindChild($"DeckStack{_activeDraw.Player}", true, false) as DeckStack;
        if (deck == null || (target == null && !_activeDraw.ReturnToDeck && !_activeDraw.Shuffle)) { FinishCardDraw(); return; }
        _drawTime += delta;
        var t = Mathf.Clamp((float)(_drawTime / _activeDraw.Duration), 0, 1);
        var deckRect = new Rect2(deck.GlobalPosition + deck.TopRect.Position, deck.TopRect.Size);
        if (_activeDraw.Shuffle)
        {
            for (var i = 0; i < _shuffleCards.Count; i++)
            {
                ArenaMotion.ShuffleCard(_shuffleCards[i], deckRect, t, i);
            }
            if (t >= 1) { FinishCardDraw(); if (!CardsInMotion) Render(); }
            return;
        }
        var sourceRect = _activeDraw.PileDestination != null ? _activeDraw.Origin ?? deckRect : _activeDraw.BonusSource >= 0 && BonusTarget(_activeDraw.BonusSource) is { } bonus
            ? BonusCardRect(bonus) : _activeDraw.ReturnToDeck ? _activeDraw.Origin ?? deckRect : deckRect;
        var destination = target is MemoryZoneButton pile ? PileCardRect(pile) : _activeDraw.ReturnToDeck ? deckRect : _activeDraw.BonusDestination ? BonusCardRect(target!) : target!.GetGlobalRect();
        var flip = 1f;
        if (_flyingFront != null)
        {
            var startsFaceUp = _activeDraw.PileDestination != null ? _activeDraw.PileSource != CardZone.Corrupted : _activeDraw.BonusSource >= 0 || _activeDraw.ReturnToDeck;
            var endsFaceUp = _activeDraw.PileDestination is { } destinationZone ? destinationZone != CardZone.Corrupted : !_activeDraw.ReturnToDeck && (_activeDraw.BonusDestination || _activeDraw.Player == 0);
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
        PositionFlyingCard(_flyingCard!, sourceRect, destination, t, _activeDraw.Player, flip);
        if (_pairedDraw != null && _pairedFlyingCard != null && DrawTarget(_pairedDraw) is { } pairedTarget &&
            _root.FindChild($"DeckStack{_pairedDraw.Player}", true, false) is DeckStack pairedDeck)
            PositionFlyingCard(_pairedFlyingCard, new(pairedDeck.GlobalPosition + pairedDeck.TopRect.Position, pairedDeck.TopRect.Size),
                pairedTarget.GetGlobalRect(), t, _pairedDraw.Player);
        if (t >= 1)
        {
            FinishCardDraw();
            if (!CardsInMotion) Render();
        }
    }

    private static void PositionFlyingCard(Control card, Rect2 source, Rect2 destination, float t, int player, float flip = 1)
        => ArenaMotion.PositionCard(card, source, destination, t, player, flip);

    private void FinishCardDraw()
    {
        if (_activeDraw != null && DrawTarget(_activeDraw) is { } target &&
            !_drawQueue.Any(d => d.PileDestination != null && d.Player == _activeDraw.Player && d.PileDestination == _activeDraw.PileDestination))
            target.Modulate = Colors.White;
        if (_pairedDraw != null && DrawTarget(_pairedDraw) is { } pairedTarget) pairedTarget.Modulate = Colors.White;
        _pairedFlyingCard?.QueueFree();
        _pairedFlyingCard = null; _pairedDraw = null;
        _flyingCard?.QueueFree();
        foreach (var card in _shuffleCards) card.QueueFree();
        _shuffleCards.Clear();
        if (_activeDraw != null && LeavesHand(_activeDraw) && _activeDraw.Player == _returnHandPlayer && !_drawQueue.Any(d => LeavesHand(d) && d.Player == _returnHandPlayer))
        {
            ClearReturnHand();
            for (var player = 0; player < 2; player++)
                for (var slot = 0; slot < _game.State.Players[player].Hand.Count; slot++)
                    if (DrawTarget(new(player, slot)) is { } card) card.Modulate = Colors.White;
            foreach (var draw in _drawQueue)
                if (DrawTarget(draw) is { } card) card.Modulate = new(1, 1, 1, 0);
        }
        _flyingCard = null; _flyingBack = null; _flyingFront = null; _activeDraw = null;
        if (!CardsInMotion) _drawOverlay?.Hide();
    }

    private void ResetDrawAnimations()
    {
        _drawQueue.Clear();
        FinishCardDraw();
        ClearReturnHand();
        _drawOverlay?.GetParent().QueueFree();
        _drawOverlay = null;
    }

    private void ClearReturnHand()
    {
        _returnHandStage?.QueueFree();
        _returnHandStage = null;
        _returnHandCards.Clear();
        _returnHandPlayer = -1;
    }
}
