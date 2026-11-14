using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relick.Core;

public sealed class DuelOpening
{
    public string[] Names { get; set; } = [];
    public List<string>[] Hands { get; set; } = [];
    public List<string>[] Decks { get; set; } = [];
    public string?[] Bonuses { get; set; } = [];
    public int StartingSeat { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? SuperMagnetCopies { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeZahiraFlair { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeSuperAttack { get; set; }
    // Each seat's Trap deck. When present it replaces the three training fields above.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>[]? Traps { get; set; }
}

public enum DuelMoveKind
{
    Learn, Attack, PlayCode, Equip, Exchange, Advance, Discard, Evolve, AttackDie, EffectDie,
    Pendant, PassDefense, BoxDefense, DestinyDefense, Magnet, SkipMagnet, CodeResponse, PassError,
    Points, Destroy, Relikia, Placement, Reward, Corrupt, Concede, Recycle, SearchDeck, MindResetTarget, MindResetMemory, MindResetHand, Pc, Sniper, Dexterity, LearningDie, ProtectDestruction, RecoverMemory, TurnBuffDiscard, FieldCode, EffectDamage, Totem, GoldenTotem, ComboResponses, TrapCraft, ZahiraTarget, BlueEggDraw
}

// Indices refer exclusively to an authenticated player's server-issued options at one revision.
// They are never accepted without both the room and expected revision matching.
public sealed class DuelMove
{
    public DuelMoveKind Kind { get; set; }
    public int Index { get; set; } = -1;
    public int Target { get; set; }
    public int Value { get; set; }
    public bool Flag { get; set; }
    public int[] Discards { get; set; } = [];

    public CommandResult Apply(GameEngine game, int actor)
    {
        if (Kind == DuelMoveKind.ComboResponses) return ApplyRules(game, actor);
        using var observation = new PublicDuelObservation(game, actor, this);
        var result = ApplyRules(game, actor);
        if (result.Success) observation.Complete();
        return result;
    }

    private CommandResult ApplyRules(GameEngine game, int actor) => Kind switch
    {
        DuelMoveKind.Learn => game.Learn(actor, Index),
        DuelMoveKind.Attack => game.Attack(actor, Index, Value),
        DuelMoveKind.PlayCode => game.PlayCode(actor, Index),
        DuelMoveKind.Equip => game.Equip(actor, Index),
        DuelMoveKind.Exchange => game.Exchange(actor, Index),
        DuelMoveKind.Advance => game.Advance(actor),
        DuelMoveKind.Discard => game.Discard(actor, Index),
        DuelMoveKind.Evolve => game.Evolve(actor),
        DuelMoveKind.AttackDie => game.ResolveAttackReroll(actor, Flag),
        DuelMoveKind.EffectDie => game.ResolveDieReroll(actor, Flag),
        DuelMoveKind.Pendant => game.UsePendant(actor, (PendantEffect)Value),
        DuelMoveKind.PassDefense => game.PassDefense(actor),
        DuelMoveKind.BoxDefense => game.CancelAttackWithBox(actor),
        DuelMoveKind.DestinyDefense => game.ResolveDefense(actor, Index, Discards),
        DuelMoveKind.Magnet => game.ResolveMagnet(actor, Flag, Index),
        DuelMoveKind.SkipMagnet => game.SkipMagnet(actor),
        DuelMoveKind.CodeResponse => game.ResolveCodeResponse(actor, Flag),
        DuelMoveKind.PassError => game.PassErrorResponse(actor),
        DuelMoveKind.Points => game.ResolvePointsChange(actor, Target, Flag),
        DuelMoveKind.Destroy => game.ResolveDestruction(actor, Target, (FieldZone)Value, Index),
        DuelMoveKind.BlueEggDraw => game.ResolveBlueEggDraw(actor, Flag),
        DuelMoveKind.Relikia => game.UseRelikiaEffect(actor, (RelikiaEffect)Value, Index),
        DuelMoveKind.Placement => game.ResolveRelikiaPlacement(actor, Index, (CardDestination)Value),
        DuelMoveKind.Reward => game.ChooseRelikiaReward(actor, (RelikiaReward)Value),
        DuelMoveKind.Corrupt => game.ResolveRelikiaCorruption(actor, Target, Index),
        DuelMoveKind.Concede => game.Concede(actor),
        DuelMoveKind.Recycle => game.ResolveRecycling(actor, Discards),
        DuelMoveKind.SearchDeck => game.ResolveDeckSearch(actor, Index),
        DuelMoveKind.MindResetTarget => game.ChooseMindResetTarget(actor, Target),
        DuelMoveKind.MindResetMemory => game.ChooseMindResetMemory(actor, Target, Index),
        DuelMoveKind.MindResetHand => game.ResolveMindReset(actor, Index),
        DuelMoveKind.Pc => game.UsePc(actor),
        DuelMoveKind.Sniper => game.ResolveSniper(actor, Flag),
        DuelMoveKind.Dexterity => game.UseDexterity(actor),
        DuelMoveKind.LearningDie => game.RerollLearningDie(actor),
        DuelMoveKind.ProtectDestruction => game.ResolveDestructionProtection(actor, Index),
        DuelMoveKind.RecoverMemory => game.ResolveMemoryRecovery(actor, Target, Index),
        DuelMoveKind.TurnBuffDiscard => game.ResolveTurnBuffDiscard(actor, Index),
        DuelMoveKind.FieldCode => game.UseFieldCode(actor, (FieldZone)Value, Index),
        DuelMoveKind.EffectDamage => game.ResolveEffectDamage(actor),
        DuelMoveKind.Totem => game.ResolveTotem(actor, Flag),
        DuelMoveKind.GoldenTotem => game.ResolveGoldenTotem(actor, Discards),
        DuelMoveKind.TrapCraft => game.ResolveTrapCraft(actor, Flag, Flag ? Math.Max(0, Value) : 0),
        DuelMoveKind.ZahiraTarget => game.ResolveZahiraTarget(actor, Target, (FieldZone)Value, Index),
        DuelMoveKind.ComboResponses => game.SetComboResponses(actor, Flag),
        _ => CommandResult.Fail("Jugada desconocida.")
    };
}

public sealed class DuelOption
{
    public string Label { get; set; } = "";
    public DuelMove Move { get; set; } = new();
    public int RequiredDiscards { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CardId { get; set; }
}

public sealed class DuelEntry
{
    public int Actor { get; set; }
    public DuelMove Move { get; set; } = new();
    public List<int> RandomValues { get; set; } = [];
}

public sealed class DuelRandom : IGameRandom
{
    private List<int> _values = [];
    private int _position;
    private Func<int, int>? _live;
    public void Replay(List<int> values) { _values = values; _position = 0; _live = null; }
    public void Record(Func<int, int> live) { _values = []; _position = 0; _live = live; }
    public List<int> Finish()
    {
        if (_live == null && _position != _values.Count) throw new InvalidOperationException("El registro de azar no coincide con las reglas.");
        return _values;
    }
    public int Next(int exclusiveMax)
    {
        var value = _live != null ? _live(exclusiveMax) : _position < _values.Count ? _values[_position++] :
            throw new InvalidOperationException("Falta un resultado de azar persistido.");
        if (value < 0 || value >= exclusiveMax) throw new InvalidOperationException("Resultado de azar incompatible con las reglas.");
        if (_live != null) _values.Add(value);
        return value;
    }
}

public static class DuelReplay
{
    public static (GameEngine Game, DuelRandom Random) Restore(DuelOpening opening, IEnumerable<DuelEntry> entries, bool legacyBlueEgg = false)
    {
        var random = new DuelRandom();
        var game = GameEngine.FromOpening(opening, random, legacyBlueEgg);
        foreach (var entry in entries)
        {
            random.Replay(entry.RandomValues);
            var result = entry.Move.Apply(game, entry.Actor);
            if (!result.Success) throw new InvalidOperationException("El registro de la partida no coincide con las reglas: " + result.Message);
            random.Finish();
        }
        return (game, random);
    }
}

[JsonSerializable(typeof(DuelOpening))]
[JsonSerializable(typeof(DuelEntry))]
[JsonSerializable(typeof(DuelSnapshot))]
[JsonSerializable(typeof(DuelBoardPacket))]
[JsonSerializable(typeof(DuelPrivateData))]
[JsonSerializable(typeof(List<DuelOption>))]
public partial class DuelJson : JsonSerializerContext { }
