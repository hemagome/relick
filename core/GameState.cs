namespace Relick.Core;

public enum Phase { Preparation, Combat, Actions, Discard, Finished, RelikiaChoice, DefenseChoice, MagnetChoice, AttackRerollChoice, DieRerollChoice, CodeResponse, PointsChoice, DestroyChoice, ErrorResponse }

public sealed class PlayerState
{
    public PlayerState(RelikarioDefinition? relikario = null)
    {
        Relikario = relikario ?? RelikarioCatalog.Yuno;
        Health = Relikario.MaxHealth;
    }
    public RelikarioDefinition Relikario { get; internal set; }
    public int Level => Relikario.Level;
    public bool HasNaturalSix { get; internal set; }
    public string Name { get; init; } = "";
    public int Health { get; internal set; }
    public int MaxHealth => Relikario.MaxHealth;
    public bool AttackRerollUsedThisTurn { get; internal set; }
    public int ActionsLeft { get; internal set; } = 2;
    public int Relikia { get; internal set; }
    public bool HasReachedZero { get; internal set; }
    public bool Recycled { get; internal set; }
    public List<string> Deck { get; } = new();
    public List<string> Hand { get; } = new();
    private readonly HashSet<int> _revealedHandSlots = new();
    public IReadOnlyCollection<int> RevealedHandSlots => _revealedHandSlots.Order().ToArray();
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
    public List<string> Memory { get; } = new();
    // Face-down cards at the bottom of memory; ineligible for activation and recycling.
    public List<string> CorruptedMemory { get; } = new();
    public string? Equipment { get; internal set; }
    public List<string> AttachedCards { get; } = new();
    public List<string> Skills { get; } = new();
    // Counters belong to a learned copy (slot), not to the shared card definition.
    public Dictionary<int, int> SkillCounters { get; } = new();
    public int CountersOnSkill(int slot) => SkillCounters.GetValueOrDefault(slot);
    public List<string> Exchange { get; } = new();
    public string? Bonus { get; internal set; }
}

public sealed class GameState
{
    public PlayerState[] Players { get; } =
        { new() { Name = "Tú" }, new() { Name = "Rival de práctica" } };
    public int CurrentPlayer { get; internal set; }
    public int Turn { get; internal set; }
    public Phase Phase { get; internal set; } = Phase.Preparation;
    public int ActionsLeft { get => Active.ActionsLeft; internal set => Active.ActionsLeft = value; }
    public bool LearnedThisTurn { get; internal set; }
    public bool AttackedThisTurn { get; internal set; }
    // Only explicit card effects may request discards; hand size has no default limit.
    public int DiscardsRemaining { get; internal set; }
    public Phase DiscardResumePhase { get; internal set; } = Phase.Actions;
    public int? DiscardPlayer { get; internal set; }
    public RelikiaChoice? PendingRelikia { get; internal set; }
    public int CorruptionsRemaining { get; internal set; }
    public int ErrorResponder { get; internal set; }
    public List<ActivationInfo> ActivationChain { get; } = new();
    public int? DestroyActor { get; internal set; }
    public PendingPoints? PendingPoints { get; internal set; }
    public PendingCode? PendingCode { get; internal set; }
    public PendingDieRoll? PendingDieRoll { get; internal set; }
    public PendingAttackRoll? PendingAttackRoll { get; internal set; }
    public int LastAttackDie { get; internal set; }
    public PendingAttack? PendingAttack { get; internal set; }
    public int DecisionPlayer => Phase == Phase.ErrorResponse ? ErrorResponder : Phase == Phase.CodeResponse ? 1 - PendingCode!.Actor : Phase == Phase.DieRerollChoice ? PendingDieRoll!.Player : Phase == Phase.DefenseChoice ? 1 - CurrentPlayer
        : Phase == Phase.Discard ? DiscardPlayer ?? CurrentPlayer : CurrentPlayer;
    public int? Winner { get; internal set; } // -1 = draw, null = still playing
    public int LastDie { get; internal set; }
    public string LastCombat { get; internal set; } = "El dado espera tu primer ataque.";
    public List<string> Log { get; } = new();
    public PlayerState Active => Players[CurrentPlayer];
    public bool IsOver => Phase == Phase.Finished;
}

public sealed record PendingAttackRoll(int Attacker, string SkillId, int Die, int Rerolls = 0, bool WasRerolled = false, bool Modified = false, bool YunoStopped = false);

public enum DiePurpose { MagnetDamage, Recycling, Relikia, ExtraLife, PointsChange }
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

