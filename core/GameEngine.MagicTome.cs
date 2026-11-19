namespace Relick.Core;

public sealed record TomeCard(int Slot, string HostId, string CardId, int OriginalOwner, bool Marked, bool Inverted = false);

public sealed partial class GameEngine
{
    private const int TomeHandBase = 1000;
    private int NextFieldInstance() => ++State.FieldInstanceSequence;
    private string FieldInstance(string text, string? physical) => $"{text}~{physical}~{NextFieldInstance()}";

    private void DeployMagicTome(int actor)
    {
        var p = State.Players[actor];
        var frame = _activeActivation!;
        frame.FieldCardId ??= FieldInstance(frame.CardId, frame.HeldCard ?? frame.CardId);
        var id = InstallActivationCard(frame, p, CardZone.Attached, p.AttachedCards.Count);
        p.AttachedCards.Add(id);
        NotifyPileMove(p, CardCatalog.MagicTome.Id, CardZone.Activation, p.AttachedCards.Count - 1, CardZone.Attached);
        Log($"{p.Name} coloca Tomo de Magia encima de su Relikario.");
        Continue(new EngineContinuation { Kind = ContinuationKind.LoadMagicTome, Actor = actor, Text = id, Count = 2 });
    }

    private void LoadMagicTome(EngineContinuation load)
    {
        var p = State.Players[load.Actor];
        while (load.Count > 0)
        {
            if (p.Deck.Count == 0) { Exhaust(p, load); return; }
            var id = p.Deck[0];
            var slot = NextFieldInstance();
            (State.TomeCards ??= []).Add(new(slot, load.Text, id, load.Actor, p.CorruptsOnMemory(CardZone.Deck, 0)));
            p.Deck.RemoveAt(0);
            p.RemoveCorruptionSlots(CardZone.Deck, 0);
            load.Count--;
            NotifyPileMove(p, id, CardZone.Deck, slot, CardZone.TomeCard);
            if (p.Deck.Count == 0) { Exhaust(p, load); return; }
        }
    }

    private IEnumerable<TomeCard> TomeChildren(string host) => (State.TomeCards ?? []).Where(card => card.HostId == host).OrderBy(card => card.Slot);

    private TomeCard[]? TakeTomeAttachments(string host)
    {
        var children = TomeChildren(host).ToArray();
        if (children.Length == 0) return null;
        foreach (var child in children)
        {
            RemoveTomeChild(child.Slot);
            ChangeZahiraHosts(child.OriginalOwner, FieldZone.TomeCard, child.Slot);
        }
        return children;
    }

    private TomeCard? RemoveTomeChild(int slot)
    {
        var card = State.TomeCards?.Find(child => child.Slot == slot);
        if (card == null) return null;
        State.TomeCards!.Remove(card);
        if (State.TomeCards.Count == 0) State.TomeCards = null;
        return card;
    }

    private void InvertTomeCard(TomeCard card)
    {
        State.TomeCards![State.TomeCards.IndexOf(card)] = card with { Inverted = true };
        Log($"{CardCatalog.Get(card.CardId).Name} se invierte sobre Tomo de Magia.");
    }

    internal TomeCardView[]? TomeViews(int owner, int viewer)
    {
        var p = State.Players[owner];
        var cards = new List<TomeCardView>();
        void Add(string id, FieldZone zone, int index)
        {
            foreach (var child in TomeChildren(id))
                {
                var handSlot = TomeHandBase + child.Slot;
                var selectable = viewer == owner && PlayableTomeCard(p, handSlot) != null;
                cards.Add(new(child.Slot, child.CardId, child.Inverted, zone, index, selectable ? handSlot : null));
            }
        }
        for (var i = 0; i < p.AttachedCards.Count; i++) Add(p.AttachedCards[i], FieldZone.Attached, i);
        for (var i = 0; i < p.Exchange.Count; i++) Add(p.Exchange[i], FieldZone.Exchange, i);
        if (p.Bonus is { } bonus) Add(bonus, FieldZone.Bonus, -1);
        return cards.Count == 0 ? null : cards.ToArray();
    }

    private FieldZone? TomeHostZone(int owner, int slot)
    {
        var child = State.TomeCards?.Find(card => card.Slot == slot);
        if (child == null) return null;
        var p = State.Players[owner];
        if (p.AttachedCards.Contains(child.HostId)) return FieldZone.Attached;
        if (p.Bonus == child.HostId) return FieldZone.Bonus;
        return p.Exchange.Contains(child.HostId) ? FieldZone.Exchange : null;
    }

    private IEnumerable<FieldTarget> TomeFieldTargets(int owner)
    {
        var p = State.Players[owner];
        foreach (var child in State.TomeCards ?? [])
            if (p.AttachedCards.Contains(child.HostId) || p.Bonus == child.HostId || p.Exchange.Contains(child.HostId))
                yield return new(owner, FieldZone.TomeCard, child.Slot, child.CardId);
    }

    public IEnumerable<(int Index, string Id, FieldTarget? Field)> PlayableCards(int actor)
    {
        var p = State.Players[actor];
        for (var i = 0; i < p.Hand.Count; i++)
            if (!HandCardLocked(p, i)) yield return (i, p.Hand[i], null);
        foreach (var target in TomeFieldTargets(actor))
            if (PlayableTomeCard(p, TomeHandBase + target.Index) != null)
                yield return (TomeHandBase + target.Index, target.CardId, target);
    }

    private TomeCard? PlayableTomeCard(PlayerState p, int handIndex) =>
        handIndex >= TomeHandBase && State.TomeCards?.Find(c => c.Slot == handIndex - TomeHandBase) is { Inverted: false } card &&
        TomeProtects(p, card) ? card : null;

    private bool TomeProtects(PlayerState p, TomeCard card)
    {
        var parent = p.AttachedCards.IndexOf(card.HostId);
        var actor = Seat(p);
        var active = parent >= 0 ? !EffectsSuppressed(actor, FieldZone.Attached, parent) :
            p.Bonus == card.HostId ? !EffectsSuppressed(actor, FieldZone.Bonus) :
            p.Exchange.IndexOf(card.HostId) is var exchange && exchange >= 0 && !EffectsSuppressed(actor, FieldZone.Exchange, exchange);
        return active && !EffectsSuppressed(actor, FieldZone.TomeCard, card.Slot);
    }

    private string? PlayableHandCard(PlayerState p, int index) => InHand(p, index)
        ? HandCardLocked(p, index) ? null : p.Hand[index]
        : PlayableTomeCard(p, index)?.CardId;

    private static bool HandCardLocked(PlayerState p, int index) => p.LockedGhostCards?.Contains(index) == true;

    private void SendTomeChildrenToMemory(string host)
    {
        if (TakeTomeAttachments(host) is not { } children) return;
        foreach (var child in children)
            SendToMemory(State.Players[child.OriginalOwner], child.CardId, CardZone.TomeCard, child.Slot, marked: child.Marked);
    }

    private void HoldActivationCard(EngineActivation frame, int handIndex)
    {
        var p = State.Players[frame.Actor];
        if (PlayableTomeCard(p, handIndex) is { } tome)
        {
            frame.HeldCard = tome.CardId;
            frame.TomeSource = RemoveTomeChild(tome.Slot);
            frame.MemoryOwner = tome.OriginalOwner;
            frame.CorruptOnMemory = tome.Marked;
            ChangeZahiraHosts(frame.Actor, FieldZone.TomeCard, tome.Slot);
            return;
        }
        frame.HeldCard = p.Hand[handIndex];
        frame.CorruptOnMemory = p.CorruptsOnMemory(CardZone.Hand, handIndex);
        if (p.RemoveHandCard(handIndex)) frame.MemoryOwner = 1 - frame.Actor;
    }

    private string InstallActivationCard(EngineActivation frame, PlayerState p, CardZone zone, int slot, bool light = false)
    {
        var id = frame.FieldCardId ?? frame.CardId;
        if (frame.HeldCard == null && frame.FieldCardId == null)
        {
            MoveMemoryCardToField(new(frame.MemorySeat, frame.MemoryIndex), p, light);
        }
        else
        {
            var borrowed = frame.HeldCard != null && frame.MemorySeat != Seat(p);
            if (light) InstallLightEquipment(p, id, borrowed);
            else if (borrowed && zone != CardZone.GalacticShield) MarkBorrowedFieldCard(p, id);
            frame.HeldCard = null;
            frame.TomeSource = null;
        }
        if (frame.CorruptOnMemory) p.MarkForCorruption(zone, slot);
        frame.CorruptOnMemory = false;
        return id;
    }

    private void FinishHeldCard(EngineActivation frame)
    {
        if (frame.HeldCard is not { } physical) return;
        frame.HeldCard = null;
        var marked = frame.CorruptOnMemory;
        frame.CorruptOnMemory = false;
        if (frame.TomeSource is { } source && TomeProtects(State.Players[frame.Actor], source))
        {
            (State.TomeCards ??= []).Add(source);
            InvertTomeCard(source);
        }
        else SendToMemory(State.Players[frame.MemorySeat], physical, CardZone.Activation, -1, marked: marked);
    }
}
