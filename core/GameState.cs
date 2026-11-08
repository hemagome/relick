using System.Text.Json.Serialization;

namespace Relick.Core;

public enum Phase { Preparation, Combat, Actions, Discard, Finished, RelikiaChoice, DefenseChoice, MagnetChoice, AttackRerollChoice, DieRerollChoice, CodeResponse, PointsChoice, DestroyChoice, ErrorResponse, RecycleChoice, DeckSearchChoice, MindResetChoice, SniperChoice }

public sealed class PlayerState
{
    public PlayerState(RelikarioDefinition? relikario = null)
    {
        Relikario = relikario ?? RelikarioCatalog.Yuno;
        Health = Relikario.MaxHealth;
    }
    [JsonInclude]
    public RelikarioDefinition Relikario { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentDie { get; internal set; }
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EquipmentCounters { get; internal set; }
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
    internal void RemoveHandCard(int index)
    {
        Hand.RemoveAt(index);
        var remaining = _revealedHandSlots.Where(slot => slot != index).Select(slot => slot > index ? slot - 1 : slot).ToArray();
        _revealedHandSlots.Clear();
        _revealedHandSlots.UnionWith(remaining);
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
    [JsonInclude]
    public Phase Phase { get; internal set; } = Phase.Preparation;
    public int ActionsLeft { get => Active.ActionsLeft; internal set => Active.ActionsLeft = value; }
    [JsonInclude]
    public bool LearnedThisTurn { get; internal set; }
    [JsonInclude]
    public bool AttackedThisTurn { get; internal set; }
    // Only explicit card effects may request discards; hand size has no default limit.
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
    public int DecisionPlayer => Phase == Phase.SniperChoice ? PendingSniper!.Actor : Phase == Phase.MindResetChoice ? PendingMindReset!.Actor : Phase == Phase.ErrorResponse ? ErrorResponder : Phase == Phase.CodeResponse ? 1 - PendingCode!.Actor : Phase is Phase.DieRerollChoice or Phase.RecycleChoice ? PendingDieRoll!.Player : Phase == Phase.DefenseChoice ? 1 - CurrentPlayer
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

public sealed record PendingAttackRoll(int Attacker, string SkillId, int Die, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, bool YunoStopped = false);

public enum DiePurpose { MagnetDamage, Recycling, Relikia, ExtraLife, PointsChange, Dodge, PcStore }
public sealed record PendingDieRoll(int Player, DiePurpose Purpose, int Die, Phase ResumePhase, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, bool YunoStopped = false);

public sealed record ActivationInfo(int Actor, string CardId, int ActionsPaid, int PrPaid);

public sealed record PendingPoints(int Actor, int Die, int Amount);

public sealed record PendingCode(int Actor, string CardId, bool FromMemory, int MemoryIndex, Phase ResumePhase);

public sealed record PendingAttack(int Attacker, string SkillId, int Damage, bool Critical);

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

