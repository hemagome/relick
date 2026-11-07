using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private sealed record Flight(DuelVisualEvent Event, Rect2? Origin = null, string? Face = null, double Duration = 0.45);
    private readonly Queue<Flight> _flights = new();
    private readonly List<(int Player, PortraitMotion Motion)> _pendingPortraits = [];
    private Control? _flight;
    private HandCard? _flightFront;
    private TextureRect? _flightBack;
    private Control? _flightTarget;
    private Flight? _activeFlight;
    private Rect2 _flightSource, _flightDestination;
    private double _flightTime;
    private int _flightLayoutFrames;
    private readonly List<TextureRect> _shuffleBacks = [];

    private Control? ZoneNode(int relative, CardZone zone, int slot) => zone switch
    {
        CardZone.Hand => _root?.FindChild((relative == 0 ? "HandCard" : "OpponentHandCard") + slot, true, false) as Control,
        CardZone.Memory or CardZone.Corrupted => _root?.FindChild((zone == CardZone.Corrupted ? "CorruptedMemory" : "Memory") + "Button" + relative, true, false) as Control,
        CardZone.Bonus => (_root?.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack)?.BonusButton,
        CardZone.Exchange => (_root?.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack)?.ExchangeButtons.ElementAtOrDefault(slot),
        CardZone.Equipment => _root?.FindChild("EquipmentZone" + relative, true, false) as Control,
        CardZone.Attached => _root?.FindChild("AttachmentStack" + relative, true, false) as Control,
        _ => null
    };
    private static Rect2 ZoneRect(Control control) => control switch
    {
        MemoryZoneButton pile => new(pile.GlobalPosition + pile.CardRect.Position, pile.CardRect.Size),
        ZoneCardButton zone => new(zone.GlobalPosition + new Vector2((zone.Size.X - 28) / 2, 20), new(28, 39)),
        _ => control.GetGlobalRect()
    };

    private Dictionary<DuelVisualEvent, Flight> CaptureOrigins(DuelSnapshot current, DuelSnapshot? previous)
    {
        var result = new Dictionary<DuelVisualEvent, Flight>();
        if (previous == null || _root == null) return result;
        foreach (var cue in current.Visuals.Where(v => v.Kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer))
        {
            var relative = cue.Player == current.MySeat ? 0 : 1;
            var source = cue.Kind is DuelVisualKind.Return or DuelVisualKind.HandTransfer ? CardZone.Hand : cue.Kind == DuelVisualKind.BonusClaim ? CardZone.Bonus : cue.Source;
            var slot = cue.Kind == DuelVisualKind.HandTransfer ? 0 : cue.Value;
            var node = ZoneNode(relative, source, slot);
            var player = previous.Players[cue.Player];
            string? id = source switch
            {
                CardZone.Hand => player.Hand.ElementAtOrDefault(slot), CardZone.Bonus => player.Bonus,
                CardZone.Equipment => player.Equipment, CardZone.Attached => player.Attached.ElementAtOrDefault(cue.Value),
                CardZone.Exchange => player.Exchange.ElementAtOrDefault(cue.Value), CardZone.Memory => player.Memory.ElementAtOrDefault(cue.Value), _ => null
            };
            if (cue.Kind == DuelVisualKind.HandTransfer) id = null; // One face-down packet per hand, regardless of its size.
            var origin = node != null && RectVisible(node, ZoneRect(node)) ? ZoneRect(node) : (Rect2?)null;
            if (source == CardZone.Activation && _root.FindChild("CombatCenter", true, false) is Control center)
            {
                var activation = new Rect2(center.GetGlobalRect().GetCenter() - new Vector2(27, 38.5f), new(54, 77));
                origin = RectVisible(center, activation) ? activation : null;
            }
            result[cue] = new Flight(cue, origin, id);
        }
        return result;
    }

    private void Animate(List<DuelVisualEvent> events, Dictionary<DuelVisualEvent, Flight> origins)
    {
        if (events.Count > 0) PlayedVisualRevisions++;
        foreach (var cue in events)
        {
            switch (cue.Kind)
            {
                case DuelVisualKind.Die: _die.QueueRoll(cue.Value, cue.Caption); break;
                case DuelVisualKind.Attack: _pendingPortraits.Add((cue.Player, PortraitMotion.Attack)); break;
                case DuelVisualKind.Hit:
                    _pendingPortraits.Add((cue.Player, cue.AttackDie switch
                    {
                        5 => PortraitMotion.HitNormal, 6 => PortraitMotion.HitCritical, _ => PortraitMotion.Hit
                    }));
                    break;
                case DuelVisualKind.Heal when cue.Value > 0: _pendingPortraits.Add((cue.Player, PortraitMotion.Heal)); break;
                case DuelVisualKind.Draw:
                case DuelVisualKind.Shuffle: _flights.Enqueue(new(cue)); break;
                case DuelVisualKind.BonusPlace: _flights.Enqueue(new(cue, Face: _snapshot!.Players[cue.Player].Bonus)); break;
                case DuelVisualKind.Pile:
                case DuelVisualKind.BonusClaim:
                case DuelVisualKind.Return:
                case DuelVisualKind.HandTransfer:
                    if (origins.TryGetValue(cue, out var flight)) _flights.Enqueue(flight);
                    break;
            }
        }
        _flightLayoutFrames = 2;
        _lastDisabled = null;
        PlayPendingPortraits();
    }

    private void PlayPendingPortraits()
    {
        if (_die.IsBusy || _flight != null || _flights.Count > 0 || _snapshot == null) return;
        foreach (var (player, motion) in _pendingPortraits) _portraits[player == _snapshot.MySeat ? 0 : 1].Play(motion);
        _pendingPortraits.Clear();
    }

    private void UpdateFlights(double delta)
    {
        if (_flightLayoutFrames > 0) { _flightLayoutFrames--; return; }
        if (_flight == null && _flights.TryDequeue(out var next))
        {
            var own = next.Event.Player == _snapshot!.MySeat;
            var relative = own ? 0 : 1;
            var deck = _root!.FindChild("DeckStack" + relative, true, false) as DeckStack;
            if (deck == null) return;
            var deckRect = new Rect2(deck.GlobalPosition + deck.TopRect.Position, deck.TopRect.Size);
            var returning = next.Event.Kind == DuelVisualKind.Return;
            var kind = next.Event.Kind;
            var target = kind switch
            {
                DuelVisualKind.Return or DuelVisualKind.Shuffle => null,
                DuelVisualKind.Pile => ZoneNode(relative, next.Event.Destination, -1),
                DuelVisualKind.BonusPlace => ZoneNode(relative, CardZone.Bonus, -1),
                DuelVisualKind.BonusClaim => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, next.Event.Value),
                DuelVisualKind.HandTransfer => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, 0),
                _ => ZoneNode(relative, CardZone.Hand, next.Event.Value)
            };
            var fromPrevious = kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer;
            var source = fromPrevious ? next.Origin : deckRect;
            Rect2? destination = returning || kind == DuelVisualKind.Shuffle ? deckRect : target == null ? null : ZoneRect(target);
            var bounds = new Rect2(Vector2.Zero, Size);
            // Never draw through clipped panels/docks when a small-screen zone is offscreen.
            if (source == null || destination == null || !bounds.Encloses(source.Value) || !bounds.Encloses(destination.Value))
            { if (target != null) target.Modulate = Colors.White; return; }
            if ((!fromPrevious || returning) && !RectVisible(deck, deckRect) || target != null && !RectVisible(target, destination.Value))
            { if (target != null) target.Modulate = Colors.White; return; }
            _flightSource = source.Value; _flightDestination = destination.Value;
            _activeFlight = next; _flightTarget = target;
            if (target != null) target.Modulate = new(1, 1, 1, 0);
            _flight = new Control { Name = "NetworkCardFlight", Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 }; AddChild(_flight);
            TextureRect Back() => new() { Texture = GD.Load<Texture2D>("res://assets/cards/card-back.png"),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Position = new(1, 7), Size = new(124, 175), MouseFilter = MouseFilterEnum.Ignore };
            if (kind == DuelVisualKind.Shuffle)
            {
                for (var i = 0; i < 4; i++) { var back = Back(); _flight.AddChild(back); _shuffleBacks.Add(back); }
                _flightTime = 0; return;
            }
            _flightBack = Back(); _flight.AddChild(_flightBack);
            var hand = _snapshot.Players[_snapshot.MySeat].Hand;
            var id = kind == DuelVisualKind.Draw ? own && next.Event.Value < hand.Length ? hand[next.Event.Value] : null : next.Face;
            if (id != null)
            {
                _flightFront = new HandCard { Card = CardCatalog.Get(id), Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore };
                _flight.AddChild(_flightFront); _flightFront.SetProcess(false);
            }
            _flightTime = 0;
        }
        if (_flight != null)
        {
            _flightTime += delta;
            if (_activeFlight!.Event.Kind == DuelVisualKind.Shuffle)
            {
                var progress = Math.Clamp((float)(_flightTime / 0.72), 0, 1);
                for (var i = 0; i < _shuffleBacks.Count; i++) ArenaMotion.ShuffleCard(_shuffleBacks[i], _flightSource, progress, i);
                if (progress >= 1) FinishFlight();
                return;
            }
            var t = Math.Clamp((float)(_flightTime / _activeFlight!.Duration), 0, 1);
            var returning = _activeFlight!.Event.Kind == DuelVisualKind.Return;
            var reveal = Math.Clamp((t - 0.55f) / 0.3f, 0, 1);
            var startsUp = _activeFlight.Event.Kind is not (DuelVisualKind.Draw or DuelVisualKind.BonusPlace) && _activeFlight.Event.Source != CardZone.Corrupted;
            var endsUp = !returning && !(_activeFlight.Event.Kind == DuelVisualKind.Pile && _activeFlight.Event.Destination == CardZone.Corrupted);
            var flip = _flightFront == null || startsUp == endsUp ? 1 : Math.Max(0.035f, Math.Abs(Mathf.Cos(reveal * Mathf.Pi)));
            if (_flightFront != null)
            {
                _flightFront.Visible = reveal < 0.5f ? startsUp : endsUp;
                _flightBack!.Visible = !_flightFront.Visible;
            }
            ArenaMotion.PositionCard(_flight, _flightSource, _flightDestination, t, _activeFlight.Event.Player == _snapshot!.MySeat ? 0 : 1, flip);
            if (t >= 1) FinishFlight();
        }
        PlayPendingPortraits();
    }
    private void FinishFlight()
    {
        if (GodotObject.IsInstanceValid(_flightTarget)) _flightTarget!.Modulate = Colors.White;
        _shuffleBacks.Clear(); _flight?.QueueFree(); _flight = null; _flightFront = null; _flightBack = null; _flightTarget = null; _activeFlight = null;
    }
    private static bool RectVisible(Control anchor, Rect2 rect)
    {
        if (!anchor.IsVisibleInTree()) return false;
        for (Node? parent = anchor; parent != null; parent = parent.GetParent())
            if (parent is Control { ClipContents: true } clip && !clip.GetGlobalRect().Encloses(rect)) return false;
        return true;
    }
    private void ResetAnimations()
    {
        SetQueuedDestinationsOpacity(1);
        _flights.Clear(); _pendingPortraits.Clear(); FinishFlight(); _die.Value = 0;
        foreach (var portrait in _portraits) portrait.ReturnToRest();
    }
}
