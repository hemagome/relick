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
    /// <summary>Effect-specific payload: a deck position, whether to pay.</summary>
    public int Value { get; init; }
    public bool Primary { get; init; }
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
        new ICardEffect[] { new BlueEggEffect(), new TimeBombEffect(), new KonamyCodeEffect() }.ToDictionary(e => e.CardId);

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
        public void Ask(string step, DecisionKind kind, string instruction, bool allowsCombos = false, bool announce = true)
        {
            State.PendingDecision = new PendingDecision { Source = Card.Id, Step = step, Decider = Actor, Kind = kind,
                Instruction = instruction, AllowsCombos = allowsCombos, ResumePhase = State.Phase };
            State.Phase = Phase.Decision;
            if (announce) State.LastCombat = $"{Player.Name} · {Card.Name} · {instruction}";
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

    /// <summary>The live choices of the pending decision, only for the player who decides.</summary>
    public IReadOnlyList<DecisionChoice> DecisionChoices(int actor)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision || decision.Decider != actor ||
            CardEffects.For(decision.Source) is not { } effect) return [];
        return effect.Choices(EffectFor(actor, decision.Source), decision);
    }

    public CommandResult ResolveDecision(int actor, int choice)
    {
        if (State.Phase != Phase.Decision || State.PendingDecision is not { } decision || decision.Decider != actor)
            return CommandResult.Fail("No te corresponde decidir.");
        var effect = CardEffects.For(decision.Source) ?? throw new InvalidOperationException("Decisión de una carta sin efecto registrado.");
        var context = EffectFor(actor, decision.Source);
        var choices = effect.Choices(context, decision);
        if (choice < 0 || choice >= choices.Count) return CommandResult.Fail("Esa opción ya no está disponible.");
        State.PendingDecision = null;
        State.Phase = decision.ResumePhase;
        effect.Choose(context, decision, choices[choice]);
        CompleteActivationIfFinished();
        return CommandResult.Ok();
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
            FieldZone.EquipmentCover => $"{(own ? "Tu" : "Rival")} Tótem de Oro · copia {t.Index + 1}",
            FieldZone.Zahira => $"{(own ? "Tu" : "Rival")} Zahira Flair{(ZahiraSlot(t.Index) > 0 ? " · segunda copia" : "")}",
            _ => own ? "Tu Bonus" : "Bonus rival"
        };
    }

    internal static string FieldCardPlace(GameState s, FieldTarget t)
    {
        var zone = t.Zone switch
        {
            FieldZone.Equipment or FieldZone.EquipmentCover => "equipo", FieldZone.Attached => $"sobre {s.Players[t.Owner].Relikario.DisplayName}",
            FieldZone.Bonus => "Bonus", _ => "canje"
        };
        return $"{zone}" + (t.Index >= 0 ? $" {t.Index + 1}" : "");
    }
}
