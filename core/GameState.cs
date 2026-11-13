using System.Text.Json.Serialization;

namespace Relick.Core;

public enum Phase { Preparation, Combat, Actions, Discard, Finished, RelikiaChoice, DefenseChoice, MagnetChoice, AttackRerollChoice, DieRerollChoice, CodeResponse, PointsChoice, DestroyChoice, ErrorResponse, RecycleChoice, DeckSearchChoice, MindResetChoice, SniperChoice, Final, DestructionResponse, MemoryRecoveryChoice, TurnBuffDiscard, TurnBuffResolving, EffectDamageChoice, ReloadResolving, Draw, TotemChoice, GoldenTotemChoice, GoldenTotemResolving, TrapCraftChoice, ZahiraTarget, BlueEggDrawChoice }

public sealed class PlayerState
{
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AutoPassCombos { get; internal set; }
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
    public int EquipmentCounters { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentStackedCopies { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? EquipmentCovers { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SuperMagnetCopies { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ZahiraAvailable { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SuperAttackAvailable { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FieldTarget? ZahiraHost { get; internal set; }
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
        var remaining = _revealedHandSlots.Where(slot => slot != index).Select(slot => slot > index ? slot - 1 : slot).ToArray();
        _revealedHandSlots.Clear();
        _revealedHandSlots.UnionWith(remaining);
        var foreign = _foreignHandSlots.Contains(index);
        var borrowed = _foreignHandSlots.Where(slot => slot != index).Select(slot => slot > index ? slot - 1 : slot).ToArray();
        _foreignHandSlots.Clear();
        _foreignHandSlots.UnionWith(borrowed);
        return foreign;
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
    // Face-down cards at the bottom of memory; ineligible for activation and recycling.
    [JsonInclude]
    public List<string> CorruptedMemory { get; internal set; } = new();
    [JsonInclude]
    public string? Equipment { get; internal set; }
    [JsonInclude]
    public List<string> AttachedCards { get; internal set; } = new();
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
}

public sealed class GameState
{
    [JsonInclude]
    public PlayerState[] Players { get; internal set; } =
        { new() { Name = "Jugador 1" }, new() { Name = "Jugador 2" } };
    [JsonInclude]
    public int CurrentPlayer { get; internal set; }
    [JsonInclude]
    public int Turn { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraDrawStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraCombatStages { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ExtraActionStages { get; internal set; }
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
    [JsonInclude]
    public List<ActivationInfo> ActivationChain { get; internal set; } = new();
    [JsonInclude]
    public int? DestroyActor { get; internal set; }
    [JsonInclude]
    public PendingPoints? PendingPoints { get; internal set; }
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
    public PendingMindReset? PendingMindReset { get; internal set; }
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
    public PendingTotemDamage? PendingTotemDamage { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PendingGoldenTotem? PendingGoldenTotem { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? PendingTrapCrafts { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrapCraft>? TrapCraftQueue { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ZahiraPlacement? PendingZahiraPlacement { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ZahiraRolledThisFinal { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Phase? TrapCraftResumePhase { get; internal set; }
    public int DecisionPlayer => Phase == Phase.TrapCraftChoice ? TrapCraftQueue![0].Actor : Phase == Phase.ZahiraTarget ? PendingZahiraPlacement!.Actor : Phase == Phase.TotemChoice ? PendingTotemDamage!.Target : Phase == Phase.EffectDamageChoice ? 1 - PendingEffectDamage!.Actor : Phase is Phase.TurnBuffDiscard or Phase.TurnBuffResolving ? PendingTurnBuff!.Actor : Phase == Phase.DestructionResponse ? PendingDestruction!.Target.Owner : Phase == Phase.SniperChoice ? PendingSniper!.Actor : Phase == Phase.MindResetChoice ? PendingMindReset!.Actor : Phase == Phase.ErrorResponse ? ErrorResponder : Phase == Phase.CodeResponse ? 1 - PendingCode!.Actor : Phase is Phase.DieRerollChoice or Phase.RecycleChoice ? PendingDieRoll!.Player : Phase == Phase.DefenseChoice ? 1 - CurrentPlayer
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

public sealed record PendingAttackRoll(int Attacker, string SkillId, int Die, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, bool YunoStopped = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SkillSlot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NormalDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CriticalDamage = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool EffectsSuppressed = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool SuperAttack = false);

public enum DiePurpose { MagnetDamage, Recycling, Relikia, ExtraLife, PointsChange, Dodge, PcStore, SkillLearning, FieldCombat, QuickLoad, Zahira }
public sealed record PendingDieRoll(int Player, DiePurpose Purpose, int Die, Phase ResumePhase, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, bool YunoStopped = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int LearningRerolls = 0);

public sealed record ActivationInfo(int Actor, string CardId, int ActionsPaid, int PrPaid);

public sealed record PendingPoints(int Actor, int Die, int Amount);

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

