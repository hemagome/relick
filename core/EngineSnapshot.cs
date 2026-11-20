using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relick.Core;

public sealed class EngineFrameSnapshot
{
    public EngineActivation Frame { get; set; } = new();
    public int Parent { get; set; } = -1;
}
public sealed record RamBatchSnapshot(RamResolution Resolution, int[] Links);

public sealed class EngineSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public GameState State { get; set; } = new();
    public DieEffect? PendingDie { get; set; }
    public List<EngineFrameSnapshot> Frames { get; set; } = [];
    public int Active { get; set; } = -1;
    public int[] Chain { get; set; } = [];
    public Phase ErrorResume { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RamBatchSnapshot>? Ram { get; set; }
    public string DieCaption { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DieIsD10 { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DuelHistory? PublicHistory { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndReason { get; set; }
}

[JsonSourceGenerationOptions(IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(EngineSnapshot))]
public partial class EngineJson : JsonSerializerContext { }

public sealed partial class GameEngine
{
    public string SaveSnapshot(bool keepLog = true)
    {
        var frames = new List<EngineFrameSnapshot>();
        var ids = new Dictionary<EngineActivation, int>(ReferenceEqualityComparer.Instance);
        int Register(EngineActivation? frame)
        {
            if (frame == null) return -1;
            if (ids.TryGetValue(frame, out var id)) return id;
            id = frames.Count; ids.Add(frame, id);
            frames.Add(new EngineFrameSnapshot { Frame = frame });
            frames[id].Parent = Register(frame.Parent);
            return id;
        }
        // Version 1 duels keep the original cost and decisions so cached option indices stay valid.
        var snapshot = new EngineSnapshot { FormatVersion = LegacyBlueEgg ? 1 : 2, State = State, PendingDie = _pendingDieEffect, ErrorResume = _errorResumePhase,
            DieCaption = CurrentDieCaption, DieIsD10 = CurrentDieIsD10, PublicHistory = PublicHistory, EndReason = PublicEndReason, Active = Register(_activeActivation), Chain = _activationChain.Select(Register).ToArray(), Frames = frames };
        if (_ramResolutions.Count > 0)
            snapshot.Ram = _ramResolutions.Select(batch => new RamBatchSnapshot(batch, batch.Links.Select(Register).ToArray())).ToList();
        // Online snapshots never retain an ever-growing log. The private audit has its own retention.
        var log = State.Log;
        if (!keepLog) State.Log = [];
        try { return JsonSerializer.Serialize(snapshot, EngineJson.Default.EngineSnapshot); }
        finally { State.Log = log; }
    }

    public static GameEngine FromSnapshot(string json, IGameRandom random)
    {
        var saved = JsonSerializer.Deserialize(json, EngineJson.Default.EngineSnapshot) ?? throw new InvalidOperationException("Estado de partida vacío.");
        if (saved.FormatVersion is not (1 or 2) || saved.State.Players.Length != 2) throw new InvalidOperationException("Formato de estado no compatible.");
        if (saved.State.PendingTrapCrafts is { } oldCrafts)
        {
            saved.State.TrapCraftQueue ??= oldCrafts.Select(actor => new TrapCraft(actor, CardCatalog.SuperMagnet.Id)).ToList();
            saved.State.PendingTrapCrafts = null;
        }
        // The saved Relikario is only an Id plus a copy of its numbers: take the catalog's current definition, which
        // also carries what is never saved (evolution, display name). Earlier saves stored Yuno Lv 2 with ten maximum
        // lives; the catalog restores its corrected cap.
        foreach (var player in saved.State.Players)
            if (RelikarioCatalog.Find(player.Relikario.Id) is { } current)
            {
                player.Relikario = current;
                player.Health = Math.Min(player.Health, player.MaxHealth);
            }
        EngineActivation? Frame(int index) => index == -1 ? null : index >= 0 && index < saved.Frames.Count ? saved.Frames[index].Frame :
            throw new InvalidOperationException("Activación persistida inválida.");
        for (var i = 0; i < saved.Frames.Count; i++) saved.Frames[i].Frame.Parent = Frame(saved.Frames[i].Parent);
        var game = new GameEngine(saved.State, random) { LegacyBlueEgg = saved.FormatVersion == 1, _pendingDieEffect = saved.PendingDie, _errorResumePhase = saved.ErrorResume,
            _activeActivation = Frame(saved.Active), CurrentDieCaption = saved.DieCaption,
            CurrentDieIsD10 = saved.DieIsD10 || saved.State.LastDie > 6 || saved.DieCaption.StartsWith("Era Futurista · D10", StringComparison.Ordinal), PublicHistory = saved.PublicHistory, PublicEndReason = saved.EndReason };
        game._activationChain.AddRange(saved.Chain.Select(i => Frame(i) ?? throw new InvalidOperationException("Cadena persistida inválida.")));
        foreach (var batch in saved.Ram ?? [])
        {
            batch.Resolution.Links.AddRange(batch.Links.Select(i => Frame(i)!));
            game._ramResolutions.Add(batch.Resolution);
        }
        game.ConvertLegacyPhases();
        return game;
    }
}
