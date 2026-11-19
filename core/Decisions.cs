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
public sealed class DecisionChoice
{
    /// <summary>Full description for the decider ("Destruir Hoguera de Beto · equipo").</summary>
    public string Label { get; init; } = "";
    /// <summary>Short button text ("Destruir"); null uses the label.</summary>
    public string? Button { get; init; }
    /// <summary>What every player may read in the public history once it is chosen.</summary>
    public string Public { get; init; } = "";
    public string? CardId { get; init; }
    public FieldTarget? Field { get; init; }
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
/// card's effect continues with the answer and who answers; the card's <see cref="ICardEffect"/> computes the choices.
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
/// The rules of one card, in one place. The engine calls <see cref="Resolve"/> when the card resolves, asks it for
/// the choices of a decision it opened, hands it the answer, and resumes it after an operation that may pause
/// (a destruction someone can protect, a Bonus refill). Steps are plain names so a saved duel can resume them.
/// </summary>
internal interface ICardEffect
{
    string CardId { get; }
    /// <summary>Why the card cannot be activated now, beyond the common costs; <paramref name="excludedHand"/> is the card itself.</summary>
    string? Unavailable(GameEngine.EffectContext ctx, int excludedHand);
    void Resolve(GameEngine.EffectContext ctx);
    IReadOnlyList<DecisionChoice> Choices(GameEngine.EffectContext ctx, PendingDecision decision);
    void Choose(GameEngine.EffectContext ctx, PendingDecision decision, DecisionChoice choice);
    void Continue(GameEngine.EffectContext ctx, string step, EngineContinuation next);
}

internal static class CardEffects
{
    private static readonly IReadOnlyDictionary<string, ICardEffect> Registered =
        new ICardEffect[] { new BlueEggEffect(), new TimeBombEffect(), new KonamyCodeEffect(), new ExtraLifeEffect(),
            new PointsIncreaseEffect(), new QuickLoadEffect(), new FloppyDiskEffect(), new SenshuniEffect(), new MindSwapEffect(),
            new ReloadCartridgeEffect(), new TripleAttackEffect(), new MindResetEffect(),
            new DodgeEffect(), new GoldenTotemEffect(), new TakeEquipmentEffect() }.ToDictionary(e => e.CardId);

    public static ICardEffect? For(string? cardId) => cardId != null && Registered.TryGetValue(cardId, out var effect) ? effect : null;
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
        /// <summary>Duels recorded before the optional Huevo Azul draw replay with its original rules.</summary>
        public bool LegacyRules => _engine.LegacyBlueEgg;

        public void Log(string text) => _engine.Log(text);
        public void Announce(string text) { State.LastCombat = text; _engine.Log(text); }

        /// <summary>Opens a decision for <see cref="Actor"/>; the card's <c>Choices</c> and <c>Choose</c> handle <paramref name="step"/>.</summary>
        public void Ask(string step, DecisionKind kind, string instruction, bool allowsCombos = false, bool announce = true, int value = 0,
            List<int>? picks = null, int? showsHandOf = null)
        {
            _engine.AskDecision(new PendingDecision { Source = Card.Id, Step = step, Decider = Actor, Kind = kind,
                Instruction = instruction, AllowsCombos = allowsCombos, Value = value, Picks = picks, ShowsHandOf = showsHandOf }, announce);
        }

        /// <summary>Continues the card at <paramref name="step"/> after the operation finishes, or right away.</summary>
        public EngineContinuation Then(string step, FieldTarget? target = null) =>
            new() { Kind = ContinuationKind.CardEffectStep, Actor = Actor, Text = Card.Id, Step = step, Target = target };

        public void Continue(EngineContinuation next) => _engine.Continue(next);

        /// <summary>Destroys a card through every replacement; <paramref name="then"/> learns whether it was prevented.</summary>
        public void Destroy(FieldTarget target, EngineContinuation then)
        {
            if (!_engine.TryReplaceOpponentFieldEffect(Actor, target, then)) _engine.RequestDestruction(target, then);
        }

        public void Draw(int count, EngineContinuation? then = null) => _engine.DrawCards(Player, count, then);
        public int Heal(int amount) => _engine.Heal(Player, amount);
        /// <summary>Swaps the whole hands of both players; each copy keeps its owner and whether it was revealed.</summary>
        public void SwapHands() => _engine.SwapHands(Actor);
        /// <summary>Both players discard their hands and draw this card's <c>Draw</c>, with Mira de Sniper triggers in between.</summary>
        public void ReloadBothHands() => _engine.BeginReloadHands(Actor);
        /// <summary>Shows these Tótems de Salvación and places them with Tótem de Oro on top, through the engine's placement sequence.</summary>
        public void DeployGoldenTotem(int[] handIndices) => _engine.DeployGoldenTotem(Actor, handIndices);
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
        /// <summary>Memory cards Resetear la Mente may return: all but a memory code waiting on its responses.</summary>
        public IEnumerable<MemorySelection> ResettableMemoryCards() => _engine.MindResetMemories();
        public string? MindResetUnavailable(int excludedHand) => _engine.MindResetUnavailable(Actor, excludedHand);
        /// <summary>Returns the chosen cards to their owners' decks, shuffles them and lets the target draw; ends this activation first.</summary>
        public void ReturnMindResetCards(int target, IReadOnlyList<MemorySelection> memories, int handIndex, Phase resumePhase) =>
            _engine.ReturnMindResetCards(Actor, target, memories, handIndex, resumePhase, _engine.EndMindResetActivation);
        /// <summary>Ends Resetear la Mente without returning cards; the turn may end now if it was waiting for it.</summary>
        public void EndMindReset() { _engine.EndMindResetActivation(); _engine.CompleteEndTurnIfReady(); }
        /// <summary>Moves a memory card to this player's hand; a card from the rival's memory stays marked as foreign.</summary>
        public string TakeFromMemory(int owner, int index) => _engine.TakeFromMemory(Actor, owner, index);
        /// <summary>Once the effect commits to a result, Error de Carga can no longer cancel it.</summary>
        public void CloseErrorWindow() { if (_engine._activeActivation != null) _engine._activeActivation.CanInterrupt = false; }
        public void RefillBonus(int owner, EngineContinuation? then) => _engine.RefillBonus(State.Players[owner], then);
        public void MoveDeckCardToHand(int deckIndex) => _engine.MoveDeckCard(Player, deckIndex, false);
        public int Random(int exclusiveMax) => _engine._random.Next(exclusiveMax);
        /// <summary>Ends the activation before the effect's last move, as a search does before taking its card.</summary>
        public void EndActivation() => _engine._activeActivation = null;

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
        if (LegacyBlueEgg || choices is not [{ RequiresAcknowledgement: false } choice]) return false;
        ApplyDecision(decision, choice);
        return true;
    }

    /// <summary>The live choices of the pending decision, only for the player who decides.</summary>
    public IReadOnlyList<DecisionChoice> DecisionChoices(int actor)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision || decision.Decider != actor) return [];
        if (CardEffects.For(decision.Source) is { } effect)
            return effect.Choices(EffectFor(actor, decision.Source), decision);
        if (decision.Source == CardCatalog.EquipmentRaid.Id) return EquipmentRaidChoices(decision);
        if (decision.Source == CardCatalog.PurpleRing.Id) return PurpleRingChoices(decision);
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

    private void ApplyDecision(PendingDecision decision, DecisionChoice choice)
    {
        State.PendingDecision = decision.Suspended;
        State.Phase = decision.ResumePhase;
        if (CardEffects.For(decision.Source) is { } effect)
            effect.Choose(EffectFor(decision.Decider, decision.Source), decision, choice);
        else if (decision.Source == CardCatalog.EquipmentRaid.Id) ResolveEquipmentRaidChoice(decision, choice);
        else if (decision.Source == CardCatalog.PurpleRing.Id) ResolvePurpleRingChoice(decision, choice);
        else ResolveOsamuDecision(decision, choice);
        CompleteActivationIfFinished();
    }

    /// <summary>Answers the pending decision with the choice that matches, for commands that predate decisions.</summary>
    private CommandResult? ResolveDecisionWhere(int actor, string source, Func<DecisionChoice, bool> matches)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision?.Source != source) return null;
        var choices = DecisionChoices(actor);
        for (var i = 0; i < choices.Count; i++)
            if (matches(choices[i])) return ResolveDecision(actor, i);
        return CommandResult.Fail(choices.Count == 0 ? "No te corresponde decidir." : "Elige una opción válida.");
    }

    private bool TryResolveWithEffect(PlayerState p, CardDefinition card)
    {
        if (CardEffects.For(card.Id) is not { } effect) return false;
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
            FieldZone.Attached => $"{(DeckRules.IsAttack(CardCatalog.Get(t.CardId)) ? "debajo de" : "sobre")} {s.Players[t.Owner].Relikario.DisplayName}",
            FieldZone.Bonus => "Bonus", FieldZone.Area => "Área", _ => "canje"
        };
        return $"{zone}" + (t.Index >= 0 ? $" {t.Index + 1}" : "");
    }
}
