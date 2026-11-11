using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private sealed record Flight(DuelVisualEvent Event, Rect2? Origin = null, string? Face = null, double Duration = 0.45, float OriginRotation = 0)
    {
        public bool IsSkillLesson => Event.Kind == DuelVisualKind.Pile && Event.Destination == CardZone.Skill;
    }
    private readonly Queue<Flight> _flights = new();
    private readonly List<(int Player, PortraitMotion Motion)> _pendingPortraits = [];
    private Control? _flight;
    private HandCard? _flightFront;
    private TextureRect? _flightBack;
    private Control? _flightTarget;
    private Flight? _activeFlight;
    private DuelVisualEvent? _skillLessonRoll;
    private bool _skillLessonPreview;
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
        CardZone.EquipmentCover => _root?.FindChild("EquipmentCoverZone" + relative, true, false) as Control,
        CardZone.Skill => _root?.FindChild($"Skill{relative}_{_snapshot!.Players[relative == 0 ? _snapshot.MySeat : 1 - _snapshot.MySeat].SkillPosition(slot)}", true, false) as Control,
        CardZone.Attached => _root?.FindChild("AttachmentStack" + relative, true, false) as Control,
        _ => null
    };
    private static Rect2 ZoneRect(Control control) => control switch
    {
        MemoryZoneButton pile => new(pile.GlobalPosition + pile.CardRect.Position, pile.CardRect.Size),
        ZoneCardButton zone => new(zone.GlobalPosition + zone.CardRect.Position, zone.CardRect.Size),
        AttachmentStack attached => new(attached.GlobalPosition + attached.CardRect.Position, attached.CardRect.Size),
        HandCard { InFan: true } card => new(card.GetGlobalTransform() * (card.Size / 2) - card.Size / 2, card.Size),
        _ => control.GetGlobalRect()
    };

    private Dictionary<DuelVisualEvent, Flight> CaptureOrigins(DuelSnapshot current, DuelSnapshot? previous)
    {
        var result = new Dictionary<DuelVisualEvent, Flight>();
        if (previous == null || _root == null) return result;
        foreach (var cue in current.Visuals.Where(v => v.Kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer or DuelVisualKind.MemoryClaim or DuelVisualKind.HandDiscard))
        {
            var relative = cue.Player == current.MySeat ? 0 : 1;
            var source = cue.Kind is DuelVisualKind.Return or DuelVisualKind.HandTransfer ? CardZone.Hand : cue.Kind == DuelVisualKind.BonusClaim ? CardZone.Bonus : cue.Source;
            var slot = cue.Kind is DuelVisualKind.HandTransfer or DuelVisualKind.HandDiscard ? 0 : cue.Kind == DuelVisualKind.MemoryClaim ? cue.SourceIndex : cue.Value;
            var node = ZoneNode(relative, source, slot);
            var player = previous.Players[cue.Player];
            string? id = source switch
            {
                CardZone.Hand => player.Hand.ElementAtOrDefault(slot), CardZone.Bonus => player.Bonus,
                CardZone.Equipment => player.Equipment, CardZone.EquipmentCover => player.EquipmentCovers?.ElementAtOrDefault(cue.Value), CardZone.Skill => player.Skills.ElementAtOrDefault(cue.Value), CardZone.Attached => player.Attached.ElementAtOrDefault(cue.Value),
                CardZone.Exchange => player.Exchange.ElementAtOrDefault(cue.Value), CardZone.Memory => player.Memory.ElementAtOrDefault(slot), _ => null
            };
            if (cue.Kind is DuelVisualKind.HandTransfer or DuelVisualKind.HandDiscard) id = null; // One face-down packet per hand, regardless of its size.
            var origin = node != null && RectVisible(node, ZoneRect(node)) ? ZoneRect(node) : (Rect2?)null;
            if (source == CardZone.Activation && _root.FindChild("CombatCenter", true, false) is Control center)
            {
                var activation = new Rect2(center.GetGlobalRect().GetCenter() - new Vector2(27, 38.5f), new(54, 77));
                origin = RectVisible(center, activation) ? activation : null;
            }
            result[cue] = new Flight(cue, origin, id, OriginRotation: node?.Rotation ?? 0);
        }
        return result;
    }

    private Flight? CaptureSkillLesson(DuelSnapshot current, DuelSnapshot previous)
    {
        var actor = previous.CurrentPlayer;
        if (previous.Phase != Phase.Combat || current.Turn != previous.Turn || current.CurrentPlayer != actor) return null;
        var before = previous.Players[actor];
        var after = current.Players[actor];
        if (after.Skills.Length != before.Skills.Length + 1 || after.HandCount != before.HandCount - 1) return null;

        // Learning appends a skill. The public board already identifies it and its position.
        var skillIndex = before.Skills.Length;
        var skill = after.Skills[skillIndex];
        var own = actor == current.MySeat;
        Control? source;
        if (own)
        {
            bool WasRemoved(int i) => before.Hand.ElementAtOrDefault(i) == skill &&
                before.Hand.Where((_, j) => j != i).SequenceEqual(after.Hand);
            var index = _selected.FirstOrDefault(WasRemoved, -1);
            if (index < 0)
                index = Enumerable.Range(0, before.Hand.Length).FirstOrDefault(WasRemoved, -1);
            source = ZoneNode(0, CardZone.Hand, index);
        }
        else
        {
            source = _root?.FindChildren("OpponentHandCard*", "Button", true, false).OfType<OpponentHandCard>()
                .FirstOrDefault(card => card.IsFaceDown && RectVisible(card, ZoneRect(card)));
        }

        var origin = source != null && RectVisible(source, ZoneRect(source)) ? ZoneRect(source) : (Rect2?)null;
        var rotation = origin != null ? source!.Rotation : 0;
        if (origin == null)
        {
            var hand = own ? _hand : _root?.FindChild("OpponentHandScroll", true, false) as Control;
            if (hand != null)
                origin = new Rect2(hand.GetGlobalRect().GetCenter() - new Vector2(27, 38), new(54, 76));
        }
        var cue = new DuelVisualEvent { Kind = DuelVisualKind.Pile, Player = actor, Value = skillIndex,
            Source = CardZone.Hand, Destination = CardZone.Skill };
        return new Flight(cue, origin, skill, Duration: 0.66, OriginRotation: rotation);
    }

    private void Animate(List<DuelVisualEvent> events, Dictionary<DuelVisualEvent, Flight> origins, Flight? lesson = null)
    {
        if (events.Count > 0 || lesson != null) PlayedVisualRevisions++;
        if (lesson != null) _flights.Enqueue(lesson);
        foreach (var cue in events)
        {
            switch (cue.Kind)
            {
                case DuelVisualKind.Die when lesson != null: _skillLessonRoll = cue; break;
                case DuelVisualKind.Die: _die.QueueRoll(cue.Value, cue.Caption); break;
                case DuelVisualKind.DieAdjustment: _die.ShowAdjustedResult(cue.Value, cue.Caption); break;
                case DuelVisualKind.Attack: _pendingPortraits.Add((cue.Player, PortraitMotion.Attack)); break;
                case DuelVisualKind.Hit:
                    _pendingPortraits.Add((cue.Player, cue.NormalFromMiss ? PortraitMotion.HitNormal : cue.AttackDie switch
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
                case DuelVisualKind.MemoryClaim:
                case DuelVisualKind.Return:
                case DuelVisualKind.HandTransfer:
                case DuelVisualKind.HandDiscard:
                    if (origins.TryGetValue(cue, out var flight)) _flights.Enqueue(flight);
                    break;
            }
        }
        // The snapshot already contains these cards. Reveal each destination on arrival.
        SetQueuedDestinationsPending(true);
        _flightLayoutFrames = 2;
        _lastDisabled = null;
        PlayPendingPortraits();
    }

    private void PlayPendingPortraits()
    {
        if (_die.IsBusy || _flight != null || _flights.Count > 0 || _openingInitiative != null || _snapshot == null) return;
        foreach (var (player, motion) in _pendingPortraits)
        {
            _portraits[player == _snapshot.MySeat ? 0 : 1].Play(motion);
            if (motion == PortraitMotion.Attack) PlaySound(SoundCue.Attack);
            if (motion == PortraitMotion.Heal) PlaySound(SoundCue.Heal);
        }
        _pendingPortraits.Clear();
    }

    private (Rect2 Source, Rect2 Destination) SkillLessonPath(Flight lesson, Control? target)
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        var slot = target?.GetGlobalRect() ?? default;
        _skillLessonPreview = target == null || !bounds.Encloses(slot) || !RectVisible(target, slot);
        if (_skillLessonPreview)
        {
            var board = _arenaScroll?.GetGlobalRect().Intersection(bounds) ?? bounds;
            var preview = SkillTile.CardSize * 0.9f;
            slot = new Rect2(board.GetCenter() - preview / 2, preview);
        }
        var source = lesson.Origin ?? new Rect2(new Vector2(Size.X / 2 - 27, Size.Y - 90), new(54, 76));
        var halfSize = source.Size / 2;
        source.Position = source.GetCenter().Clamp(bounds.Position + halfSize, bounds.End - halfSize) - halfSize;
        // The flying card uses the portrait hand artwork. Rotate its footprint into the skill tile.
        var portrait = new Vector2(slot.Size.Y, slot.Size.X);
        return (source, new Rect2(slot.GetCenter() - portrait / 2, portrait));
    }

    private void UpdateFlights(double delta)
    {
        if (_flightLayoutFrames > 0) { _flightLayoutFrames--; return; }
        if (_flight == null && _flights.Count == 0 && _openingInitiative != null)
        { UpdateOpeningInitiative(delta); return; }
        if (_flight == null && _flights.TryDequeue(out var next))
        {
            var own = next.Event.Player == _snapshot!.MySeat;
            var relative = own ? 0 : 1;
            var returning = next.Event.Kind == DuelVisualKind.Return;
            var kind = next.Event.Kind;
            var target = kind switch
            {
                DuelVisualKind.Return or DuelVisualKind.Shuffle => null,
                DuelVisualKind.Pile when next.IsSkillLesson => ZoneNode(relative, CardZone.Skill, next.Event.Value),
                DuelVisualKind.Pile => ZoneNode(relative, next.Event.Destination, -1),
                DuelVisualKind.BonusPlace => ZoneNode(relative, CardZone.Bonus, -1),
                DuelVisualKind.BonusClaim or DuelVisualKind.MemoryClaim => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, next.Event.Value),
                DuelVisualKind.HandTransfer => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, 0),
                DuelVisualKind.HandDiscard => ZoneNode(relative, CardZone.Memory, -1),
                _ => ZoneNode(relative, CardZone.Hand, next.Event.Value)
            };
            if (next.IsSkillLesson) (_flightSource, _flightDestination) = SkillLessonPath(next, target);
            else
            {
                var deck = _root!.FindChild("DeckStack" + relative, true, false) as DeckStack;
                if (deck == null) { SetDestinationPending(target, false); return; }
                var deckRect = new Rect2(deck.GlobalPosition + deck.TopRect.Position, deck.TopRect.Size);
                if (kind == DuelVisualKind.Draw) PlaySound(SoundCue.CardDraw);
                if (kind == DuelVisualKind.Shuffle) PlaySound(SoundCue.CardShuffle);
                var fromPrevious = kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer or DuelVisualKind.MemoryClaim or DuelVisualKind.HandDiscard;
                var source = fromPrevious ? next.Origin : deckRect;
                Rect2? destination = returning || kind == DuelVisualKind.Shuffle ? deckRect : target == null ? null : ZoneRect(target);
                var bounds = new Rect2(Vector2.Zero, Size);
                // Never draw through clipped panels/docks when a small-screen zone is offscreen.
                if (source == null || destination == null || !bounds.Encloses(source.Value) || !bounds.Encloses(destination.Value) ||
                    (!fromPrevious || returning) && !RectVisible(deck, deckRect) || target != null && !RectVisible(target, destination.Value))
                { SetDestinationPending(target, false); return; }
                _flightSource = source.Value; _flightDestination = destination.Value;
            }
            _activeFlight = next; _flightTarget = target;
            SetDestinationPending(target, true);
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
            var lesson = _activeFlight.IsSkillLesson;
            var relative = _activeFlight.Event.Player == _snapshot!.MySeat ? 0 : 1;
            var t = Math.Clamp((float)(_flightTime / _activeFlight.Duration), 0, 1);
            var returning = _activeFlight.Event.Kind == DuelVisualKind.Return;
            var reveal = Math.Clamp((t - (lesson ? 0.28f : 0.55f)) / 0.3f, 0, 1);
            var startsUp = _activeFlight.Event.Kind is not (DuelVisualKind.Draw or DuelVisualKind.BonusPlace) &&
                _activeFlight.Event.Source != CardZone.Corrupted && (!lesson || relative == 0);
            var endsUp = !returning && !(_activeFlight.Event.Kind == DuelVisualKind.Pile && _activeFlight.Event.Destination == CardZone.Corrupted);
            var flip = _flightFront == null || startsUp == endsUp ? 1 : Math.Max(0.035f, Math.Abs(Mathf.Cos(reveal * Mathf.Pi)));
            if (_flightFront != null)
            {
                _flightFront.Visible = reveal < 0.5f ? startsUp : endsUp;
                _flightBack!.Visible = !_flightFront.Visible;
            }
            ArenaMotion.PositionCard(_flight, _flightSource, _flightDestination, t, relative, flip,
                _activeFlight.OriginRotation, lesson ? Mathf.Pi / 2 : _flightTarget?.Rotation ?? 0);
            var fade = _skillLessonPreview ? Math.Clamp((float)(_flightTime - _activeFlight.Duration - 0.24) / 0.2f, 0, 1) : t;
            if (_skillLessonPreview) _flight.Modulate = new Color(1, 1, 1, 1 - fade);
            if (fade >= 1)
            {
                FinishFlight();
                if (lesson && _skillLessonRoll is { } roll)
                {
                    _skillLessonRoll = null;
                    _die.QueueRoll(roll.Value, roll.Caption);
                }
            }
        }
        PlayPendingPortraits();
    }
    private void FinishFlight()
    {
        if (GodotObject.IsInstanceValid(_flightTarget)) SetDestinationPending(_flightTarget, false);
        _shuffleBacks.Clear(); _flight?.QueueFree(); _flight = null; _flightFront = null; _flightBack = null; _flightTarget = null; _activeFlight = null;
        _skillLessonPreview = false;
    }

    private static void SetDestinationPending(Control? target, bool pending)
    {
        if (target is SkillTile skill) skill.SetLessonPending(pending);
        else if (target != null) target.Modulate = new(1, 1, 1, pending ? 0 : 1);
    }

    private static bool RectVisible(Control anchor, Rect2 rect)
    {
        if (!anchor.IsVisibleInTree()) return false;
        if (anchor is HandCard { InFan: true })
            rect = anchor.GetGlobalTransform() * new Rect2(Vector2.Zero, anchor.Size);
        for (Node? parent = anchor; parent != null; parent = parent.GetParent())
            if (parent is Control { ClipContents: true } clip && !clip.GetGlobalRect().Encloses(rect)) return false;
        return true;
    }
    private void ResetAnimations()
    {
        foreach (var pool in _actionPools) pool.Settle();
        ResetStageBanner();
        SetQueuedDestinationsPending(false);
        ResetOpeningInitiative();
        _skillLessonRoll = null;
        _flights.Clear(); _pendingPortraits.Clear(); FinishFlight(); _die.Value = 0;
        foreach (var portrait in _portraits) portrait.ReturnToRest();
    }
}
