using Relick.Core;

static class SnapshotTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("Persistencia: el estado en línea excluye el registro creciente", BoundedLog),
        ("Red: paquetes separados conservan cantidades y revelaciones permitidas", PacketPrivacy)
    };
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
