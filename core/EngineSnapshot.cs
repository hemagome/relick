using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relick.Core;

public sealed class EngineFrameSnapshot
{
    public EngineActivation Frame { get; set; } = new();
    public int Parent { get; set; } = -1;
}
public sealed class EngineSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public GameState State { get; set; } = new();
    public DieEffect? PendingDie { get; set; }
    public List<EngineFrameSnapshot> Frames { get; set; } = [];
    public int Active { get; set; } = -1;
    public int[] Chain { get; set; } = [];
    public Phase ErrorResume { get; set; }
    public string DieCaption { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DuelHistory? PublicHistory { get; set; }
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
        var snapshot = new EngineSnapshot { State = State, PendingDie = _pendingDieEffect, ErrorResume = _errorResumePhase,
            DieCaption = CurrentDieCaption, PublicHistory = PublicHistory, Active = Register(_activeActivation), Chain = _activationChain.Select(Register).ToArray(), Frames = frames };
        // Online snapshots never retain an ever-growing log. The private audit has its own retention.
        var log = State.Log;
        if (!keepLog) State.Log = [];
        try { return JsonSerializer.Serialize(snapshot, EngineJson.Default.EngineSnapshot); }
        finally { State.Log = log; }
    }

    public static GameEngine FromSnapshot(string json, IGameRandom random)
    {
        var saved = JsonSerializer.Deserialize(json, EngineJson.Default.EngineSnapshot) ?? throw new InvalidOperationException("Estado de partida vacío.");
        if (saved.FormatVersion != 1 || saved.State.Players.Length != 2) throw new InvalidOperationException("Formato de estado no compatible.");
        // Earlier saves stored Yuno Lv 2 with ten maximum lives. Restore its corrected cap.
        foreach (var player in saved.State.Players)
            if (player.Relikario.Id == RelikarioCatalog.YunoLv2.Id)
            {
                player.Relikario = RelikarioCatalog.YunoLv2;
                player.Health = Math.Min(player.Health, player.MaxHealth);
            }
        EngineActivation? Frame(int index) => index == -1 ? null : index >= 0 && index < saved.Frames.Count ? saved.Frames[index].Frame :
            throw new InvalidOperationException("Activación persistida inválida.");
        for (var i = 0; i < saved.Frames.Count; i++) saved.Frames[i].Frame.Parent = Frame(saved.Frames[i].Parent);
        var game = new GameEngine(saved.State, random) { _pendingDieEffect = saved.PendingDie, _errorResumePhase = saved.ErrorResume,
            _activeActivation = Frame(saved.Active), CurrentDieCaption = saved.DieCaption, PublicHistory = saved.PublicHistory };
        game._activationChain.AddRange(saved.Chain.Select(i => Frame(i) ?? throw new InvalidOperationException("Cadena persistida inválida.")));
        return game;
    }
}
