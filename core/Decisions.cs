using System.Text.Json.Serialization;

namespace Relick.Core;

/// <summary>How a decision is presented: the rules only see its choices.</summary>
public enum DecisionKind
{
    /// <summary>A few buttons: pay or not, keep or not.</summary>
    Confirm,
    /// <summary>A card or zone on the board.</summary>
    ChooseTarget,
    /// <summary>A card shown face up in a gallery, such as one from the deck.</summary>
    ChooseCard
}

/// <summary>
/// One choice of a pending decision. Choices are computed live by the card's effect each time they are needed,
/// so they always reflect the current board and are never stored; only the decider ever receives them.
/// </summary>
public sealed record DecisionChoice
{
    /// <summary>Full description for the decider ("Destruir Hoguera de Beto · equipo").</summary>
    public string Label { get; init; } = "";
    /// <summary>Short button text ("Destruir"); null uses the label.</summary>
    public string? Button { get; init; }
    /// <summary>What every player may read in the public history once it is chosen.</summary>
    public string Public { get; init; } = "";
    public string? CardId { get; init; }
    public FieldTarget? Field { get; init; }
    /// <summary>The player explicitly targeted by an effect when there is no field target.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TargetPlayer { get; init; }
    /// <summary>Whose pile the card comes from (a memory), so a gallery can group it; null for the decider's own cards.</summary>
    public int? Owner { get; init; }
    /// <summary>Effect-specific payload: a deck position, whether to pay.</summary>
    public int Value { get; init; }
    public bool Primary { get; init; }
    /// <summary>Keep the decision visible until the player has seen the information the effect shows.</summary>
    public bool RequiresAcknowledgement { get; init; }
}

/// <summary>
/// The single decision phase that replaces a phase per card. It records which card asks, which step of that
/// card's effect continues with the answer and who answers; the card's <see cref="CardEffect"/> computes the choices.
/// </summary>
public sealed class PendingDecision
{
    public string Source { get; set; } = "";
    public string Step { get; set; } = "";
    public int Decider { get; set; }
    public DecisionKind Kind { get; set; }
    public string Instruction { get; set; } = "";
    /// <summary>COMBO cards may answer while this decision waits, as they could during the old target choices.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllowsCombos { get; set; }
    public Phase ResumePhase { get; set; }
    /// <summary>A number the effect needs to list its choices, such as the PR a die decided.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Value { get; set; }
    /// <summary>What the effect chose in earlier steps of a multi-step decision, such as memory cards already picked.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Picks { get; set; }
    /// <summary>A hand only the decider may see while this decision waits, as Resetear la Mente lets its player look at one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShowsHandOf { get; set; }
    /// <summary>The decision a COMBO interrupted; it waits underneath and comes back once this one is answered.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingDecision? Suspended { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingAttackRoll? Attack { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EngineContinuation? Continuation { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TargetResponse? TargetResponse { get; set; }

    public string Title() => CardCatalog.All.TryGetValue(Source, out var card) ? card.Name : RelikarioCatalog.NameOf(Source);
}

/// <summary>What both players see about the pending decision; its choices only reach the decider, as options.</summary>
public sealed class DecisionView
{
    public string Source { get; set; } = "";
    public int Decider { get; set; }
    public DecisionKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Instruction { get; set; } = "";
}

/// <summary>
/// The rules of one card that the engine consults outside an activation: the choices of a decision the card opened
/// (on its own or from a trigger in the engine), the answer, and how it resumes after an operation that may pause
/// (a destruction someone can protect, a Bonus refill). Steps are plain names so a saved duel can resume them.
/// </summary>
internal abstract class CardRules
{
    public abstract string CardId { get; }
    /// <summary>Why this card cannot be played as a code, beyond the shared costs.</summary>
    public virtual string? Unavailable(GameEngine.EffectContext ctx, int excludedHand) => null;
    /// <summary>The choices of a decision this card opened.</summary>
    public virtual IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision) => [];
    public virtual void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice) { }
    /// <summary>Resumes the card at <paramref name="step"/>; only a card that pauses with <c>Then</c> or <c>Roll</c> needs it.</summary>
    public virtual void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next) { }
}

/// <summary>
/// A card whose rules live in one place, including its activation: the engine checks <see cref="Unavailable"/> before
/// it can be played and calls <see cref="Resolve"/> when it resolves. A card the player never activates (an equipment
/// or Area that only reacts) derives from <see cref="CardRules"/> instead, so the engine never resolves it as a code.
/// </summary>
internal abstract class CardEffect : CardRules
{
    /// <summary>The physical copy waits outside memory while its activation resolves.</summary>
    public virtual bool HoldsActivationCard => false;
    public abstract void Resolve(GameEngine.EffectContext ctx);
}

/// <summary>A COMBO that is offered when an opponent's effect selects the player or a card they control.</summary>
internal interface ITargetResponse { }

/// <summary>Moments of the turn when a card in play may act. Each moment's triggers run in their <c>Order</c>.</summary>
internal enum TurnMoment { TurnStart, ActionStart, FinalStage }

/// <summary>
/// A card that acts at a moment of the turn without being activated (see docs/CARD_TRIGGERS.md). The engine fires the
/// moment's triggers in <see cref="Order"/>; one that starts work which comes back later returns true and, when it
/// ends, hands off with <c>EffectContext.FinishTurnTrigger</c>.
/// </summary>
internal interface ITurnTrigger
{
    TurnMoment Moment { get; }
    /// <summary>Position within the moment; the rules fix it, never the registration order.</summary>
    int Order { get; }
    /// <summary>Starts or resumes this card's work for the context's player; false when it has nothing to do now.</summary>
    bool Fire(GameEngine.EffectContext ctx);
}

/// <summary>How a card would leave the field.</summary>
internal enum FieldLeave { Destroy, Return }

/// <summary>Order of the replacements when a card would leave the field; the rules fix it.</summary>
internal enum LeaveStep { Protect, Cover }

/// <summary>
/// A card that may replace another card leaving the field (see docs/CARD_TRIGGERS.md): Escudo Galáctico protects what
/// sits on the Relikario, Tótem de Oro takes a Tótem de Salvación's place. Reverse Shuffle still answers in the engine.
/// </summary>
internal interface IFieldLeaveReplacement
{
    LeaveStep Step { get; }
    /// <summary>True when it took over: it continues <paramref name="then"/> itself, or asks the engine to go on from its step.</summary>
    bool TryReplace(GameEngine.EffectContext ctx, FieldTarget target, FieldLeave leave, EngineContinuation? then);
}

/// <summary>
/// The steps of the damage chain, in the order the engine applies them (docs/CARD_TRIGGERS.md). Each step is either
/// the engine's own code or the cards registered for it; the engine resumes the chain from a step after a pause.
/// </summary>
internal enum DamageStep
{
    /// <summary>Estrella del Fontanero of the attacker doubles the damage.</summary>
    Amplify,
    /// <summary>Estrella del Fontanero of the defender prevents it.</summary>
    Prevent,
    /// <summary>The equipment's own reduction or prevention.</summary>
    Guard,
    /// <summary>Turbo Sword's defense die.</summary>
    Respond,
    /// <summary>Cards that subtract from the damage without asking (Robotizar).</summary>
    Reduce,
    /// <summary>Anillo Morado: halve the damage or gain PR.</summary>
    Absorb,
    /// <summary>Escudo Galáctico: take a face-down card and subtract 2.</summary>
    Shield,
    /// <summary>The non-lethal limit, then equipment that gathers counters from damage (Mira de Sniper).</summary>
    Counters,
    /// <summary>Tótem de Salvación; after it, the Relikario's health.</summary>
    Lethal
}

/// <summary>The damage on its way through the chain.</summary>
internal sealed record DamageEvent(int Target, int Amount, int AttackDie)
{
    /// <summary>The attacking seat when the damage comes from an attack die, as the guard feedback names it.</summary>
    public int? Attacker => AttackDie > 0 ? 1 - Target : null;
}

/// <summary>A card that acts on damage at its step of the chain; the context's player is the one receiving it.</summary>
internal interface IDamageTrigger
{
    DamageStep Step { get; }
    /// <summary>Changes the damage without pausing the chain.</summary>
    int Modify(GameEngine.EffectContext ctx, DamageEvent damage) => damage.Amount;
    /// <summary>
    /// May pause the chain (a decision, a die). True when it took over: it resumes the chain later with
    /// <c>EffectContext.ContinueDamage</c>, or ends it with <paramref name="completed"/>.
    /// </summary>
    bool TryPause(GameEngine.EffectContext ctx, DamageEvent damage, EngineContinuation? completed) => false;
}

internal static class CardEffects
{
    private static readonly IReadOnlyDictionary<string, CardRules> Registered =
        new CardRules[] { new BlueEggEffect(), new TimeBombEffect(), new KonamyCodeEffect(), new ExtraLifeEffect(),
            new PointsIncreaseEffect(), new QuickLoadEffect(), new FloppyDiskEffect(), new SenshuniEffect(), new MindSwapEffect(),
            new ReloadCartridgeEffect(), new TripleAttackEffect(), new MindResetEffect(),
            new DodgeEffect(), new GoldenTotemEffect(), new TakeEquipmentEffect(), new GalacticShieldEffect(),
            new VictoriousManualControlEffect(), new EllieEffect(), new RobotizeEffect(), new ComboParryEffect(), new NoOptionsEffect(),
            new DestinyPickEffect(), new MagicTomeEffect(), new GlassFluteEffect(), new Compass16BitEffect(), new GhostDogEffect(),
            new DualityCassetteEffect(), new GalacticEnergyCoreEffect(), new SecretRoomEffect(),
            new MouseBikeEffect(), new ThiefGhostEffect(), new FutureEraEffect(), new PlatformsEffect(), new ZahiraFlairEffect(), new PurpleRingEffect(),
            GuardEquipmentEffect.Bonfire, GuardEquipmentEffect.DiamondHelmet, GuardEquipmentEffect.MetalHood, new SniperScopeEffect(), new TurboSwordEffect(), new SalvationTotemEffect() }.ToDictionary(e => e.CardId);

    public static CardRules? For(string? cardId) => cardId != null && Registered.TryGetValue(cardId, out var effect) ? effect : null;

    private static readonly IReadOnlyDictionary<TurnMoment, CardRules[]> TurnTriggerTable = Registered.Values
        .OfType<ITurnTrigger>().GroupBy(t => t.Moment)
        .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Order).Cast<CardRules>().ToArray());

    private static readonly ILookup<DamageStep, CardRules> DamageTriggerTable = Registered.Values
        .OfType<IDamageTrigger>().ToLookup(t => t.Step, t => (CardRules)t);

    /// <summary>The cards that change damage at <paramref name="step"/>.</summary>
    public static IEnumerable<CardRules> DamageTriggers(DamageStep step) => DamageTriggerTable[step];

    /// <summary>The cards that may replace a card leaving the field, in the order the rules fix.</summary>
    public static IReadOnlyList<CardRules> LeaveReplacements { get; } = Registered.Values
        .OfType<IFieldLeaveReplacement>().OrderBy(r => r.Step).Cast<CardRules>().ToArray();

    /// <summary>The cards that act at <paramref name="moment"/>, in the order the rules fix.</summary>
    public static IReadOnlyList<CardRules> TurnTriggers(TurnMoment moment) => TurnTriggerTable.GetValueOrDefault(moment) ?? [];
}

public sealed partial class GameEngine
{
    /// <summary>
    /// The operations a card effect may use. They are the engine's own, so a migrated card behaves exactly like
    /// the code it replaces: same random draws, same replacements (Súper Imán, Reverse Shuffle, Tótem de Oro).
    /// </summary>
    internal sealed class EffectContext
    {
        private readonly GameEngine _engine;
        internal EffectContext(GameEngine engine, int actor, string cardId) { _engine = engine; Actor = actor; Card = CardCatalog.Get(cardId); }

        public int Actor { get; }
        public CardDefinition Card { get; }
        public GameState State => _engine.State;
        public PlayerState Player => State.Players[Actor];
        public string PlayerName(int seat) => State.Players[seat].Name;

        public void Log(string text) => _engine.Log(text);
        public void Announce(string text) { State.LastCombat = text; _engine.Log(text); }

        /// <summary>Opens a decision for <see cref="Actor"/> (or <paramref name="decider"/>); the card's <c>Choices</c> and <c>Choose</c> handle <paramref name="step"/>.</summary>
        public void Ask(string step, DecisionKind kind, string instruction, bool allowsCombos = false, bool announce = true, int value = 0,
            List<int>? picks = null, int? showsHandOf = null, EngineContinuation? continuation = null, int? decider = null)
        {
            _engine.AskDecision(new PendingDecision { Source = Card.Id, Step = step, Decider = decider ?? Actor, Kind = kind,
                Instruction = instruction, AllowsCombos = allowsCombos, Value = value, Picks = picks, ShowsHandOf = showsHandOf,
                Continuation = continuation }, announce);
        }

        /// <summary>Continues the card at <paramref name="step"/> after the operation finishes, or right away.</summary>
        public EngineContinuation Then(string step, FieldTarget? target = null) =>
            new() { Kind = ContinuationKind.CardEffectStep, Actor = Actor, Text = Card.Id, Step = step, Target = target };

        public void Continue(EngineContinuation? next) => _engine.Continue(next);
        /// <summary>Goes back to the engine's pending play: the action stage, a RAM resolution or a die waiting for answers.</summary>
        public void CompletePendingPlay() => _engine.CompletePendingPlay();
        public bool EffectsSuppressed(int owner, FieldZone zone, int index = -1) => _engine.EffectsSuppressed(owner, zone, index);
        /// <summary>The draw of the turn's draw stage, with <paramref name="extraCards"/> beyond the usual one (three with an empty hand).</summary>
        public void DrawAtTurnStart(int extraCards) => _engine.DrawAtTurnStart(extraCards: extraCards);
        /// <summary>Moves a hand card of this player to the rival's hand.</summary>
        public void GiveHandCard(int handIndex) => _engine.MoveCardBetweenHands(Player, State.Players[1 - Actor], handIndex);
        /// <summary>Activates a copy of <paramref name="cardId"/> from outside the game for <paramref name="seat"/>, as Era Futurista does.</summary>
        public void ActivateOutOfGameCopy(int seat, string cardId) => _engine.BeginActivation(new EngineActivation(seat, cardId, 0, 0, State.Phase)
        {
            FieldCardId = _engine.FieldInstance(cardId, cardId), HeldCard = cardId
        });

        /// <summary>Destroys a card through every replacement; <paramref name="then"/> learns whether it was prevented.</summary>
        public void Destroy(FieldTarget target, EngineContinuation then)
        {
            if (!_engine.TryReplaceOpponentFieldEffect(Actor, target, then)) _engine.RequestDestruction(target, then);
        }

        public void Draw(int count, EngineContinuation? then = null) => _engine.DrawCards(Player, count, then);
        public void ReturnMemoryCardsToDeck(IReadOnlyList<int> indices) => _engine.ReturnMemoryCardsToDeck(Actor, indices);
        /// <summary>Finishes the held physical card by corruption. A copy replayed with Relikia is already corrupt.</summary>
        public void CorruptActivationCard()
        {
            if (_engine._activeActivation is { } frame) _engine.FinishHeldCard(frame, corrupt: true);
        }
        public void DealNonlethalDamage(int amount, EngineContinuation then)
        {
            then.NonlethalDamageTarget = 1 - Actor;
            _engine.DealDamage(1 - Actor, amount, completed: then, source: Actor);
        }
        public void CorruptMemoryCard(int owner, int index) => _engine.Corrupt(Actor, owner, index);
        public int Heal(int amount) => _engine.Heal(Player, amount);
        /// <summary>Swaps the whole hands of both players; each copy keeps its owner and whether it was revealed.</summary>
        public void SwapHands() => _engine.SwapHands(Actor);
        /// <summary>Both players discard their hands and draw this card's <c>Draw</c>, with Mira de Sniper triggers in between.</summary>
        public void ReloadBothHands() => _engine.BeginReloadHands(Actor);
        public bool CanRespondToTarget() => _engine.WaitingForTargetResponse(Actor);
        public void DiscardHandExceptRandomCard(int seat)
        {
            var player = State.Players[seat];
            var count = player.Hand.Count;
            var discarded = count > 1 ? _engine.DiscardHandCards(seat, Random(count)) : [];
            Announce($"{Card.Name} · {player.Name} descarta {Math.Max(0, count - 1)} carta(s).");
            _engine.BeginSniperChoices(seat, discarded);
        }
        /// <summary>Shows these Tótems de Salvación and places them with Tótem de Oro on top, through the engine's placement sequence.</summary>
        public void DeployGoldenTotem(int[] handIndices) => _engine.DeployGoldenTotem(Actor, handIndices);
        public void DeployGalacticShield() => _engine.DeployGalacticShield(Actor);
        /// <summary>Moves the activating card to this player's <paramref name="zone"/> slot, keeping its owner and corruption mark; returns its instance id.</summary>
        public string InstallActivationCard(CardZone zone, int slot) => _engine.InstallActivationCard(_engine._activeActivation!, Player, zone, slot);
        /// <summary>Tells the presentation that a card of this player moved between piles; it does not move the card.</summary>
        public void NotifyPileMove(string id, CardZone source, int index = -1, CardZone destination = CardZone.Memory,
            int? destinationPlayer = null, int sourceIndex = 0) =>
            _engine.NotifyPileMove(Player, id, source, index, destination, destinationPlayer: destinationPlayer, sourceIndex: sourceIndex);
        /// <summary>Ends this card's work at <paramref name="moment"/>: the moment's later triggers run, then the engine's pending play.</summary>
        public void FinishTurnTrigger(TurnMoment moment) => _engine.ContinueTurnTriggers(moment, Actor, Card.Id);
        /// <summary>How many copies of this card sit on top of this player's Relikario with their effects active.</summary>
        public int ActiveAttachedCopies() => _engine.ActiveAttachedCopies(Actor, Card.Id).Count();
        /// <summary>This player's equipment, when its effects are active.</summary>
        public CardDefinition? ActiveEquipment() => _engine.ActiveEquipment(Actor);
        /// <summary>Tells the presentation that this card changed damage to this player from <paramref name="original"/> to <paramref name="final"/>.</summary>
        public void NotifyGuard(int? attacker, int original, int final, bool blocked) =>
            _engine.NotifyGuard(Actor, attacker, Card.Id, original, final, blocked);
        /// <summary>The player whose Relikario this card sits on top of, directly or under a Zahira Flair; null elsewhere.</summary>
        public int? RelikarioOwnerBelow(FieldTarget target) => _engine.AboveRelikarioOwner(target);
        /// <summary>Destroys through the field-leave replacements and Reverse Shuffle, without Súper Imán's substitution.</summary>
        public void RequestDestruction(FieldTarget target, EngineContinuation? then, LeaveStep? after = null) =>
            _engine.RequestDestruction(target, then, after);
        /// <summary>Moves a card of the field back to its owner's hand at once, with no replacement.</summary>
        public void ReturnFieldCardNow(FieldTarget target) => _engine.ReturnFieldCardNow(target);
        /// <summary>Sends a card of this player from <paramref name="source"/> to memory, keeping its corruption mark.</summary>
        public void SendToMemory(string id, CardZone source, int index, bool marked) => _engine.SendToMemory(Player, id, source, index, marked: marked);
        /// <summary>Destroys this player's equipment through the replacements, then <paramref name="then"/>.</summary>
        public void DestroyEquipment(EngineContinuation? then, int stackIndex = -1) => _engine.DestroyEquipment(Player, then, stackIndex);
        /// <summary>A continuation that applies the damage straight to this player's health, past the remaining steps.</summary>
        public EngineContinuation HealthDamage(DamageEvent damage, EngineContinuation? completed) => new()
            { Kind = ContinuationKind.ApplyDamage, Actor = damage.Target, Count = damage.Amount, Value = damage.AttackDie, Next = completed };
        /// <summary>Goes on with the damage chain after <paramref name="step"/>, with the damage as it stands.</summary>
        public void ContinueDamage(DamageEvent damage, EngineContinuation? completed, DamageStep step) =>
            _engine.ResumeDamage(damage.Target, damage.Amount, damage.AttackDie, completed, step + 1);
        /// <summary>
        /// Rolls for <paramref name="seat"/> through the shared die window; the engine resolves the result by its purpose,
        /// for cards whose die outcome is still in the engine.
        /// </summary>
        public void RollResolvedByEngine(int seat, DiePurpose purpose, int slot = 0) =>
            _engine.RollWithRelikario(State.Players[seat], purpose, new DieEffect { Actor = seat, Purpose = purpose, Slot = slot });
        /// <summary>Rolls a prepared die effect through the shared window; the engine resolves it by its purpose.</summary>
        public void RollResolvedByEngine(DieEffect effect) => _engine.RollWithRelikario(State.Players[effect.Actor], effect.Purpose, effect);
        public string? PlayableHandCard(int handIndex) => _engine.PlayableHandCard(Player, handIndex);
        /// <summary>Sends this card from the hand to memory and activates it there without paying actions or PR.</summary>
        public void ActivateFromHandForFree(int handIndex, bool choosesDieResult)
        {
            var memory = _engine.SendHandToMemory(Player, handIndex);
            _engine.BeginActivation(new EngineActivation(Actor, Card.Id, 0, 0, State.Phase)
            {
                MemoryIndex = memory?.Index ?? -1, MemoryOwner = memory?.Owner, ChooseDieResult = choosesDieResult
            });
        }
        /// <summary>The player's deck ran out: the shared recycling, then <paramref name="then"/>.</summary>
        public void Exhaust(EngineContinuation? then) => _engine.Exhaust(Player, then);
        public void MoveTopCardToHand(EngineContinuation? then) => _engine.MoveTopCard(Player, bonus: false, then);
        /// <summary>Moves the top card of the deck to the next exchange slot, keeping its corruption mark; the exchange may then complete.</summary>
        public void SendTopCardToExchange(EngineContinuation? then)
        {
            var player = Player;
            var slot = player.Exchange.Count;
            var id = player.Deck[0];
            if (player.CorruptsOnMemory(CardZone.Deck, 0)) player.MarkForCorruption(CardZone.Exchange, slot);
            player.Deck.RemoveAt(0);
            player.RemoveCorruptionSlots(CardZone.Deck, 0);
            _engine.NotifyPileMove(player, id, CardZone.Deck, slot, CardZone.Exchange);
            _engine.AddToExchange(player, id);
            var after = new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = Actor, Next = then };
            if (player.Deck.Count == 0) _engine.Exhaust(player, after);
            else _engine.Continue(after);
        }
        public void FinishComboParry() => _engine.FinishComboParry(Actor);
        public bool CanParryActivation() => _engine.CanParryActivation(Actor);
        public bool CanPlayDestinyPick() => _engine.CanPlayDestinyPick(Actor);
        public IEnumerable<int> PlayableTotems() => _engine.PlayableTotems(Actor);
        public IEnumerable<FieldTarget> ReturnableFieldCards() => _engine.ReturnableFieldCards();
        /// <summary>Every card on the field, Relikarios included.</summary>
        public IEnumerable<FieldTarget> FieldCards() => _engine.FieldCards();
        /// <summary>Súper Imán's substitution when this player's effect would affect a rival card; true when it took the effect's place.</summary>
        public bool ReplaceOpponentFieldEffect(FieldTarget target, EngineContinuation then) => _engine.TryReplaceOpponentFieldEffect(Actor, target, then);
        public string FieldName(FieldTarget target) => _engine.FieldCardName(target);
        public string FieldPlace(FieldTarget target) => FieldCardPlace(State, target);
        public void ReturnFieldCard(FieldTarget target) => _engine.ReturnFieldCard(Actor, target);
        public void DeployMagicTome() => _engine.DeployMagicTome(Actor);

        public IEnumerable<FieldTarget> MovableFieldCards() => _engine.MovableFieldCards(Actor);
        public IEnumerable<FieldTarget> CompatibleFieldDestinations(FieldTarget source) => _engine.CompatibleFieldDestinations(source);
        public void AskFieldDestination(FieldTarget source) => _engine.AskFieldDestination(source);
        public void PayLifeAndMoveFieldCard(FieldTarget source, FieldTarget destination) => _engine.PayLifeAndMoveFieldCard(source, destination);
        public IEnumerable<FieldTarget> RivalEquipment() => _engine.EquipmentTransferTargets(1 - Actor);
        public void TakeEquipment(FieldTarget target, EngineContinuation then)
        {
            if (!_engine.TryReplaceOpponentFieldEffect(Actor, target, then)) _engine.PrepareEquipmentTransfer(target, then);
        }
        public bool FinishEquipmentTransfer(FieldTarget target, EngineContinuation then) => _engine.FinishEquipmentTransfer(Actor, target, then);
        public void AttachAttackFromDeck(int index) => _engine.AttachAttackFromDeck(Player, index);
        /// <summary>The pending attack against this player is negated: the attacker loses a prize already paid and combat goes on.</summary>
        public void NegatePendingAttack()
        {
            var attack = State.PendingAttack!; var attacker = State.Players[attack.Attacker];
            _engine.NotifyGuard(Actor, attack.Attacker, Card.Id, attack.Damage, 0, blocked: true);
            if (!attack.RewardPending) attacker.Relikia = Math.Max(0, attacker.Relikia - CardCatalog.Get(attack.SkillId).Reward);
            State.PendingAttack = null; State.Phase = Phase.Combat;
        }

        /// <summary>
        /// Rolls a D6 through the shared die window (Relikario rerolls, Colgante, PC, destreza, COMBO), then continues the
        /// card at <paramref name="step"/> with the final result in <c>next.Value</c>. The purpose names the roll in the
        /// state and the log exactly as before the card moved to a module.
        /// </summary>
        public void Roll(DiePurpose purpose, string step) =>
            _engine.RollWithRelikario(Player, purpose, new DieEffect { Purpose = purpose, Actor = Actor, SkillId = Card.Id, Step = step });

        /// <summary>The player must discard <paramref name="count"/> cards through the shared discard flow, which owns its triggers.</summary>
        public void RequireDiscard(int count)
        {
            State.DiscardsRemaining = Math.Min(count, Player.Hand.Count);
            _engine._activeActivation = null;
            if (State.DiscardsRemaining == 0) return;
            State.DiscardPlayer = Actor; State.DiscardResumePhase = Phase.Actions; State.Phase = Phase.Discard;
        }

        public IEnumerable<MemorySelection> RecoverableMemoryCards() => _engine.RecoverableMemoryCards();
        public IEnumerable<string> AvailableTrapCards() => _engine.AvailableTrapCards(Actor);
        public void PlaceTrap(string id) => _engine.PlaceTrap(Actor, id);
        /// <summary>Memory cards Resetear la Mente may return: all but a memory code waiting on its responses.</summary>
        public IEnumerable<MemorySelection> ResettableMemoryCards() => _engine.MindResetMemories();
        public string? MindResetUnavailable(int excludedHand) => _engine.MindResetUnavailable(Actor, excludedHand);
        /// <summary>Returns the chosen cards to their owners' decks, shuffles them and lets the target draw; ends this activation first.</summary>
        public void ReturnMindResetCards(int target, IReadOnlyList<MemorySelection> memories, int handIndex, Phase resumePhase) =>
            _engine.ReturnMindResetCards(Actor, target, memories, handIndex, resumePhase, _engine.EndMindResetActivation);
        /// <summary>Ends Resetear la Mente without returning cards; the turn may end now if it was waiting for it.</summary>
        public void EndMindReset() { _engine.EndMindResetActivation(); _engine.CompleteEndTurnIfReady(); }
        /// <summary>Moves a memory card to this player's hand; a card from the rival's memory stays marked as foreign.</summary>
        public string TakeFromMemory(int owner, int index) => _engine.TakeFromMemory(Actor, owner, index, Card.Name);
        public int CorruptDeckTop(int count) => _engine.CorruptDeckTop(Player, count);
        /// <summary>Moves a corrupted card to this player's hand and returns its hand slot; a rival's card stays marked as foreign.</summary>
        public int TakeCorruptedCard(int owner, int index)
        {
            var source = State.Players[owner];
            var id = source.CorruptedMemory[index];
            var slot = Player.Hand.Count;
            source.CorruptedMemory.RemoveAt(index);
            Player.Hand.Add(id);
            if (owner != Actor) _engine.MarkBorrowedHandCard(Player, slot);
            _engine.NotifyPileMove(source, id, CardZone.Corrupted, slot, CardZone.Hand, destinationPlayer: Actor, sourceIndex: index);
            return slot;
        }
        /// <summary>Moves <paramref name="owner"/>'s Bonus to this player's hand, without refilling it.</summary>
        public void TakeBonus(int owner) => _engine.TakeBonus(State.Players[owner], Player);
        /// <summary>Whether this activation lets the player choose the die result instead of rolling.</summary>
        public bool ChoosesDieResult => _engine._activeActivation?.ChooseDieResult == true;
        /// <summary>Whether that memory slot holds the copy being activated, which an effect cannot take from itself.</summary>
        public bool IsActivatingMemoryCard(int owner, int index) => _engine._activeActivation is { } frame &&
            frame.MemorySeat == owner && frame.MemoryIndex == index && frame.HeldCard == null;
        /// <summary>Once the effect commits to a result, Error de Carga can no longer cancel it.</summary>
        public void CloseErrorWindow() { if (_engine._activeActivation != null) _engine._activeActivation.CanInterrupt = false; }
        public void RefillBonus(int owner, EngineContinuation? then) => _engine.RefillBonus(State.Players[owner], then);
        public void ShuffleBonusZoneIntoDecks() => _engine.ShuffleBonusZoneIntoDecks(Player);
        public void MoveDeckCardToBonus(int deckIndex) => _engine.MoveDeckCard(Player, deckIndex, true,
            new EngineContinuation { Kind = ContinuationKind.RefillDone, Actor = Actor,
                Next = new EngineContinuation { Kind = ContinuationKind.CompleteExchange, Actor = Actor } });
        public void MoveDeckCardToHand(int deckIndex, EngineContinuation? then = null) => _engine.MoveDeckCard(Player, deckIndex, false, then);
        public int Random(int exclusiveMax) => _engine._random.Next(exclusiveMax);
        /// <summary>Ends the activation before the effect's last move, as a search does before taking its card.</summary>
        public void EndActivation() => _engine.ReleaseActiveActivation();

        /// <summary>Every non-skill card on the field, as Huevo Azul Explosivo may destroy.</summary>
        public IEnumerable<FieldTarget> NonSkillFieldCards() => _engine.NonSkillFieldCards();
        /// <summary>The zones Bomba de Tiempo may hit; the hand counts as one zone, chosen at random on resolution.</summary>
        public IEnumerable<FieldTarget> ZoneTargets(int excludedOwner = -1, int excludedHand = -1) => _engine.TimeBombTargets(excludedOwner, excludedHand);
        public IEnumerable<int> NonSkillDeckCards() => _engine.SearchableDeckCards(Actor);
    }

    private EffectContext EffectFor(int actor, string cardId) => new(this, actor, cardId);

    private void AskDecision(PendingDecision decision, bool announce = true)
    {
        decision.ResumePhase = State.Phase;
        decision.Suspended = State.Phase == Phase.Decision ? State.PendingDecision : null;
        State.PendingDecision = decision;
        State.Phase = Phase.Decision;
        if (ResolveOnlyChoice(decision, DecisionChoices(decision.Decider))) return;
        if (announce) State.LastCombat = $"{State.Players[decision.Decider].Name} · {decision.Title()} · {decision.Instruction}";
    }

    private bool ResolveOnlyChoice(PendingDecision decision, IReadOnlyList<DecisionChoice> choices)
    {
        // Version 1 duels recorded selections as separate moves, even with a single eligible choice.
        if (choices is not [{ RequiresAcknowledgement: false } choice]) return false;
        ApplyDecision(decision, choice);
        return true;
    }

    /// <summary>The live choices of the pending decision, only for the player who decides.</summary>
    public IReadOnlyList<DecisionChoice> DecisionChoices(int actor)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision || decision.Decider != actor) return [];
        if (decision.TargetResponse != null)
            return [new() { Label = "Dejar resolver el efecto", Button = "Pasar", Public = "deja resolver el efecto rival.", RequiresAcknowledgement = true }];
        if (CardEffects.For(decision.Source) is { } effect)
            return effect.Choices(EffectFor(actor, decision.Source), decision);
        if (decision.Source == CardCatalog.CopyHelmet.Id) return CopyHelmetChoices(decision);
        if (decision.Source == CardCatalog.EquipmentRaid.Id) return EquipmentRaidChoices(decision);
        if (decision.Source == CardCatalog.BlackHole.Id) return BlackHoleChoices(decision);
        if (decision.Source == CardCatalog.IronwingFire.Id || decision.Source == RelikarioCatalog.Ironwing.Id ||
            decision.Source == RelikarioCatalog.IronwingLv2.Id) return IronwingChoices(decision);
        return OsamuDecisionChoices(decision);
    }

    public CommandResult ResolveDecision(int actor, int choice)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision || decision.Decider != actor)
            return CommandResult.Fail("No te corresponde decidir.");
        var choices = DecisionChoices(actor);
        if (choice < 0 || choice >= choices.Count) return CommandResult.Fail("Esa opción ya no está disponible.");
        ApplyDecision(decision, choices[choice]);
        return CommandResult.Ok();
    }

    private void ApplyDecision(PendingDecision decision, DecisionChoice choice, bool allowTargetResponse = true)
    {
        if (decision.TargetResponse != null) { ResumeTargetedEffect(decision); return; }
        if (allowTargetResponse && (choice.TargetPlayer ?? choice.Field?.Owner) is int target &&
            TryOfferTargetResponse(decision.Decider, target, decision.Source, choice: choice)) return;
        State.PendingDecision = decision.Suspended;
        State.Phase = decision.ResumePhase;
        if (CardEffects.For(decision.Source) is { } effect)
            effect.Choose(EffectFor(decision.Decider, decision.Source), decision, choice);
        else if (decision.Source == CardCatalog.CopyHelmet.Id) AddCopyHelmetToRam(decision, choice);
        else if (decision.Source == CardCatalog.EquipmentRaid.Id) ResolveEquipmentRaidChoice(decision, choice);
        else if (decision.Source == CardCatalog.BlackHole.Id) ResolveBlackHoleChoice(decision, choice);
        else if (decision.Source == CardCatalog.IronwingFire.Id || decision.Source == RelikarioCatalog.Ironwing.Id ||
            decision.Source == RelikarioCatalog.IronwingLv2.Id) ResolveIronwingChoice(decision, choice);
        else ResolveOsamuDecision(decision, choice);
        CompleteActivationIfFinished();
    }

    private bool TryResolveWithEffect(PlayerState p, CardDefinition card)
    {
        if (CardEffects.For(card.Id) is not CardEffect effect) return false;
        effect.Resolve(EffectFor(Array.IndexOf(State.Players, p), card.Id));
        return true;
    }

    /// <summary>
    /// Recomputes the remaining choices after an interruption. With no valid target left the effect fails (A.2).
    /// </summary>
    private void RecheckDecision()
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision) return;
        var choices = DecisionChoices(decision.Decider);
        if (ResolveOnlyChoice(decision, choices) || choices.Count > 0) return;
        Log($"{decision.Title()} se resuelve sin opciones disponibles después de las respuestas.");
        State.PendingDecision = decision.Suspended; State.Phase = decision.ResumePhase;
        CompleteActivationIfFinished();
    }

    private void ContinueCardEffect(EngineContinuation next)
    {
        var effect = CardEffects.For(next.Text) ?? throw new InvalidOperationException("Paso de una carta sin efecto registrado.");
        effect.Continue(EffectFor(next.Actor, next.Text), next.Step ?? "", next);
    }

    internal static string FieldPlace(GameState s, FieldTarget t, int viewer)
    {
        var own = t.Owner == viewer;
        return t.Zone switch
        {
            FieldZone.Hand => own ? "Tu mano al azar" : "Mano rival al azar",
            FieldZone.Equipment => own ? "Tu equipo" : "Equipo rival",
            FieldZone.LightEquipment => $"{(own ? "Tu" : "Rival")} {CardCatalog.Get(t.CardId).Name} · equipo ligero {t.Index + 1}",
            FieldZone.EquipmentCover => $"{(own ? "Tu" : "Rival")} Tótem de Oro · copia {t.Index + 1}",
            FieldZone.Area => own ? "Tu Área" : "Área rival",
            FieldZone.Zahira => $"{(own ? "Tu" : "Rival")} Zahira Flair{(ZahiraSlot(t.Index) > 0 ? " · segunda copia" : "")}",
            _ => own ? "Tu Bonus" : "Bonus rival"
        };
    }

    internal static string FieldCardPlace(GameState s, FieldTarget t)
    {
        var zone = t.Zone switch
        {
            FieldZone.Equipment or FieldZone.EquipmentCover => "equipo",
            FieldZone.LightEquipment => "equipo ligero",
            FieldZone.Skill => "habilidad",
            FieldZone.GalacticShield => $"sobre {s.Players[t.Owner].Relikario.DisplayName}",
            FieldZone.Attached => $"{(DeckRules.IsAttack(CardCatalog.Get(t.CardId)) ? "debajo de" : "sobre")} {s.Players[t.Owner].Relikario.DisplayName}",
            FieldZone.Bonus => "Bonus", FieldZone.Area => "Área", _ => "canje"
        };
        return $"{zone}" + (t.Index >= 0 ? $" {t.Index + 1}" : "");
    }
}
