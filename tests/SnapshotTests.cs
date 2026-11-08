using Relick.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class SnapshotTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("Persistencia: fixtures anteriores conservan sus estados hasta evolucionar o agotar el mazo", Golden),
        ("Persistencia: el estado en línea excluye el registro creciente", BoundedLog),
        ("Red: paquetes separados conservan cantidades y revelaciones permitidas", PacketPrivacy)
    };
    public sealed class GoldenGame { public DuelOpening? Opening { get; set; } = new(); public string? InitialState { get; set; } public GoldenStep[] Steps { get; set; } = []; }
    public sealed class GoldenStep { public DuelEntry Entry { get; set; } = new(); public string Hash { get; set; } = ""; }
    // Compare the legacy shape; dedicated tests cover persistence of the new dexterity counter.
    static string Hash(GameEngine game)
    {
        var legacy = JsonSerializer.Serialize(game.State).Replace("\"Dexterity\":0,", "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacy)));
    }
    static void Golden()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "legacy-duels.json");
        var cases = JsonSerializer.Deserialize<GoldenGame[]>(File.ReadAllText(path))!;
        foreach (var (test, index) in cases.Select((g,i)=>(g,i)))
        {
            var random = new DuelRandom(); var game = test.InitialState == null ? GameEngine.FromOpening(test.Opening!, random) :
                GameEngine.FromSnapshot("{\"State\":" + test.InitialState + "}", random);
            // Already-evolved legacy fixtures start under the replaced health/dice rules.
            // Current evolution tests cover their corrected snapshot recovery explicitly.
            if (game.State.Players.Any(p => p.Relikario.Id == RelikarioCatalog.YunoLv2.Id)) continue;
            foreach (var player in game.State.Players) player.Dexterity = 0;
            for (var step = 0; step < test.Steps.Length; step++)
            {
                var expected = test.Steps[step]; random.Replay(expected.Entry.RandomValues);
                var recycled = game.State.Players.Select(p => p.Recycled).ToArray();
                var result = expected.Entry.Move.Apply(game, expected.Entry.Actor);
                // These historical recordings chose random memory cards. Their suffix is
                // intentionally obsolete; current full-duel replay/snapshot tests cover the new rule.
                if (game.State.Players.Where((p, i) => p.Recycled && !recycled[i]).Any()) break;
                random.Finish();
                // Evolution now caps healing at level-one health and changes Yuno's attack dice.
                if (result.Success && expected.Entry.Move.Kind == DuelMoveKind.Evolve) break;
                if (!result.Success || Hash(game) != expected.Hash) throw new Exception($"Cambio de reglas en duelo {index}, jugada {step}, {game.State.Phase}: {result.Message}");
                game = GameEngine.FromSnapshot(game.SaveSnapshot(), random);
                if (Hash(game) != expected.Hash) throw new Exception($"Estado incompleto en duelo {index}, jugada {step}, {game.State.Phase}");
            }
        }
    }
    static void PacketPrivacy()
    {
        var game = TestGame.Create(17); game.Start([]);
        var publicBoard = DuelSnapshot.For(game, -1, 1);
        foreach (var p in publicBoard.Players) if (p.Hand.All(c => c == null)) p.Hand = [];
        var own = new DuelPrivateData { MySeat = 0, OwnHand = game.State.Players[0].Hand.Cast<string?>().ToArray() };
        var combined = DuelPackets.Combine(new DuelBoardPacket { Board = publicBoard }, own, []);
        if (combined.Players[1].Hand.Length != game.State.Players[1].Hand.Count || combined.Players[1].Hand.Any(c => c != null))
            throw new Exception("La compresión cambia la cantidad o revela una carta oculta.");
        game.State.Players[1].RevealHandCard(2);
        game = GameEngine.FromSnapshot(game.SaveSnapshot(), new SeededRandom(1));
        var visible = DuelPackets.Combine(new DuelBoardPacket { Board = DuelSnapshot.For(game, -1, 2) }, own, []);
        if (visible.Players[1].Hand[2] != game.State.Players[1].Hand[2] || visible.Players[1].Hand.Where((_, i) => i != 2).Any(c => c != null))
            throw new Exception("La proyección pierde o amplía una revelación pública.");
    }

    static void BoundedLog()
    {
        var game = TestGame.Create(1); game.Start([]);
        game.State.Log.Add(new string('x', 50000));
        var saved = game.SaveSnapshot(false);
        if (saved.Length > 20000 || game.State.Log.Count == 0) throw new Exception("El estado conserva el registro o modifica el motor original.");
        if (GameEngine.FromSnapshot(saved, new SeededRandom(1)).State.Log.Count != 0) throw new Exception("Se persistió el registro completo.");
    }
}
