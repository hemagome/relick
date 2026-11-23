namespace Relick.Core;

public sealed partial class GameEngine
{
    private const string LetterMode = "letter-mode";
    private const string GoldSearch = "gold-search";
    private const string GoldEvolution = "gold-evolution";

    private static bool IsLetter(string id) => CardCatalog.Get(id).Letter.HasValue;
    private static int RelikarioLetterCount(PlayerState p) => p.AttachedCards.Count(IsLetter);
    private static int DistinctRelikarioLetters(PlayerState p) => p.AttachedCards.Select(id => CardCatalog.Get(id).Letter)
        .OfType<char>().Distinct().Count();

    private void CheckLetterVictory(int actor)
    {
        var p = State.Players[actor];
        if (DistinctRelikarioLetters(p) == 6)
            Finish(actor, $"{p.Name} reúne las seis letras de RELICK encima de su Relikario.");
    }

    private bool HasLetterRamResponse(int actor, bool allowOtherCombos, bool opening = false) => ComboResponsesEnabled(actor) &&
        PlayableCards(actor).Any(entry =>
            (allowOtherCombos || CardCatalog.Get(entry.Id).Letter.HasValue) &&
            (opening ? IsLetterRamCombo(CardCatalog.Get(entry.Id)) : CanJoinLetterRam(actor, CardCatalog.Get(entry.Id))) &&
            (CodeUnavailableReason(actor, entry.Index) == null ||
             CodeUnavailableReason(actor, entry.Index, placeLetter: true) == null));

    private bool CanJoinLetterRam(int actor, CardDefinition card) => State.ErrorResponder == actor &&
        IsLetterRamCombo(card);

    private static bool IsLetterRamCombo(CardDefinition card) => card.Kind == CardKind.Code && card.IsCombo &&
        !card.CancelActivation && card.Id != CardCatalog.CopyHelmet.Id && card.Id != CardCatalog.ComboParry.Id &&
        !card.RepeatStage && !card.DiscardForTurnBuff && !card.RerollOwnDie && card.SetOwnDie == 0;

    private CommandResult ActivateLetterRamCard(int actor, int index, CardDefinition card, bool place, int actions, bool discarded = false)
    {
        var p = State.Players[actor];
        var joinsRam = State.Phase == Phase.ErrorResponse && _activationChain.Count > 0 && CanJoinLetterRam(actor, card);
        var frame = new EngineActivation(actor, card.Id, actions, place ? 0 : CodePrCost(card),
            joinsRam ? _errorResumePhase : State.Phase)
        {
            PlaceLetter = place,
            DiscardedToActivate = discarded,
            Parent = joinsRam ? null : _activeActivation
        };
        HoldActivationCard(frame, index);
        p.ActionsLeft -= frame.Actions;
        p.Relikia -= frame.Pr;
        AnnounceLetterMode(frame);
        if (joinsRam)
        {
            _activationChain.Add(frame);
            State.ActivationChain.Add(new(actor, card.Id, frame.Actions, frame.Pr, place));
            GiveRamPriority(1 - actor);
        }
        else BeginActivation(frame);
        return CommandResult.Ok();
    }

    private void AnnounceLetterMode(EngineActivation frame)
    {
        var card = CardCatalog.Get(frame.CardId);
        var text = $"{State.Players[frame.Actor].Name} activa {card.Name}" +
            (frame.PlaceLetter ? $" para colocar la {card.Letter} encima de su Relikario." :
                card.Letter.HasValue ? " para resolver su efecto." : ".");
        Log(text);
        if (card.Letter.HasValue) State.LastCombat = text;
    }

    private void ResolveCodeMode(int actor, CardDefinition card)
    {
        if (_activeActivation is not { PlaceLetter: true } frame)
        {
            ResolveCode(State.Players[actor], card);
            return;
        }
        var p = State.Players[actor];
        var slot = p.AttachedCards.Count;
        p.AttachedCards.Add(InstallActivationCard(frame, p, CardZone.Attached, slot));
        NotifyPileMove(p, card.Id, CardZone.Activation, slot, CardZone.Attached);
        Log($"{p.Name} coloca la {card.Letter} encima de su Relikario.");
        CheckLetterVictory(actor);
        if (!State.IsOver)
        {
            _activeActivation = frame.Parent;
            CompletePendingPlay();
        }
    }

    private void AskLetterMode(int actor, CardDefinition card, int index, int actions, int source = 0)
    {
        AskDecision(new PendingDecision { Source = card.Id, Step = LetterMode, Kind = DecisionKind.Confirm,
            Decider = actor, Value = index, Picks = [actions, source],
            Instruction = $"Elige cómo activar la {card.Letter}." });
    }

    private static DecisionChoice[] LetterModeChoices(string id, int value) =>
    [
        new() { Label = "Resolver el efecto", Button = "Resolver", CardId = id, Value = value,
            Public = $"elige resolver el efecto de {CardCatalog.Get(id).Name}." },
        new() { Label = "Colocar encima de mi Relikario", Button = "Colocar", CardId = id, Value = value,
            Public = $"elige colocar la {CardCatalog.Get(id).Letter} encima de su Relikario." }
    ];

    private IReadOnlyList<DecisionChoice> AvailableLetterModes(PendingDecision decision)
    {
        var card = CardCatalog.Get(decision.Source);
        var choices = LetterModeChoices(card.Id, decision.Value);
        var effectAllowed = card.Letter switch
        {
            'R' => decision.Decider == State.CurrentPlayer,
            'E' => EnergyCodes(decision.Decider, decision.Picks![1] is 0 or 3 ? decision.Value : -1).Any(),
            _ => true
        };
        return effectAllowed ? choices : [choices[1]];
    }

    private void ApplyLetterMode(PendingDecision decision, DecisionChoice choice)
    {
        var card = CardCatalog.Get(decision.Source);
        var actor = decision.Decider;
        var place = choice.Button == "Colocar";
        var actions = decision.Picks![0];
        switch (decision.Picks[1])
        {
            case 0:
                ActivateLetterRamCard(actor, decision.Value, card, place, actions);
                return;
            case 1:
                if (!place) { ActivateCode(actor, card, true, decision.Value); return; }
                var physical = RemoveMemoryCard(new(actor, decision.Value));
                var frame = new EngineActivation(actor, card.Id, actions, RelikiaEffects.Get(RelikiaEffect.ReplayCode).Cost, State.Phase)
                    { PlaceLetter = true, FromMemory = true, HeldCard = physical, MemoryIndex = -1 };
                AnnounceLetterMode(frame);
                BeginActivation(frame);
                return;
            case 2:
                BeginCopyFromMemory(actor, card, place);
                return;
            case 3:
                ActivateDiscardedLetter(actor, decision.Value, card, place);
                return;
        }
    }

    private bool OfferGoldAttackLetter(PendingAttackRoll attack)
    {
        var actor = attack.Attacker;
        var p = State.Players[actor];
        if (p.Relikario != RelikarioCatalog.YunoOro || !attack.NaturalD6Six || attack.Modified ||
            EffectsSuppressed(actor, FieldZone.Relikario)) return false;
        if (!p.Deck.Any(IsLetter))
        {
            Shuffle(p);
            return false;
        }
        AskDecision(new PendingDecision { Source = p.Relikario.Id, Step = GoldSearch, Kind = DecisionKind.ChooseCard,
            Decider = actor, Attack = attack, Instruction = "Busca una letra en tu mazo y añádela a tu mano." });
        return true;
    }

    private void OfferGoldEvolutionLetter(int actor)
    {
        var p = State.Players[actor];
        if (!p.Deck.Concat(p.Memory).Concat(p.CorruptedMemory).Any(IsLetter)) return;
        AskDecision(new PendingDecision { Source = p.Relikario.Id, Step = GoldEvolution, Kind = DecisionKind.ChooseCard,
            Decider = actor, Instruction = "Coloca una letra de tu mazo, memoria o corruptas encima de tu Relikario." });
    }

    private static IEnumerable<(CardZone Zone, int Index, string Id)> LetterSources(PlayerState p, bool fromDeckOnly)
    {
        foreach (var (zone, cards) in fromDeckOnly ? new[] { (CardZone.Deck, p.Deck) } :
            [(CardZone.Deck, p.Deck), (CardZone.Memory, p.Memory), (CardZone.Corrupted, p.CorruptedMemory)])
            for (var i = 0; i < cards.Count; i++)
                if (IsLetter(cards[i])) yield return (zone, i, cards[i]);
    }

    private IReadOnlyList<DecisionChoice> GoldRelikarioChoices(PendingDecision decision) =>
        LetterSources(State.Players[decision.Decider], decision.Step == GoldSearch).Select((entry, i) => new DecisionChoice
        {
            CardId = entry.Id, Value = i, Label = $"{CardCatalog.Get(entry.Id).Name} · " + (entry.Zone switch
            { CardZone.Deck => "Mazo", CardZone.Memory => "Memoria", _ => "Corruptas" }),
            Button = decision.Step == GoldSearch ? "Añadir a la mano" : "Colocar",
            Public = decision.Step == GoldSearch ? "añade una letra de su mazo a su mano." : ""
        }).ToArray();

    private void ApplyGoldRelikarioChoice(PendingDecision decision, DecisionChoice choice)
    {
        var actor = decision.Decider;
        var p = State.Players[actor];
        var (zone, index, id) = LetterSources(p, decision.Step == GoldSearch).ElementAt(choice.Value);
        if (decision.Step == GoldSearch)
        {
            var next = new EngineContinuation { Kind = ContinuationKind.ResolveSkillAttackAfterLetter,
                Actor = actor, Attack = decision.Attack };
            MoveSearchedDeckCardToHand(p, index, next);
            return;
        }
        var marked = p.CorruptsOnMemory(zone, index);
        if (zone == CardZone.Deck)
        {
            p.Deck.RemoveAt(index);
            p.RemoveCorruptionSlots(zone, index);
            Shuffle(p);
        }
        else if (zone == CardZone.Memory) RemoveMemoryCard(new(actor, index));
        else p.CorruptedMemory.RemoveAt(index);
        var slot = p.AttachedCards.Count;
        if (marked) p.MarkForCorruption(CardZone.Attached, slot);
        p.AttachedCards.Add(id);
        NotifyPileMove(p, id, zone, slot, CardZone.Attached, sourceIndex: index);
        Log($"{p.Name} coloca la {CardCatalog.Get(id).Letter} encima de su Relikario al evolucionar.");
        CheckLetterVictory(actor);
        if (!State.IsOver && zone == CardZone.Deck && p.Deck.Count == 0) Exhaust(p);
    }

    private void BeginCopyFromMemory(int actor, CardDefinition card, bool place)
    {
        var frame = new EngineActivation(actor, card.Id, 0, 0, State.Phase)
        {
            PlaceLetter = place,
            FieldCardId = FieldInstance(card.Id, null),
            MemoryIndex = -1,
            Kind = card.Kind == CardKind.Equipment ? ActivationKind.Equipment : ActivationKind.Code,
            EquipOpponent = card.Id == CardCatalog.ThiefGhost.Id
        };
        if (card.Kind == CardKind.Area)
        {
            var p = State.Players[actor];
            p.Area = frame.FieldCardId;
            NotifyPileMove(p, card.Id, CardZone.Activation, destination: CardZone.Area);
            CompletePendingPlay();
            return;
        }
        AnnounceLetterMode(frame);
        if (card.DiscardForTurnBuff || card.RepeatStage) BeginTurnBuff(frame);
        else BeginActivation(frame);
    }

    private IEnumerable<(int Index, string Id)> EnergyCodes(int actor, int excludedHand)
    {
        var p = State.Players[actor];
        return PlayableCards(actor).Where(entry => entry.Field == null && entry.Index != excludedHand)
            .Where(entry =>
            {
                var card = CardCatalog.Get(entry.Id);
                if (card.Kind != CardKind.Code || card.CodeType == CodeType.Trap) return false;
                if (card.Id == CardCatalog.LetterE.Id || card.Letter.HasValue) return true;
                return CodeEffectUnavailable(p, card, excludedHand: entry.Index) == null;
            }).Select(entry => (entry.Index, entry.Id));
    }

    private void ActivateEnergyCode(int actor, int index)
    {
        var card = CardCatalog.Get(State.Players[actor].Hand[index]);
        ReleaseActiveActivation();
        if (card.Letter.HasValue) AskLetterMode(actor, card, index, 0, source: 3);
        else
        {
            Log($"{State.Players[actor].Name} descarta {card.Name} para activarlo con Energía Combo.");
            ActivateHandCode(actor, index, actionCost: 0, discardToActivate: true);
        }
    }

    private void ActivateDiscardedLetter(int actor, int index, CardDefinition card, bool place)
    {
        Log($"{State.Players[actor].Name} descarta {card.Name} para activarlo con Energía Combo.");
        ActivateLetterRamCard(actor, index, card, place, 0, discarded: true);
    }

    private void ResolveMonkeyName(int actor, int index)
    {
        var rival = 1 - actor;
        var player = State.Players[actor];
        var id = State.Players[rival].Hand[index];
        var name = CardCatalog.Get(id).Name;
        if (name.Contains(player.Relikario.Name[0], StringComparison.OrdinalIgnoreCase))
            MoveCardBetweenHands(State.Players[rival], player, index);
        else
        {
            var owner = ReturnHandCardToDeck(rival, index);
            if (owner >= 0) CardsReturnedToDeck?.Invoke(owner);
        }
    }

    private void CorruptHand(int owner, int index)
    {
        var p = State.Players[owner];
        p.MarkForCorruption(CardZone.Hand, index);
        SendHandToMemory(p, index);
    }

    private void CorruptField(int actor, FieldTarget target, EngineContinuation then)
    {
        then.CorruptTarget = true;
        if (!TryReplaceOpponentFieldEffect(actor, target, then)) RequestDestruction(target, then);
    }

    private void MarkFieldForCorruption(FieldTarget target)
    {
        if (target.Zone == FieldZone.TomeCard)
        {
            var slot = State.TomeCards?.FindIndex(card => card.Slot == target.Index) ?? -1;
            if (slot >= 0) State.TomeCards![slot] = State.TomeCards[slot] with { Marked = true };
        }
        else if (target.Zone != FieldZone.Zahira)
        {
            var p = State.Players[target.Owner];
            var index = target.Zone == FieldZone.Equipment && target.Index < 0 ? p.EquipmentStackedCopies : target.Index;
            p.MarkForCorruption(MovementCardZone(target.Zone), index);
        }
    }

    private List<int> MillDeckTop(int actor, int count)
    {
        var p = State.Players[actor];
        var milled = new List<int>();
        for (var i = Math.Min(count, p.Deck.Count); i > 0; i--)
        {
            var id = p.Deck[0];
            var landed = SendToMemory(p, id, CardZone.Deck, 0);
            p.Deck.RemoveAt(0);
            p.RemoveCorruptionSlots(CardZone.Deck, 0);
            if (landed is { } slot) milled.Add(slot.Index);
        }
        return milled;
    }

    private void BottomMemoryCard(int owner, int index)
    {
        var p = State.Players[owner];
        var id = RemoveMemoryCard(new(owner, index));
        p.Deck.Add(id);
        NotifyPileMove(p, id, CardZone.Memory, index, CardZone.Deck);
        CardsReturnedToDeck?.Invoke(owner);
    }

    private IEnumerable<string> CloneableMemory(int actor)
    {
        var p = State.Players[actor];
        return MindResetMemories().Where(entry => entry.Owner == actor).Select(entry => p.Memory[entry.Index]).Distinct()
            .Where(id =>
            {
                var card = CardCatalog.Get(id);
                return card.Kind switch
                {
                    CardKind.Area => p.Area == null,
                    CardKind.Equipment => !card.Stackable || p.Equipment == null,
                    CardKind.Code => card.CodeType != CodeType.Trap &&
                        (card.Letter.HasValue || CodeEffectUnavailable(p, card, excludedHand: int.MaxValue) == null),
                    _ => false
                };
            });
    }

    private void CloneMemoryCard(int actor, string id)
    {
        ReleaseActiveActivation();
        var card = CardCatalog.Get(id);
        if (card.Letter.HasValue) AskLetterMode(actor, card, -1, 0, source: 2);
        else BeginCopyFromMemory(actor, card, false);
    }

    private void OverlayMemoryCard(int owner, int index, FieldTarget host, bool above)
    {
        var source = State.Players[owner];
        var holder = State.Players[host.Owner];
        var id = RemoveMemoryCard(new(owner, index));
        if (host.Zone == FieldZone.Relikario && above && IsLetter(id))
        {
            var fieldId = FieldInstance(id, id);
            var slot = holder.AttachedCards.Count;
            if (owner != host.Owner) MarkBorrowedFieldCard(holder, fieldId);
            holder.AttachedCards.Add(fieldId);
            NotifyPileMove(source, id, CardZone.Memory, slot, CardZone.Attached, destinationPlayer: host.Owner, sourceIndex: index);
            CheckLetterVictory(host.Owner);
            return;
        }
        var child = new TomeCard(NextFieldInstance(), host.CardId, id, owner, false, OverlayHost: host, Above: above);
        (State.TomeCards ??= []).Add(child);
        NotifyPileMove(source, id, CardZone.Memory, child.Slot, CardZone.TomeCard, destinationPlayer: host.Owner, sourceIndex: index);
    }

    private void ChangeOverlayHosts(int owner, FieldZone zone, int index, int count, FieldTarget? destination)
    {
        if (State.TomeCards == null) return;
        foreach (var child in State.TomeCards.ToArray())
        {
            if (child.OverlayHost is not { } host || host.Owner != owner || host.Zone != zone || host.Index < index) continue;
            if (host.Index >= index + count && zone != FieldZone.TomeCard)
                State.TomeCards[State.TomeCards.IndexOf(child)] = child with { OverlayHost = host with { Index = host.Index - count } };
            else if (host.Index - index < count)
            {
                if (destination != null)
                    State.TomeCards[State.TomeCards.IndexOf(child)] = child with { OverlayHost = destination };
                else
                {
                    RemoveTomeChild(child.Slot);
                    ChangeZahiraHosts(owner, FieldZone.TomeCard, child.Slot);
                    SendToMemory(State.Players[child.OriginalOwner], child.CardId, CardZone.TomeCard, child.Slot, marked: child.Marked);
                }
            }
        }
    }
}
