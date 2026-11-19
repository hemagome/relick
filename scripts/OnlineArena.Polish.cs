using Godot;
using Relick.Core;

namespace Relick;

// Small, purely cosmetic touches: none of them blocks a play, delays a decision or runs a rule.
public partial class OnlineArena
{
    private string? _lastPlayAnchor;
    internal int RejectionShakes { get; private set; }

    /// <summary>The card or zone whose action is about to be sent, so a refusal can point at it.</summary>
    private void RememberPlayAnchor(DuelOption option)
    {
        var move = option.Move;
        var anchor = _cardActionAnchor;
        if (anchor == null && _fieldSelection is { } field && MatchesField(option, field)) anchor = FieldActionAnchor(field);
        if (anchor == null && IsHandAction(move.Kind) && !ChoosingEffectCards && !ChoosingRecycling)
            anchor = _handCards.ElementAtOrDefault(move.Index);
        if (anchor == null && move.Kind is DuelMoveKind.ProtectDestruction or DuelMoveKind.DestinyDefense)
            anchor = _ownSkills.FirstOrDefault(tile => tile.SkillIndex == move.Index && tile.Skill != null);
        _lastPlayAnchor = anchor?.Name;
    }

    /// <summary>A short shake of the card or zone whose play was refused. Only its drawn place moves, and only briefly.</summary>
    private void ShakeRejected()
    {
        var name = _lastPlayAnchor; _lastPlayAnchor = null;
        if (_impacts.ReducedMotion || name == null || _root?.FindChild(name, true, false) is not Control target) return;
        var rest = target.Position;
        var tween = target.CreateTween();
        foreach (var dx in new[] { -7f, 6, -4, 2, 0 })
            tween.TweenProperty(target, "position", rest + new Vector2(dx, 0), 0.04);
        RejectionShakes++;
    }

    private sealed record TrapRecipe(DuelHistoryEntry Entry, string[] Ingredients);
    private (DuelSnapshot Previous, DuelSnapshot Current, TrapRecipe? Recipe)? _queuedPlay;
    private TrapRecipe? _pendingTrapRecipe;
    private ZoneCardButton? _summonedEquipment;
    private Tween? _summonedEquipmentTween;

    private TrapRecipe? CaptureTrapRecipe(DuelSnapshot previous, DuelSnapshot current)
    {
        if (previous.Phase == Phase.DestructionResponse && _pendingTrapRecipe is { } pending)
        {
            if (current.Phase == Phase.DestructionResponse) return null;
            _pendingTrapRecipe = null;
            return SuperMagnetPlaced(previous, current, pending.Entry.Actor) ? pending : null;
        }
        _pendingTrapRecipe = null;
        if (previous.Phase != Phase.TrapCraftChoice) return null;
        var seen = previous.History?.Select(e => e.Id).DefaultIfEmpty(0UL).Max() ?? 0;
        var entry = current.History?.LastOrDefault(e => e.Id > seen && e.CardId.Length > 0 &&
            CardCatalog.All.TryGetValue(e.CardId, out var trap) && trap.CodeType == CodeType.Trap);
        if (entry == null) return null;
        var recipe = new TrapRecipe(entry, previous.TrapIngredients ?? []);
        if (entry.CardId != CardCatalog.SuperMagnet.Id) return recipe;
        if (current.Phase != Phase.DestructionResponse)
            return SuperMagnetPlaced(previous, current, entry.Actor) ? recipe : null;

        // The existing equipment can still be protected. Keep the recipe for the revision that settles its replacement.
        _pendingTrapRecipe = recipe;
        return null;
    }

    private static bool SuperMagnetPlaced(DuelSnapshot previous, DuelSnapshot current, int actor) =>
        current.Players[actor].Equipment == CardCatalog.SuperMagnet.Id &&
        (previous.Players[actor].Equipment != CardCatalog.SuperMagnet.Id ||
            current.Visuals.Any(c => c is { Kind: DuelVisualKind.Pile, Source: CardZone.Equipment, Destination: CardZone.OutOfGame } && c.Player == actor));

    private void QueuePlayedCard(DuelSnapshot previous, DuelSnapshot current, TrapRecipe? recipe)
    {
        FinishSummonedEquipment();
        _queuedPlay = (previous, current, recipe);
        if (!_impacts.ReducedMotion && recipe?.Entry.CardId == CardCatalog.SuperMagnet.Id &&
            ZoneNode(recipe.Entry.Actor == current.MySeat ? 0 : 1, CardZone.Equipment, -1) is ZoneCardButton slot)
        {
            _summonedEquipment = slot;
            slot.SetEquipmentPending(true, null);
        }
    }

    /// <summary>
    /// The flash of a played code and a created Trap's summon, one frame after the Render that showed them:
    /// read straight away, the rebuilt board has no layout yet and every place sits at the top-left corner.
    /// </summary>
    private void PresentQueuedPlay()
    {
        if (_queuedPlay is not { } play) return;
        _queuedPlay = null;
        if (_snapshot?.Revision != play.Current.Revision) { FinishSummonedEquipment(); return; }
        FlashPlayedCard(play.Previous, play.Current);
        var hasLanding = play.Recipe is { } recipe && SummonCreatedTrap(play.Current, recipe);
        if (IsInstanceValid(_summonedEquipment))
        {
            if (!hasLanding) { FinishSummonedEquipment(); return; }
            _summonedEquipmentTween = _summonedEquipment!.CreateTween();
            _summonedEquipmentTween.TweenInterval(ImpactLayer.SummonLanding);
            _summonedEquipmentTween.TweenCallback(Callable.From(() =>
            {
                _summonedEquipmentTween = null;
                FinishSummonedEquipment();
            }));
        }
    }

    private void FinishSummonedEquipment()
    {
        _summonedEquipmentTween?.Kill();
        _summonedEquipmentTween = null;
        if (IsInstanceValid(_summonedEquipment)) _summonedEquipment!.SetEquipmentPending(false, null);
        _summonedEquipment = null;
    }

    /// <summary>
    /// Shows the code a duelist just played, read from the public history. A follow-up decision inside the
    /// same resolution (choosing Disquete's card, say) is not a new play and gets no flash.
    /// </summary>
    internal void FlashPlayedCard(DuelSnapshot previous, DuelSnapshot current)
    {
        var seen = previous.History?.Select(e => e.Id).DefaultIfEmpty(0UL).Max() ?? 0;
        var entry = current.History?.LastOrDefault(e => e.Id > seen && e.CardId.Length > 0 &&
            e.Status is DuelResolutionStatus.Response or DuelResolutionStatus.Resolving);
        if (entry == null || previous.Resolution?.CardId == entry.CardId) return;
        // A link of a visible response chain slots into the chain instead of flashing on its own.
        if (LiveChain(current) is { Count: >= 2 } chain && chain.Any(link => link.CardId == entry.CardId)) return;
        // A created Trap gets its own recipe animation instead (SummonCreatedTrap).
        if (!CardCatalog.All.TryGetValue(entry.CardId, out var card) || card.Kind != CardKind.Code || card.CodeType == CodeType.Trap) return;
        if (BoardArt.LoadCardArtwork(card) is not { } art) return;
        var lane = _combatCenter != null && IsInstanceValid(_combatCenter) ? _combatCenter.GetGlobalRect() : GetGlobalRect();
        var center = new Vector2(lane.GetCenter().X, Math.Max(lane.GetCenter().Y - 150, GlobalPosition.Y + 150));
        _impacts.PlayedCard(art, center, BoardArt.Gold);
    }

    /// <summary>
    /// After a «Crear» decision, the public history names the Trap. Its two exchanged cards are already in
    /// memory (rulebook § 4.6), so echoes of them rise from there, meet over the exchange and call the Trap,
    /// which then flies to its place: the hand for Súper Ataque, the equipment zone for Súper Imán. Zahira
    /// Flair's host is chosen next, so its card appears and fades over the exchange.
    /// </summary>
    private bool SummonCreatedTrap(DuelSnapshot current, TrapRecipe recipe)
    {
        var entry = recipe.Entry;
        if (_impacts.ReducedMotion || _root == null || BoardArt.LoadCardArtwork(CardCatalog.Get(entry.CardId)) is not { } art) return false;
        var relative = entry.Actor == current.MySeat ? 0 : 1;
        Rect2 Bounds(Control node) => Global(node, new Rect2(Vector2.Zero, node.Size));
        Rect2? Find(string name) => _root.FindChild(name, true, false) is Control { } node && node.IsVisibleInTree() ? Bounds(node) : null;
        var memory = Find("MemoryButton" + relative);
        var exchange = _exchangeTracks[relative] is { } track && IsInstanceValid(track)
            ? track.ExchangeButtons.Where(IsInstanceValid).Select(Bounds).Aggregate((a, b) => a.Merge(b)) : (Rect2?)null;
        if (memory == null || exchange == null) return false;
        var ingredients = recipe.Ingredients.Where(CardCatalog.All.ContainsKey)
            .Select(id => BoardArt.LoadCardArtwork(CardCatalog.Get(id), current.Players[entry.Actor].RelikarioId)).OfType<Texture2D>().ToArray();
        Rect2? destination = entry.CardId == CardCatalog.SuperMagnet.Id
            ? ZoneNode(relative, CardZone.Equipment, -1) is ZoneCardButton equipment && equipment.IsVisibleInTree() ? ZoneRect(equipment) : null
            : entry.CardId == CardCatalog.SuperAttack.Id ? relative == 0 ? HandLanding() : Find("OpponentHandFan") ?? Find("OpponentHandScroll") : null;
        _impacts.TrapSummon(ingredients, memory.Value.GetCenter(), exchange.Value.GetCenter(), art, destination, BoardArt.Gold);
        if (entry.CardId == CardCatalog.SuperAttack.Id && destination != null)
            HideUntilSummonLands(current, entry.Actor, relative);
        return destination != null;
    }

    /// <summary>
    /// The board was rebuilt with Súper Ataque already in the hand; that card stays hidden while its echo is
    /// summoned and fades in as the flying Trap lands on it, so the card is never seen before it arrives.
    /// </summary>
    private void HideUntilSummonLands(DuelSnapshot current, int actor, int relative)
    {
        var hand = current.Players[actor];
        var slot = relative == 0 ? Array.LastIndexOf(hand.Hand, CardCatalog.SuperAttack.Id) : hand.HandCount - 1;
        if (slot < 0 || ZoneNode(relative, CardZone.Hand, slot) is not { } card) return;
        HiddenSummonedCard = card;
        card.Modulate = new Color(1, 1, 1, 0);
        var tween = card.CreateTween();
        tween.TweenInterval(ImpactLayer.SummonLanding);
        tween.TweenProperty(card, "modulate:a", 1f, ImpactLayer.SummonLandingFade);
    }

    /// <summary>For tests: the hand card kept hidden while the Trap summon flies to it.</summary>
    internal Control? HiddenSummonedCard { get; private set; }

    /// <summary>Where a card joining the own hand lands: the visible band of the fan, at board card size.</summary>
    private Rect2? HandLanding()
    {
        if (_hand == null || !IsInstanceValid(_hand) || !_hand.IsVisibleInTree()) return null;
        var hand = _hand.GetGlobalRect();
        var top = Math.Min(hand.Position.Y + 20, GlobalPosition.Y + Size.Y - 160);
        return new Rect2(hand.GetCenter().X - 60, top, 120, 160);
    }
}
