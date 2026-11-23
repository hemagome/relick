using System.Text.Json.Serialization;

namespace Relick.Core;

public enum FutureDieState { Ready, Prepared, Used }

public enum Phase { Preparation, Combat, Actions, Discard, Finished, RelikiaChoice, DefenseChoice, MagnetChoice, AttackRerollChoice, DieRerollChoice, CodeResponse, ErrorResponse, RecycleChoice, SniperChoice, Final, DestructionResponse, TurnBuffDiscard, TurnBuffResolving, EffectDamageChoice, ReloadResolving, Draw, GoldenTotemResolving, TrapCraftChoice, Decision }

public static class PhaseExtensions
{
    /// <summary>The four stages of a turn (Robo, Combate, Acciones, Final), with no pending resolution or choice.</summary>
    public static bool IsTurnStage(this Phase phase) => phase is Phase.Draw or Phase.Combat or Phase.Actions or Phase.Final;

    /// <summary>A turn stage other than Final: the ones a choice resumes into and where hand cards show their cost.</summary>
    public static bool IsStageBeforeFinal(this Phase phase) => phase is Phase.Draw or Phase.Combat or Phase.Actions;
}

public sealed record LightEquipmentCopy(string CardId, bool Borrowed = false);

public sealed class PlayerState
{
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoPassCombos { get; internal set; }
    /// <summary>
    /// Cosmetic sleeve and die from the player's deck; no rule reads them. Null means the classic ones, which keeps
    /// the saved state of every duel without a style byte-for-byte as before.
    /// </summary>
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sleeve { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dice { get; internal set; }
    public PlayerState(RelikarioDefinition? relikario = null)
    {
        Relikario = relikario ?? RelikarioCatalog.Yuno;
        Health = Relikario.MaxHealth;
        Dexterity = Relikario.StartingDexterity;
    }
    [JsonInclude]
    public RelikarioDefinition Relikario { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentDie { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FutureDieState FutureDie { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentCounters { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentStackedCopies { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? EquipmentCovers { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LightEquipmentCopy>? LightEquipment { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Area { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AreaIsBorrowed { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AreaSideA { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AreaNeedsBonus { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool InitialAreaWaitingInBonus { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SuperMagnetCopies { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ZahiraAvailable { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SuperAttackAvailable { get; internal set; }
    // Copies beyond the first. Kept apart from the flags above so earlier snapshots still load.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ZahiraSpares { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SuperAttackSpares { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? ZahiraHost { get; internal set; }
    // A Trap deck holds up to two copies, so one player can have two on the field at once.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? ZahiraHost2 { get; internal set; }
    public const int ZahiraSlots = 2;
    public FieldTarget? Zahira(int slot) => slot == 0 ? ZahiraHost : ZahiraHost2;
    internal void SetZahira(int slot, FieldTarget? host) { if (slot == 0) ZahiraHost = host; else ZahiraHost2 = host; }
    // Consumed at the owner's next turn start, even if Reverse replaces the destruction.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EquipmentExpiresAtTurnStart { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int StarEffects { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool BonusEffectUsed { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? ExchangeEffectsUsed { get; internal set; }
    // Zero means an established card from a save without placement history.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int BonusPlacedTurn { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? ExchangePlacementTurns { get; internal set; }
    public int Level => Relikario.Level;
    [JsonInclude]
    public bool HasNaturalSix { get; internal set; }
    public string Name { get; init; } = "";
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InitialSkillId { get; internal set; }
    [JsonInclude]
    public int Health { get; internal set; }
    public int MaxHealth => Relikario.MaxHealth;
    [JsonInclude]
    public bool AttackRerollUsedThisTurn { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RelikarioSearchUsedThisTurn { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HashSet<int>? IronwingFireUsed { get; internal set; }
    [JsonInclude]
    public int ActionsLeft { get; internal set; } = 2;
    [JsonInclude]
    public int Relikia { get; internal set; }
    // Zero must also be serialized: an omitted spent counter would be initialized again on restore.
    [JsonInclude]
    public int Dexterity { get; internal set; }
    [JsonInclude]
    public bool HasReachedZero { get; internal set; }
    [JsonInclude]
    public bool Recycled { get; internal set; }
    [JsonInclude]
    public List<string> Deck { get; internal set; } = new();
    [JsonInclude]
    public List<string> Hand { get; internal set; } = new();
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HashSet<int>? GhostDogReveals { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HashSet<int>? LockedGhostCards { get; internal set; }
    private readonly HashSet<int> _revealedHandSlots = new();
    [JsonInclude]
    public IReadOnlyCollection<int> RevealedHandSlots
    {
        get => _revealedHandSlots.Order().ToArray();
        internal set { _revealedHandSlots.Clear(); _revealedHandSlots.UnionWith(value); }
    }
    public bool IsHandCardRevealed(int index) => index >= 0 && index < Hand.Count && _revealedHandSlots.Contains(index);
    public string? RevealedHandCard(int index) => IsHandCardRevealed(index) ? Hand[index] : null;

    // Effects control disclosure. Removing a card shifts the remaining slots without
    // revealing a different copy of the same card or a later draw into the vacated slot.
    internal bool RevealHandCard(int index) => index >= 0 && index < Hand.Count && _revealedHandSlots.Add(index);
    internal void HideHandCards() => _revealedHandSlots.Clear();
    /// <summary>Removes the card and reports whether that exact copy belongs to the rival.</summary>
    internal bool RemoveHandCard(int index)
    {
        Hand.RemoveAt(index);
        GhostDogReveals = ShiftHandSlots(GhostDogReveals, index);
        LockedGhostCards = ShiftHandSlots(LockedGhostCards, index);
        RemoveCorruptionSlots(CardZone.Hand, index);
        var remaining = _revealedHandSlots.Where(slot => slot != index).Select(slot => slot > index ? slot - 1 : slot).ToArray();
        _revealedHandSlots.Clear();
        _revealedHandSlots.UnionWith(remaining);
        var foreign = _foreignHandSlots.Contains(index);
        var borrowed = _foreignHandSlots.Where(slot => slot != index).Select(slot => slot > index ? slot - 1 : slot).ToArray();
        _foreignHandSlots.Clear();
        _foreignHandSlots.UnionWith(borrowed);
        return foreign;
    }

    private static HashSet<int>? ShiftHandSlots(HashSet<int>? slots, int removed)
    {
        if (slots == null) return null;
        var shifted = slots.Where(slot => slot != removed).Select(slot => slot > removed ? slot - 1 : slot).ToHashSet();
        return shifted.Count == 0 ? null : shifted;
    }

    // Cards are plain identifiers, so ownership is tracked beside them. A hand slot listed
    // here holds a copy owned by the rival (taken with Magnet, Cambio de Mentes, Disquete…).
    // Decks and memories only ever contain their owner's cards.
    private readonly HashSet<int> _foreignHandSlots = new();
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? ForeignHandSlots
    {
        get => _foreignHandSlots.Count == 0 ? null : _foreignHandSlots.Order().ToArray();
        internal set { _foreignHandSlots.Clear(); if (value != null) _foreignHandSlots.UnionWith(value); }
    }
    public bool IsForeignHandCard(int index) => _foreignHandSlots.Contains(index);
    internal void MarkForeignHandCard(int index) => _foreignHandSlots.Add(index);
    // Osamu marks copies, including copies moved to another zone or controlled by the other player.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<CardZone, List<int>>? CorruptionMarks { get; internal set; }
    internal bool CorruptsOnMemory(CardZone zone, int index) =>
        CorruptionMarks?.GetValueOrDefault(zone)?.Contains(index) == true;
    internal void MarkForCorruption(CardZone zone, int index)
    {
        CorruptionMarks ??= [];
        if (!CorruptionMarks.TryGetValue(zone, out var slots)) CorruptionMarks[zone] = slots = [];
        if (!slots.Contains(index)) slots.Add(index);
    }
    internal void RemoveCorruptionSlots(CardZone zone, int index, int count = 1)
    {
        if (CorruptionMarks?.GetValueOrDefault(zone) is not { } slots) return;
        slots.RemoveAll(slot => slot >= index && slot < index + count);
        for (var i = 0; i < slots.Count; i++)
            if (slots[i] >= index + count) slots[i] -= count;
        if (slots.Count == 0) ClearCorruptionMarks(zone);
    }
    internal void ClearCorruptionMarks(CardZone zone)
    {
        if (CorruptionMarks == null) return;
        CorruptionMarks.Remove(zone);
        if (CorruptionMarks.Count == 0) CorruptionMarks = null;
    }
    // Rival-owned copies on this player's field (skills, equipment, covers, canje, Bonus).
    // Identical copies are interchangeable there, so a multiset is enough.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? ForeignFieldCards { get; internal set; }
    internal void MarkForeignFieldCard(string id) => (ForeignFieldCards ??= []).Add(id);
    internal bool ReleaseForeignFieldCard(string id)
    {
        if (ForeignFieldCards == null || !ForeignFieldCards.Remove(id)) return false;
        if (ForeignFieldCards.Count == 0) ForeignFieldCards = null;
        return true;
    }
    [JsonInclude]
    public List<string> Memory { get; internal set; } = new();
    // Face-down portion of the same memory. The last entry sits at the very bottom.
    // Ordinary memory effects only address the face-up portion above it.
    [JsonInclude]
    public List<string> CorruptedMemory { get; internal set; } = new();
    [JsonInclude]
    public string? Equipment { get; internal set; }
    [JsonInclude]
    public List<string> AttachedCards { get; internal set; } = new();
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GalacticShield>? GalacticShields { get; internal set; }
    [JsonInclude]
    public List<string> Skills { get; internal set; } = new();
    // Counters belong to a learned copy (slot), not to the shared card definition.
    [JsonInclude]
    public Dictionary<int, int> SkillCounters { get; internal set; } = new();
    public int CountersOnSkill(int slot) => SkillCounters.GetValueOrDefault(slot);
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, int>? SkillPositions { get; internal set; }
    public int SkillPosition(int index) => SkillPositions?.GetValueOrDefault(index, index) ?? index;
    public int NextSkillPosition() => Enumerable.Range(0, GameEngine.MaxSkills)
        .FirstOrDefault(position => !Enumerable.Range(0, Skills.Count).Any(index => SkillPosition(index) == position), -1);
    [JsonInclude]
    public List<string> Exchange { get; internal set; } = new();
    [JsonInclude]
    public string? Bonus { get; internal set; }
    // State carried by cards moved to Bonus (-1) or to one of the exchange slots.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, MovedFieldCardState>? MovedFieldCards { get; internal set; }
}

public sealed class GameState
{
    [JsonInclude]
    public PlayerState[] Players { get; internal set; } =
        { new() { Name = "Jugador 1" }, new() { Name = "Jugador 2" } };
    [JsonInclude]
    public int CurrentPlayer { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? AreaRewards { get; internal set; }
    [JsonInclude]
    public int Turn { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FieldInstanceSequence { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TomeCard>? TomeCards { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraDrawStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraCombatStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraActionStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ActionStartActor { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraFinalStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RepeatedFinalAwaitingAdvance { get; internal set; }
    [JsonInclude]
    public Phase Phase { get; internal set; } = Phase.Preparation;
    public int ActionsLeft { get => Active.ActionsLeft; internal set => Active.ActionsLeft = value; }
    [JsonInclude]
    public bool LearnedThisTurn { get; internal set; }
    [JsonInclude]
    public bool AttackedThisTurn { get; internal set; }
    // Fixed discards requested by effects; Phase.Final checks the current hand size instead.
    [JsonInclude]
    public int DiscardsRemaining { get; internal set; }
    [JsonInclude]
    public Phase DiscardResumePhase { get; internal set; } = Phase.Actions;
    [JsonInclude]
    public int? DiscardPlayer { get; internal set; }
    [JsonInclude]
    public RelikiaChoice? PendingRelikia { get; internal set; }
    [JsonInclude]
    public int CorruptionsRemaining { get; internal set; }
    [JsonInclude]
    public int ErrorResponder { get; internal set; }
    /// <summary>The Error de Carga window is open after a Code's die result (6.5, 6.6); <see cref="PendingDieRoll"/> holds the final die.</summary>
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ErrorAfterDie { get; internal set; }
    /// <summary>During <see cref="ErrorAfterDie"/> for a COMBO that rerolled a die: the rest of its effect, run if it is not canceled.</summary>
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EngineContinuation? PendingAfterError { get; internal set; }
    [JsonInclude]
    public List<ActivationInfo> ActivationChain { get; internal set; } = new();
    /// <summary>The decision a card effect is waiting for (<see cref="Phase.Decision"/>).</summary>
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingDecision? PendingDecision { get; internal set; }
    [JsonInclude]
    public PendingCode? PendingCode { get; internal set; }
    [JsonInclude]
    public PendingDieRoll? PendingDieRoll { get; internal set; }
    [JsonInclude]
    public PendingAttackRoll? PendingAttackRoll { get; internal set; }
    [JsonInclude]
    public int LastAttackDie { get; internal set; }
    [JsonInclude]
    public PendingAttack? PendingAttack { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingSniper? PendingSniper { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingDestruction? PendingDestruction { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingTurnBuff? PendingTurnBuff { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingEffectDamage? PendingEffectDamage { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingReload? PendingReload { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingGoldenTotem? PendingGoldenTotem { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrapCraft>? TrapCraftQueue { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ZahiraRolledThisFinal { get; internal set; }
    // Next Zahira Flair of the waiting player that still has to roll in this final stage.
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ZahiraFinalSlot { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Phase? TrapCraftResumePhase { get; internal set; }
    public int DecisionPlayer => Phase == Phase.Decision ? PendingDecision!.Decider : Phase == Phase.TrapCraftChoice ? TrapCraftQueue![0].Actor : Phase == Phase.EffectDamageChoice ? 1 - PendingEffectDamage!.Actor : Phase is Phase.TurnBuffDiscard or Phase.TurnBuffResolving ? PendingTurnBuff!.Actor : Phase == Phase.DestructionResponse ? PendingDestruction!.Target.Owner : Phase == Phase.SniperChoice ? PendingSniper!.Actor : Phase == Phase.ErrorResponse ? ErrorResponder : Phase == Phase.CodeResponse ? 1 - PendingCode!.Actor : Phase is Phase.DieRerollChoice or Phase.RecycleChoice ? PendingDieRoll!.Player : Phase == Phase.DefenseChoice ? 1 - CurrentPlayer
        : Phase == Phase.Discard ? DiscardPlayer ?? CurrentPlayer : CurrentPlayer;
    [JsonInclude]
    public int? Winner { get; internal set; } // -1 = draw, null = still playing
    [JsonInclude]
    public int LastDie { get; internal set; }
    [JsonInclude]
    public string LastCombat { get; internal set; } = "";
    [JsonInclude]
    public List<string> Log { get; internal set; } = new();
    public PlayerState Active => Players[CurrentPlayer];
    public bool IsOver => Phase == Phase.Finished;
}

public sealed record PendingAttackRoll(int Attacker, string SkillId, int Die, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, [property: JsonPropertyName("YunoStopped")] bool RerollStopped = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkillSlot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NormalDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CriticalDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EffectsSuppressed = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SuperAttack = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IronwingPaid = false);

public enum DiePurpose { MagnetDamage, Recycling, Relikia, ExtraLife, PointsChange, Dodge, PcStore, SkillLearning, FieldCombat, QuickLoad, Zahira, TurboSwordDamage, TurboSwordDefense, TurboOverlapDamage, BlackHole, GhostDog, DualityCassette }
public sealed record PendingDieRoll(int Player, DiePurpose Purpose, int Die, Phase ResumePhase, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, [property: JsonPropertyName("YunoStopped")] bool RerollStopped = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int LearningRerolls = 0);

public sealed record ActivationInfo(int Actor, string CardId, int ActionsPaid, int PrPaid);


public sealed record PendingCode(int Actor, string CardId, bool FromMemory, int MemoryIndex, Phase ResumePhase);

public sealed record PendingAttack(int Attacker, string SkillId, int Damage, bool Critical,
    // Older saves have already paid the reward; missing flags retain that behavior on recovery.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RewardPending = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DamageResolved = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NormalFromMiss = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EffectsSuppressed = false);

public readonly record struct CommandResult(bool Success, string Message)
{
    public static CommandResult Ok(string message = "") => new(true, message);
    public static CommandResult Fail(string message) => new(false, message);
}

public interface IGameRandom
{
    int Next(int exclusiveMax);
}

public sealed class SeededRandom(int seed) : IGameRandom
{
    private readonly Random _random = new(seed);
    public int Next(int exclusiveMax) => _random.Next(exclusiveMax);
}

