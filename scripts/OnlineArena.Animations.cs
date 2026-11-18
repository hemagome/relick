using Godot;
using Relick.Core;

namespace Relick;

public partial class OnlineArena
{
    private sealed record Flight(DuelVisualEvent Event, Rect2? Origin = null, string? Face = null, double Duration = 0.45,
        float OriginRotation = 0, bool IsPlacement = false, string? PreviousEquipmentStatus = null,
        bool? StartsFaceUp = null, bool? EndsFaceUp = null, bool Paired = false)
    {
        public bool IsSteal => Event.Kind == DuelVisualKind.HandSteal;
        public bool IsSkillLesson => IsPlacement && Event.Destination == CardZone.Skill;
        public bool IsExchangePlacement => IsPlacement && Event.Destination == CardZone.Exchange;
        public bool LeavesExchange => Event.Kind == DuelVisualKind.Pile && Event.Source == CardZone.Exchange;
        public bool IsExchangePileArrival => LeavesExchange && Event.Destination is CardZone.Memory or CardZone.Corrupted;
        public bool TurnsSideways => IsSkillLesson || IsExchangePlacement;
    }
    private readonly Queue<Flight> _flights = new();
    private readonly List<(int Player, PortraitMotion Motion)> _pendingPortraits = [];
    // Cosmetic only: the amounts come from the same authorized cues that move the portraits.
    private readonly List<(int Player, int Amount, bool Critical)> _pendingImpacts = [];
    private Control? _flight;
    // Mirrored flight for the other player (Cartucho de Re-carga): it starts and lands with the primary one.
    private sealed record Companion(Flight Flight, Control Node, HandCard? Front, TextureRect Back, Control? Target, Rect2 Source, Rect2 Destination);
    private Companion? _companion;
    private HandCard? _flightFront;
    private TextureRect? _flightBack;
    private Control? _flightTarget;
    private Flight? _activeFlight;
    private readonly Queue<DuelVisualEvent> _placementDice = new();
    private bool _placementPreview;
    private Rect2 _flightSource, _flightDestination;
    private double _flightTime;
    private int _flightLayoutFrames;
    private readonly List<TextureRect> _shuffleBacks = [];

    private Control? ZoneNode(int relative, CardZone zone, int slot) => zone switch
    {
        CardZone.Hand => _root?.FindChild((relative == 0 ? "HandCard" : "OpponentHandCard") + slot, true, false) as Control,
        CardZone.Memory or CardZone.Corrupted => _root?.FindChild((zone == CardZone.Corrupted ? "CorruptedMemory" : "Memory") + "Button" + relative, true, false) as Control,
        CardZone.Bonus => (_root?.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack)?.BonusButton,
        CardZone.Area => (_root?.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack)?.AreaButton,
        CardZone.Exchange => (_root?.FindChild("BonusTrack" + relative, true, false) as ExchangeTrack)?.ExchangeButtons.ElementAtOrDefault(slot),
        CardZone.Equipment => _root?.FindChild("EquipmentZone" + relative, true, false) as Control,
        CardZone.LightEquipment => _root?.FindChild("LightEquipmentZone" + relative, true, false) as Control,
        CardZone.EquipmentCover => _root?.FindChild("EquipmentCoverZone" + relative, true, false) as Control,
        CardZone.Skill => _root?.FindChild($"Skill{relative}_{_snapshot!.Players[relative == 0 ? _snapshot.MySeat : 1 - _snapshot.MySeat].SkillPosition(slot)}", true, false) as Control,
        CardZone.Attached => _root?.FindChild("AttachmentStack" + relative, true, false) as Control,
        CardZone.Deck => _root?.FindChild("DeckStack" + relative, true, false) as Control,
        _ => null
    };
    private static Rect2 ZoneRect(Control control) => control switch
    {
        MemoryZoneButton pile => Global(pile, pile.CardRect),
        ZoneCardButton zone => Global(zone, zone.CardRect),
        AttachmentStack attached => Global(attached, attached.CardRect),
        DeckStack deck => Global(deck, deck.TopRect),
        HandCard { InFan: true } card => Upright(card),
        OpponentHandCard card => Upright(card),
        _ => control.GetGlobalRect()
    };

    // The desktop board is a scaled stage: zone rectangles go through the control's transform.
    private static Rect2 Global(Control control, Rect2 local) => control.GetGlobalTransform() * local;

    private static Rect2 Upright(Control card)
    {
        var transform = card.GetGlobalTransform();
        var size = card.Size * transform.Scale;
        return new Rect2(transform * (card.Size / 2) - size / 2, size);
    }

    private Dictionary<DuelVisualEvent, Flight> CaptureOrigins(DuelSnapshot current, DuelSnapshot? previous)
    {
        var result = new Dictionary<DuelVisualEvent, Flight>();
        if (previous == null || _root == null) return result;
        foreach (var cue in current.Visuals.Where(v => v.Kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer or DuelVisualKind.MemoryClaim or DuelVisualKind.HandDiscard or DuelVisualKind.HandSteal))
        {
            var relative = cue.Player == current.MySeat ? 0 : 1;
            var source = cue.Kind is DuelVisualKind.Return or DuelVisualKind.HandTransfer ? CardZone.Hand : cue.Kind == DuelVisualKind.BonusClaim ? CardZone.Bonus : cue.Source;
            var slot = cue.Kind is DuelVisualKind.HandTransfer or DuelVisualKind.HandDiscard ? 0 : cue.Kind is DuelVisualKind.MemoryClaim or DuelVisualKind.HandSteal ? cue.SourceIndex : cue.Value;
            var node = ZoneNode(relative, source, slot);
            var player = previous.Players[cue.Player];
            string? id = source switch
            {
                CardZone.Hand => player.Hand.ElementAtOrDefault(slot), CardZone.Bonus => player.Bonus, CardZone.Area => player.Area,
                CardZone.Equipment => player.Equipment, CardZone.EquipmentCover => player.EquipmentCovers?.ElementAtOrDefault(cue.Value), CardZone.Skill => player.Skills.ElementAtOrDefault(cue.Value), CardZone.Attached => player.Attached.ElementAtOrDefault(cue.Value),
                CardZone.LightEquipment => player.LightEquipment?.ElementAtOrDefault(cue.Value),
                CardZone.Exchange => player.Exchange.ElementAtOrDefault(cue.Value), CardZone.Memory => player.Memory.ElementAtOrDefault(slot), _ => null
            };
            if (cue.Kind is DuelVisualKind.HandTransfer or DuelVisualKind.HandDiscard) id = null; // One face-down packet per hand, regardless of its size.
            var origin = node != null && RectVisible(node, ZoneRect(node)) ? ZoneRect(node) : (Rect2?)null;
            if (source == CardZone.Activation && _root.FindChild("CombatCenter", true, false) is Control center)
            {
                var activation = new Rect2(center.GetGlobalRect().GetCenter() - new Vector2(27, 38.5f), new(54, 77));
                origin = RectVisible(center, activation) ? activation : null;
            }
            var rotation = source == CardZone.Exchange ? Mathf.Pi / 2 : node?.Rotation ?? 0;
            if (cue.Kind == DuelVisualKind.HandSteal)
            {
                // The card shows its face only on the side of the board that may see it: a hidden card
                // turns over as it reaches the thief, and one of ours turns face down on its way out.
                var landing = current.Players[cue.Target].Hand.ElementAtOrDefault(cue.Value);
                // A compact strip may have scrolled the card out of view: it then leaves from the middle of that hand.
                if (origin == null && (relative == 0 ? _hand : (_root.FindChild("OpponentHandFan", true, false)
                        ?? _root.FindChild("OpponentHandScroll", true, false)) as Control) is { } hand && hand.IsVisibleInTree())
                    origin = PreviewRect(hand.GetGlobalRect().GetCenter());
                result[cue] = new Flight(cue, origin, id ?? landing, Duration: 0.72, OriginRotation: rotation,
                    StartsFaceUp: id != null, EndsFaceUp: landing != null);
                continue;
            }
            if (source == CardZone.Deck && cue.Destination == CardZone.Attached)
            {
                result[cue] = new Flight(cue, origin, current.Players[cue.Player].Attached.LastOrDefault(), StartsFaceUp: false);
                continue;
            }
            if (source == CardZone.Deck && cue.Destination is CardZone.Equipment or CardZone.LightEquipment)
            {
                var destination = current.Players[cue.Player];
                var face = cue.Destination == CardZone.LightEquipment ? destination.LightEquipment?.LastOrDefault() : destination.Equipment;
                result[cue] = new Flight(cue, origin, face, StartsFaceUp: false);
                continue;
            }
            if (source == CardZone.Hand && cue.Destination == CardZone.Area)
            {
                result[cue] = new Flight(cue, origin, current.Players[cue.Player].Area, StartsFaceUp: id != null);
                continue;
            }
            result[cue] = new Flight(cue, origin, id, OriginRotation: rotation);
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

        var skillIndex = before.Skills.Length;
        return CaptureHandPlacement(current, previous, actor, after.Skills[skillIndex], CardZone.Skill, skillIndex);
    }

    private Flight? CaptureExchangePlacement(DuelSnapshot current, DuelSnapshot previous, Dictionary<DuelVisualEvent, Flight> origins)
    {
        for (var actor = 0; actor < current.Players.Length; actor++)
        {
            var before = previous.Players[actor];
            var after = current.Players[actor];
            // A completed exchange sends the new card to memory in the same revision.
            var departures = current.Visuals.Where(c => c.Kind == DuelVisualKind.Pile && c.Source == CardZone.Exchange && c.Player == actor).ToArray();
            if (after.Exchange.Length + departures.Length <= before.Exchange.Length) continue;
            var departure = departures.ElementAtOrDefault(before.Exchange.Length);
            var slot = departure?.Value ?? after.Exchange.Length - 1;
            var face = departure == null ? after.Exchange[slot] : null;

            var source = previous.Phase == Phase.SniperChoice && previous.DecisionPlayer == actor ? CardZone.Memory :
                face != null && face == before.Bonus && before.HandCount == after.HandCount ? CardZone.Bonus : CardZone.Hand;
            Flight placement;
            if (source == CardZone.Hand)
            {
                if (face == null && departure != null) face = CompletedExchangeFace(current, previous, actor, departure);
                placement = CaptureHandPlacement(current, previous, actor, face, CardZone.Exchange, slot);
            }
            else
            {
                var relative = actor == current.MySeat ? 0 : 1;
                var node = ZoneNode(relative, source, -1);
                var origin = node != null && RectVisible(node, ZoneRect(node)) ? ZoneRect(node) : (Rect2?)null;
                var cue = new DuelVisualEvent { Kind = DuelVisualKind.Pile, Player = actor, Value = slot, Source = source, Destination = CardZone.Exchange };
                placement = new Flight(cue, origin, face ?? CardCatalog.Sniper.Id, Duration: 0.55, IsPlacement: true);
            }
            if (departure != null && origins.TryGetValue(departure, out var exit))
                origins[departure] = exit with { Face = placement.Face };
            return placement;
        }
        return null;
    }

    private static string? CompletedExchangeFace(DuelSnapshot current, DuelSnapshot previous, int actor, DuelVisualEvent departure)
    {
        var history = current.History?.LastOrDefault();
        if (previous.Phase == Phase.Actions && history?.Actor == actor &&
            history.Id > (previous.History?.LastOrDefault()?.Id ?? 0) && CardCatalog.All.ContainsKey(history.CardId))
            return history.CardId;

        var before = previous.Players[actor].Memory;
        var after = current.Players[actor].Memory;
        if (departure.Destination != CardZone.Memory || !after.AsSpan().StartsWith(before)) return null;
        var preceding = current.Visuals.TakeWhile(c => c != departure)
            .Count(c => c.Player == actor && c.Kind == DuelVisualKind.Pile && c.Destination == CardZone.Memory);
        return after.ElementAtOrDefault(before.Length + preceding);
    }

    private Flight? CaptureEquipmentInstall(DuelSnapshot current, DuelSnapshot previous, TrapRecipe? recipe = null)
    {
        for (var actor = 0; actor < current.Players.Length; actor++)
        {
            var before = previous.Players[actor];
            var after = current.Players[actor];
            var light = (after.LightEquipment?.Length ?? 0) > (before.LightEquipment?.Length ?? 0);
            var destination = light ? CardZone.LightEquipment : CardZone.Equipment;
            var id = light ? after.LightEquipment?.LastOrDefault() : after.Equipment;
            if (id == null) continue;
            if (!light && id == CardCatalog.SuperMagnet.Id && recipe?.Entry.Actor == actor && recipe.Entry.CardId == id)
                continue; // The recipe summon already delivers this copy to the equipment slot.
            if (current.Visuals.Any(c => (c.DestinationPlayer ?? c.Player) == actor && c.Kind == DuelVisualKind.Pile && c.Destination == destination))
                continue; // The effect already emitted a flight for the equipped copy.

            var stacked = !light && before.Equipment == id && after.EquipmentStackedCopies > before.EquipmentStackedCopies;
            var replaced = current.Visuals.Any(c => c.Player == actor && c.Kind == DuelVisualKind.Pile &&
                c.Source == CardZone.Equipment && c.Destination == CardZone.Memory);
            if (!light && before.Equipment == id && !stacked && (!replaced || after.EquipmentStackedCopies < before.EquipmentStackedCopies))
                continue;

            var relative = actor == current.MySeat ? 0 : 1;
            var previousStatus = stacked ? (ZoneNode(relative, CardZone.Equipment, -1) as ZoneCardButton)?.StatusText : null;
            if (previous.Phase == Phase.Actions && actor == previous.CurrentPlayer && before.HandCount == after.HandCount + 1)
                return CaptureHandPlacement(current, previous, actor, id, destination, previousEquipmentStatus: previousStatus);

            var fromMemory = previous.Phase == Phase.SniperChoice && id == CardCatalog.Sniper.Id;
            var source = fromMemory ? ZoneNode(relative, CardZone.Memory, -1) :
                _root?.FindChild("CombatCenter", true, false) as Control;
            Rect2? origin = source == null ? null : fromMemory ? ZoneRect(source) : PreviewRect(source.GetGlobalRect().GetCenter());
            var cue = new DuelVisualEvent { Kind = DuelVisualKind.Pile, Player = actor,
                Source = fromMemory ? CardZone.Memory : CardZone.Activation, Destination = destination };
            return new Flight(cue, origin, id, Duration: 0.60, IsPlacement: true, PreviousEquipmentStatus: previousStatus);
        }
        return null;
    }

    private static Rect2 PreviewRect(Vector2 center) => new(center - new Vector2(27, 38), new(54, 76));

    private static Rect2 PortraitFootprint(Rect2 landscape)
    {
        var portrait = new Vector2(landscape.Size.Y, landscape.Size.X);
        return new Rect2(landscape.GetCenter() - portrait / 2, portrait);
    }

    private Flight CaptureHandPlacement(DuelSnapshot current, DuelSnapshot previous, int actor, string? id,
        CardZone destination, int slot = -1, string? previousEquipmentStatus = null)
    {
        var before = previous.Players[actor];
        var after = current.Players[actor];
        var own = actor == current.MySeat;
        Control? source;
        if (own)
        {
            // A claimed Bonus or a triggered draw can add cards after an exchange removes one.
            var remaining = destination == CardZone.Exchange ? after.Hand.Take(before.Hand.Length - 1) : after.Hand;
            bool WasRemoved(int i) => before.Hand.ElementAtOrDefault(i) is { } candidate && (id == null || candidate == id) &&
                before.Hand.Where((_, j) => j != i).SequenceEqual(remaining);
            var index = _selected.FirstOrDefault(WasRemoved, -1);
            if (index < 0)
                index = Enumerable.Range(0, before.Hand.Length).FirstOrDefault(WasRemoved, -1);
            id ??= before.Hand.ElementAtOrDefault(index);
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
            var hand = own ? _hand : (_root?.FindChild("OpponentHandFan", true, false) ?? _root?.FindChild("OpponentHandScroll", true, false)) as Control;
            if (hand != null)
                origin = PreviewRect(hand.GetGlobalRect().GetCenter());
        }
        var cue = new DuelVisualEvent { Kind = DuelVisualKind.Pile, Player = actor, Value = slot,
            Source = CardZone.Hand, Destination = destination };
        var duration = destination switch { CardZone.Skill => 0.66, CardZone.Exchange => 0.55, _ => 0.60 };
        return new Flight(cue, origin, id, Duration: duration,
            OriginRotation: rotation, IsPlacement: true, PreviousEquipmentStatus: previousEquipmentStatus);
    }

    private void Animate(List<DuelVisualEvent> events, Dictionary<DuelVisualEvent, Flight> origins, Flight? placement = null)
    {
        if (events.Count > 0 || placement != null) PlayedVisualRevisions++;
        if (placement?.IsExchangePlacement == true)
        {
            _flights.Enqueue(placement);
            var relative = placement.Event.Player == _snapshot!.MySeat ? 0 : 1;
            foreach (var flight in origins.Values.Where(f => f.LeavesExchange && f.Event.Player == placement.Event.Player && f.Event.Value < placement.Event.Value))
                (ZoneNode(relative, CardZone.Exchange, flight.Event.Value) as ZoneCardButton)?.SetExchangePreview(flight.Face);
        }
        var (ordered, paired) = PairDrawBlocks(events);
        foreach (var cue in ordered)
        {
            switch (cue.Kind)
            {
                case DuelVisualKind.Die:
                case DuelVisualKind.DieAdjustment:
                    if (placement != null) _placementDice.Enqueue(cue);
                    else PresentDieCue(cue);
                    break;
                case DuelVisualKind.Attack: _pendingPortraits.Add((cue.Player, PortraitMotion.Attack)); break;
                case DuelVisualKind.Hit:
                    if (cue.Value > 0) _pendingImpacts.Add((cue.Player, -cue.Value, cue.AttackDie == 6 && !cue.NormalFromMiss));
                    _pendingPortraits.Add((cue.Player, cue.NormalFromMiss ? PortraitMotion.HitNormal : cue.AttackDie switch
                    {
                        5 => PortraitMotion.HitNormal, 6 => PortraitMotion.HitCritical, _ => PortraitMotion.Hit
                    }));
                    break;
                case DuelVisualKind.Heal when cue.Value > 0:
                    _pendingImpacts.Add((cue.Player, cue.Value, false));
                    _pendingPortraits.Add((cue.Player, PortraitMotion.Heal)); break;
                case DuelVisualKind.Draw: _flights.Enqueue(new(cue, Paired: paired.Contains(cue))); break;
                case DuelVisualKind.Shuffle: _flights.Enqueue(new(cue)); break;
                case DuelVisualKind.BonusPlace: _flights.Enqueue(new(cue, Face: _snapshot!.Players[cue.Player].Bonus)); break;
                case DuelVisualKind.Pile:
                case DuelVisualKind.BonusClaim:
                case DuelVisualKind.MemoryClaim:
                case DuelVisualKind.Return:
                case DuelVisualKind.HandTransfer:
                case DuelVisualKind.HandDiscard:
                case DuelVisualKind.HandSteal:
                    if (origins.TryGetValue(cue, out var flight))
                        _flights.Enqueue(cue.Kind == DuelVisualKind.HandDiscard ? flight with { Paired = true } : flight);
                    break;
            }
        }
        if (placement != null && !placement.IsExchangePlacement) _flights.Enqueue(placement);
        // The snapshot already contains these cards. Reveal each destination on arrival.
        SetQueuedDestinationsPending(true);
        _flightLayoutFrames = 2;
        _lastDisabled = null;
        PlayPendingPortraits();
    }

    // Two back-to-back blocks of draws for different players (both hands refilled at once) alternate,
    // so each pair of cards can fly at the same time instead of one player waiting for the other.
    private static (List<DuelVisualEvent> Ordered, HashSet<DuelVisualEvent> Paired) PairDrawBlocks(List<DuelVisualEvent> events)
    {
        var ordered = new List<DuelVisualEvent>(events.Count);
        var paired = new HashSet<DuelVisualEvent>();
        for (var i = 0; i < events.Count;)
        {
            var end = i;
            while (end < events.Count && events[end].Kind == DuelVisualKind.Draw) end++;
            if (end == i) { ordered.Add(events[i++]); continue; }
            var run = events.GetRange(i, end - i);
            var split = run.FindIndex(e => e.Player != run[0].Player);
            if (split > 0 && run.Skip(split).All(e => e.Player == run[split].Player))
            {
                for (var k = 0; k < Math.Max(split, run.Count - split); k++)
                {
                    if (k < split) ordered.Add(run[k]);
                    if (split + k < run.Count) ordered.Add(run[split + k]);
                }
                paired.UnionWith(run);
            }
            else ordered.AddRange(run);
            i = end;
        }
        return (ordered, paired);
    }

    private void PresentDieCue(DuelVisualEvent cue)
    {
        // Each roll shows the roller's cosmetic die; older servers do not say who rolled, so the turn's player's is shown.
        if (cue.Kind == DuelVisualKind.Die)
        {
            var style = _snapshot?.Players.ElementAtOrDefault(cue.Player is 0 or 1 ? cue.Player : _snapshot.CurrentPlayer)?.Dice;
            _die.QueueRoll(cue.Value, cue.Caption, style, cue.SecondDie is >= 1 and <= 6 ? () =>
            {
                _secondDie.Show();
                _secondDie.QueueRoll(cue.SecondDie, style: style);
            } : null);
        }
        else _die.ShowAdjustedResult(cue.Value, cue.Caption);
    }

    private void UpdateDiceOverlay()
    {
        _diceOverlay.Visible = _die.IsPresented;
        _secondDie.Visible = _secondDie.IsPresented;
        _diceOverlay.Modulate = new Color(1, 1, 1, _die.PresentationOpacity);
        if (_die.IsPresented) _diceCaption.Text = _die.RollCaption;
    }

    private void PlayPendingPortraits()
    {
        if (DiceBusy || _flight != null || _flights.Count > 0 || _openingInitiative != null || _snapshot == null) return;
        for (var i = 0; i < _pendingPortraits.Count;)
        {
            var (player, motion) = _pendingPortraits[i];
            var relative = player == _snapshot.MySeat ? 0 : 1;
            var portrait = _portraits[relative];
            if (motion is PortraitMotion.Hit or PortraitMotion.HitNormal or PortraitMotion.HitCritical &&
                _impacts.AttackInFlightTo(portrait))
            {
                i++;
                continue;
            }
            _pendingPortraits.RemoveAt(i);
            portrait.Play(motion);
            if (motion == PortraitMotion.Attack)
            {
                PlaySound(SoundCue.Attack);
                _impacts.LaunchAttack(portrait, _portraits[1 - relative]);
            }
            if (motion == PortraitMotion.Heal) PlaySound(SoundCue.Heal);
        }
        // Numbers read the portrait rectangle after the rebuilt board has been laid out.
        if (_flightLayoutFrames > 0) return;
        for (var i = 0; i < _pendingImpacts.Count;)
        {
            var (player, amount, critical) = _pendingImpacts[i];
            var portrait = _portraits[player == _snapshot.MySeat ? 0 : 1];
            if (amount < 0 && _impacts.AttackInFlightTo(portrait))
            {
                i++;
                continue;
            }
            _pendingImpacts.RemoveAt(i);
            if (!portrait.IsVisibleInTree()) continue;
            var rect = portrait.GetGlobalRect();
            _impacts.Number(new Vector2(rect.GetCenter().X, rect.Position.Y + rect.Size.Y * 0.62f), amount > 0 ? $"+{amount}" : amount.ToString(),
                amount > 0 ? new Color("5dffa8") : critical ? BoardArt.Gold : RelickVisuals.State.Danger, critical);
            if (amount < 0) _impacts.Flash(critical ? BoardArt.Gold : RelickVisuals.State.Danger);
        }
    }

    private (Rect2 Source, Rect2 Destination) PlacementPath(Flight placement, Control? target)
    {
        var bounds = new Rect2(Vector2.Zero, Size);
        var slot = target == null ? default : ZoneRect(target);
        if (placement.IsExchangePlacement && placement.Face is { } face && target is ZoneCardButton { Card: null } exchange)
        {
            var rect = exchange.ArtworkRect(BoardArt.LoadCardArtwork(CardCatalog.Get(face), FlightArtworkRelikario(placement.Event)));
            slot = Global(exchange, rect);
        }
        _placementPreview = target == null || !bounds.Encloses(slot) || !RectVisible(target, slot);
        if (_placementPreview)
        {
            var board = _arenaScroll?.GetGlobalRect().Intersection(bounds) ?? bounds;
            var preview = placement.IsSkillLesson ? SkillTile.CardSize * 0.9f :
                placement.IsExchangePlacement ? new Vector2(152, 104) : new Vector2(104, 152);
            slot = new Rect2(board.GetCenter() - preview / 2, preview);
        }
        var source = placement.Origin ?? PreviewRect(new Vector2(Size.X / 2, Size.Y - 52));
        var halfSize = source.Size / 2;
        source.Position = source.GetCenter().Clamp(bounds.Position + halfSize, bounds.End - halfSize) - halfSize;
        // Flights use portrait artwork, even when the slot displays a horizontal card.
        return (source, placement.TurnsSideways ? PortraitFootprint(slot) : slot);
    }

    private void UpdateFlights(double delta)
    {
        if (_flightLayoutFrames > 0) { _flightLayoutFrames--; return; }
        if (_flight == null && _flights.Count == 0 && _openingInitiative != null)
        { UpdateOpeningInitiative(delta); return; }
        if (_flight == null && _flights.TryPeek(out var next))
        {
            if (WaitForDrawAnnouncement(next.Event)) return;
            _flights.Dequeue();
            var own = next.Event.Player == _snapshot!.MySeat;
            var relative = own ? 0 : 1;
            var returning = next.Event.Kind == DuelVisualKind.Return;
            var kind = next.Event.Kind;
            var target = kind switch
            {
                DuelVisualKind.Return or DuelVisualKind.Shuffle => null,
                DuelVisualKind.Pile => ZoneNode(PileRecipient(next.Event) == _snapshot.MySeat ? 0 : 1, next.Event.Destination,
                    next.IsPlacement || next.Event.Destination == CardZone.Hand ? next.Event.Value : -1),
                DuelVisualKind.BonusPlace => ZoneNode(relative, CardZone.Bonus, -1),
                DuelVisualKind.BonusClaim or DuelVisualKind.MemoryClaim or DuelVisualKind.HandSteal => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, next.Event.Value),
                DuelVisualKind.HandTransfer => ZoneNode(next.Event.Target == _snapshot.MySeat ? 0 : 1, CardZone.Hand, 0),
                DuelVisualKind.HandDiscard => ZoneNode(relative, CardZone.Memory, -1),
                _ => ZoneNode(relative, CardZone.Hand, next.Event.Value)
            };
            if (next.IsPlacement) (_flightSource, _flightDestination) = PlacementPath(next, target);
            else
            {
                var deck = _root!.FindChild("DeckStack" + relative, true, false) as DeckStack;
                if (deck == null) { SetDestinationPending(next, target, false); return; }
                var deckRect = Global(deck, deck.TopRect);
                if (kind is DuelVisualKind.Draw or DuelVisualKind.HandSteal) PlaySound(SoundCue.CardDraw);
                if (kind == DuelVisualKind.Shuffle) PlaySound(SoundCue.CardShuffle);
                var fromPrevious = kind is DuelVisualKind.Return or DuelVisualKind.BonusClaim or DuelVisualKind.Pile or DuelVisualKind.HandTransfer or DuelVisualKind.MemoryClaim or DuelVisualKind.HandDiscard or DuelVisualKind.HandSteal;
                var source = fromPrevious ? next.Origin : deckRect;
                if (next.LeavesExchange && ZoneNode(relative, CardZone.Exchange, next.Event.Value) is ZoneCardButton { HasExchangePreview: true } slot)
                {
                    var rect = ZoneRect(slot);
                    source = RectVisible(slot, rect) ? rect : null;
                    slot.SetExchangePreview(null);
                }
                Rect2? destination = returning || kind == DuelVisualKind.Shuffle ? deckRect : target == null ? null : ZoneRect(target);
                var bounds = new Rect2(Vector2.Zero, Size);
                // Never draw through clipped panels/docks when a small-screen zone is offscreen.
                // On the desktop board the hands overhang the screen, so a flight may start or end partly outside it.
                bool OnScreen(Rect2 rect) => _handLowered ? bounds.Intersects(rect) : bounds.Encloses(rect);
                if (source == null || destination == null || !OnScreen(source.Value) || !OnScreen(destination.Value) ||
                    (!fromPrevious || returning) && !RectVisible(deck, deckRect) || target != null && !RectVisible(target, destination.Value))
                { SetDestinationPending(next, target, false); return; }
                _flightSource = next.Event.Source == CardZone.Exchange ? PortraitFootprint(source.Value) : source.Value;
                _flightDestination = destination.Value;
            }
            _activeFlight = next; _flightTarget = target;
            SetDestinationPending(next, target, true);
            _flight = new Control { Name = "NetworkCardFlight", Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 }; AddChild(_flight);
            // Cards in flight show their owner's sleeve.
            var sleeve = SleeveArt.Of(_snapshot, next.Event.Player);
            TextureRect Back() => FlightBack(sleeve);
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
                _flightFront = new HandCard { Card = CardCatalog.Get(id), RelikarioId = FlightArtworkRelikario(next.Event),
                    Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore };
                _flight.AddChild(_flightFront); _flightFront.SetProcess(false);
            }
            _flightTime = 0;
            if (next.Paired) TryLaunchCompanion(next);
        }
        if (_flight != null)
        {
            _flightTime += delta;
            if (_activeFlight!.Event.Kind == DuelVisualKind.Shuffle)
            {
                var progress = Math.Clamp((float)(_flightTime / 0.72), 0, 1);
                for (var i = 0; i < _shuffleBacks.Count; i++) ArenaMotion.ShuffleCard(_shuffleBacks[i], _flightSource, progress, i);
                if (progress >= 1) FinishFlight(landed: true);
                return;
            }
            var placement = _activeFlight.IsPlacement;
            var relative = _activeFlight.Event.Player == _snapshot!.MySeat ? 0 : 1;
            var t = Math.Clamp((float)(_flightTime / _activeFlight.Duration), 0, 1);
            var returning = _activeFlight.Event.Kind == DuelVisualKind.Return;
            var reveal = Math.Clamp((t - (placement ? 0.28f : 0.55f)) / 0.3f, 0, 1);
            var startsUp = _activeFlight.StartsFaceUp ?? _activeFlight.Event.Kind is not (DuelVisualKind.Draw or DuelVisualKind.BonusPlace) &&
                _activeFlight.Event.Source != CardZone.Corrupted &&
                !(placement && _activeFlight.Event.Source == CardZone.Hand && relative == 1);
            var endsUp = _activeFlight.EndsFaceUp ?? !returning && !(_activeFlight.Event.Kind == DuelVisualKind.Pile && _activeFlight.Event.Destination == CardZone.Corrupted);
            var flip = _flightFront == null || startsUp == endsUp ? 1 : Math.Max(0.035f, Math.Abs(Mathf.Cos(reveal * Mathf.Pi)));
            if (_flightFront != null)
            {
                _flightFront.Visible = reveal < 0.5f ? startsUp : endsUp;
                _flightBack!.Visible = !_flightFront.Visible;
            }
            if (_activeFlight.IsSteal)
                ArenaMotion.StealCard(_flight, _flightSource, _flightDestination, t, flip, _activeFlight.OriginRotation, _flightTarget?.Rotation ?? 0);
            else
                ArenaMotion.PositionCard(_flight, _flightSource, _flightDestination, t, relative, flip,
                    _activeFlight.OriginRotation, _activeFlight.TurnsSideways ? Mathf.Pi / 2 : _flightTarget?.Rotation ?? 0);
            if (_companion != null) PoseCompanion(_companion, t);
            var fade = _placementPreview ? Math.Clamp((float)(_flightTime - _activeFlight.Duration - 0.24) / 0.2f, 0, 1) : t;
            if (_placementPreview) _flight.Modulate = new Color(1, 1, 1, 1 - fade);
            if (fade >= 1)
            {
                FinishFlight(landed: true);
                if (placement)
                    while (_placementDice.TryDequeue(out var cue)) PresentDieCue(cue);
            }
        }
        PlayPendingPortraits();
    }
    private static TextureRect FlightBack(string? sleeve = null) => new() { Texture = SleeveArt.Back(sleeve),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        Position = new(1, 7), Size = new(124, 175), MouseFilter = MouseFilterEnum.Ignore };

    private string FlightArtworkRelikario(DuelVisualEvent cue)
    {
        if (cue.Kind is DuelVisualKind.BonusClaim or DuelVisualKind.HandSteal &&
            HandCardOwner(cue.Target, cue.Value)?.RelikarioId is { } original)
            return original;
        var owner = cue.ToRivalMemory ? 1 - cue.Player : cue.Player;
        return _snapshot!.Players[owner].RelikarioId;
    }

    private static int PileRecipient(DuelVisualEvent cue) => cue.DestinationPlayer ?? (cue.ToRivalMemory ? 1 - cue.Player : cue.Player);

    private void TryLaunchCompanion(Flight primary)
    {
        if (!_flights.TryPeek(out var pair) || !pair.Paired || pair.IsPlacement || pair.Event.Kind != primary.Event.Kind ||
            pair.Event.Player == primary.Event.Player || WaitForDrawAnnouncement(pair.Event)) return;
        _flights.Dequeue();
        var own = pair.Event.Player == _snapshot!.MySeat;
        var relative = own ? 0 : 1;
        var discard = pair.Event.Kind == DuelVisualKind.HandDiscard;
        var target = discard ? ZoneNode(relative, CardZone.Memory, -1) : ZoneNode(relative, CardZone.Hand, pair.Event.Value);
        var deck = _root!.FindChild("DeckStack" + relative, true, false) as DeckStack;
        Rect2? deckRect = deck == null ? null : Global(deck, deck.TopRect);
        var source = discard ? pair.Origin : deckRect;
        Rect2? destination = target == null ? null : ZoneRect(target);
        var bounds = new Rect2(Vector2.Zero, Size);
        bool OnScreen(Rect2 rect) => _handLowered ? bounds.Intersects(rect) : bounds.Encloses(rect);
        if (deck == null || target == null || source == null || destination == null || !OnScreen(source.Value) || !OnScreen(destination.Value) ||
            !discard && !RectVisible(deck, deckRect!.Value) || !RectVisible(target, destination.Value))
        { SetDestinationPending(pair, target, false); return; }
        SetDestinationPending(pair, target, true);
        var node = new Control { Name = "NetworkCardFlightPair", Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore, ZIndex = 20 }; AddChild(node);
        var back = FlightBack(SleeveArt.Of(_snapshot, pair.Event.Player)); node.AddChild(back);
        var hand = _snapshot.Players[_snapshot.MySeat].Hand;
        HandCard? front = null;
        if (!discard && own && hand.ElementAtOrDefault(pair.Event.Value) is { } id)
        {
            front = new HandCard { Card = CardCatalog.Get(id), RelikarioId = FlightArtworkRelikario(pair.Event),
                Size = new(126, 184), MouseFilter = MouseFilterEnum.Ignore };
            node.AddChild(front); front.SetProcess(false);
        }
        _companion = new(pair, node, front, back, target, source.Value, destination.Value);
    }

    private void PoseCompanion(Companion pair, float t)
    {
        var relative = pair.Flight.Event.Player == _snapshot!.MySeat ? 0 : 1;
        // Draws arrive face down from the deck; a discarded hand leaves face down as one packet.
        var flip = 1f;
        if (pair.Front != null)
        {
            var reveal = Math.Clamp((t - 0.55f) / 0.3f, 0, 1);
            flip = Math.Max(0.035f, Math.Abs(Mathf.Cos(reveal * Mathf.Pi)));
            pair.Front.Visible = reveal >= 0.5f;
            pair.Back.Visible = !pair.Front.Visible;
        }
        ArenaMotion.PositionCard(pair.Node, pair.Source, pair.Destination, t, relative, flip,
            pair.Flight.OriginRotation, pair.Target?.Rotation ?? 0);
    }

    private void FinishCompanion(bool landed)
    {
        if (_companion is not { } pair) return;
        _companion = null;
        if (landed)
            _impacts.Ring(pair.Destination, pair.Flight.Event.Player == _snapshot?.MySeat ? ArenaPresentation.Mint : ArenaPresentation.Coral);
        if (GodotObject.IsInstanceValid(pair.Target)) SetDestinationPending(pair.Flight, pair.Target, false);
        pair.Node.QueueFree();
    }

    private void FinishFlight(bool landed = false)
    {
        FinishCompanion(landed && !_placementPreview);
        if (landed && _activeFlight is { } arrived && !_placementPreview && arrived.Event.Kind != DuelVisualKind.Shuffle)
        {
            // A short flash marks where the card came to rest.
            var own = (arrived.IsSteal ? arrived.Event.Target : arrived.Event.Player) == _snapshot?.MySeat;
            var color = arrived.IsPlacement ? BoardArt.Gold : own ? ArenaPresentation.Mint : ArenaPresentation.Coral;
            _impacts.Ring(arrived.TurnsSideways ? PortraitFootprint(_flightDestination) : _flightDestination, color);
        }
        if (_activeFlight != null && GodotObject.IsInstanceValid(_flightTarget))
        {
            SetDestinationPending(_activeFlight, _flightTarget, false);
            if (_activeFlight.IsExchangePlacement && _flightTarget is ZoneCardButton { Card: null } slot &&
                _flights.Any(f => f.LeavesExchange && f.Event.Player == _activeFlight.Event.Player && f.Event.Value == _activeFlight.Event.Value))
                slot.SetExchangePreview(_activeFlight.Face);
        }
        _shuffleBacks.Clear(); _flight?.QueueFree(); _flight = null; _flightFront = null; _flightBack = null; _flightTarget = null; _activeFlight = null;
        _placementPreview = false;
    }

    private static void SetDestinationPending(Flight flight, Control? target, bool pending, bool queued = false)
    {
        if (flight.IsExchangePileArrival && target is MemoryZoneButton pile)
        {
            // Count each card when queued and reveal it when the flight is resolved or skipped.
            if (queued || !pending)
                pile.ChangePendingExchangeArrivals(pending ? 1 : -1);
        }
        else if (target is SkillTile skill) skill.SetLessonPending(pending);
        else if (flight.IsExchangePlacement && target is ZoneCardButton exchange)
            exchange.SetExchangePending(pending);
        else if (flight.IsPlacement && target is ZoneCardButton equipment)
            equipment.SetEquipmentPending(pending, flight.PreviousEquipmentStatus);
        else if (target != null) target.Modulate = new(1, 1, 1, pending ? 0 : 1);
    }

    private static bool RectVisible(Control anchor, Rect2 rect)
    {
        if (!anchor.IsVisibleInTree()) return false;
        if (anchor is HandCard { InFan: true })
            rect = anchor.GetGlobalTransform() * new Rect2(Vector2.Zero, anchor.Size);
        for (Node? parent = anchor; parent != null; parent = parent.GetParent())
            if (parent is Control { ClipContents: true } clip)
            {
                // Both hands hang over the edges of the desktop board on purpose: there a card
                // counts as visible while any part of it is on the playmat.
                // The board is a scaled stage: allow for the rounding of its transforms.
                var bounds = clip.GetGlobalRect().Grow(1);
                if (clip is BoardCanvas ? !bounds.Intersects(rect) : !bounds.Encloses(rect)) return false;
            }
        return true;
    }
    private void ResetAnimations(bool preserveStageAnnouncements = false)
    {
        foreach (var pool in _actionPools) pool.Settle();
        if (!preserveStageAnnouncements) ResetStageBanner();
        SetQueuedDestinationsPending(false);
        ResetOpeningInitiative();
        _placementDice.Clear(); _queuedPlay = null; FinishSummonedEquipment();
        _flights.Clear(); _pendingPortraits.Clear(); _pendingImpacts.Clear(); FinishFlight(); _die.Value = 0;
        _secondDie.Value = 0;
        _secondDie.Hide();
        _impacts.Clear();
        foreach (var portrait in _portraits) portrait.ReturnToRest();
    }
}
