using Relick.Core;
using System.Text.Json;

static class DuelHistoryTests
{
    public static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("Historial público: activación, resultado y robos sin identidades privadas; rechazo atómico", PublicAndRejected),
        ("Historial público: Error y contraerror se explican en orden inverso y recuperan la cadena", CounterChain),
        ("Historial público: anulación devuelve costes y no inventa robos", Canceled),
        ("Historial público: límite, identificadores únicos y recuperación del checkpoint sin duplicados", Recovery),
        ("Historial público: fondo de mazo, búsqueda, corrupción y Magnet ocultan las identidades privadas", HiddenMoves),
        ("Historial público: la preferencia COMBO permanece privada y solo registra una resolución efectiva", Preference),
        ("Historial público: una activación, sus respuestas y su cierre comparten resolución; los trámites se marcan", Grouping),
        ("Historial público: el daño y el cambio de vidas se leen en una línea; las entradas antiguas conservan su texto", ReadingAndLegacy)
    };
    private static GameEngine Fixture(List<string>? first = null, List<string>? second = null, params int[] rolls)
    {
        var rng = new DuelRandom(); rng.Replay(rolls.ToList());
        var g = GameEngine.FromOpening(new DuelOpening { Names = ["Ana", "Beto"], StartingSeat = 0,
            Hands = [first ?? [CardCatalog.Senshuni.Id], second ?? [CardCatalog.Agility.Id]],
            Decks = [Enumerable.Repeat(CardCatalog.DiamondHelmet.Id, 100).ToList(), Enumerable.Repeat(CardCatalog.LoadError.Id, 100).ToList()], Bonuses = [null, null] }, rng);
        g.State.Phase = Phase.Actions; g.State.Turn = 2;
        foreach (var p in g.State.Players) p.Dexterity = 0;
        return g;
    }
    private static void Check(bool b) { if (!b) throw new Exception("Historial público incorrecto."); }
    private static void Play(GameEngine g, int actor, DuelMoveKind kind, int index = -1, int value = 0, bool flag = false, int target = 0)
        => Check(new DuelMove { Kind = kind, Index = index, Value = value, Flag = flag, Target = target }.Apply(g, actor).Success);
    private static List<string> Lines(GameEngine g, DuelHistoryEntry e) => DuelHistoryText.Lines(e, seat => g.State.Players[seat].Name);
    private static string Public(GameEngine g) => JsonSerializer.Serialize(new { History = DuelSnapshot.For(g, -1, 0).History, Resolution = DuelSnapshot.For(g, -1, 0).Resolution });
    private static void PublicAndRejected()
    {
        var g = Fixture(); var before = g.SaveSnapshot();
        Check(!new DuelMove { Kind = DuelMoveKind.PlayCode, Index = 99 }.Apply(g, 0).Success && g.SaveSnapshot() == before);
        Play(g, 0, DuelMoveKind.PlayCode, 0);
        var view = DuelSnapshot.For(g, -1, 1); var entry = view.History!.Single();
        Check(entry.CardId == CardCatalog.Senshuni.Id && entry.Status == DuelResolutionStatus.Resolved && Lines(g, entry).Contains("Ana roba 2 carta(s)."));
        var json = Public(g);
        foreach (var secret in new[] { CardCatalog.Agility, CardCatalog.DiamondHelmet, CardCatalog.LoadError })
            Check(!json.Contains(secret.Id) && !json.Contains(secret.Name));
        Check(!json.Contains("RandomValues") && !json.Contains("MemoryIndex") && !json.Contains("OwnHand"));
        Check(JsonSerializer.Deserialize(JsonSerializer.Serialize(view, DuelJson.Default.DuelSnapshot), DuelJson.Default.DuelSnapshot)!.History!.Single().Id == entry.Id);
    }
    private static void CounterChain()
    {
        var g = Fixture([CardCatalog.Senshuni.Id, CardCatalog.LoadError.Id], [CardCatalog.LoadError.Id, CardCatalog.LoadError.Id]);
        Play(g, 0, DuelMoveKind.PlayCode, 0);
        Check(g.State.Phase == Phase.ErrorResponse && DuelSnapshot.For(g, -1, 1).Resolution!.CardId == CardCatalog.Senshuni.Id);
        Play(g, 1, DuelMoveKind.PlayCode, 0); Play(g, 0, DuelMoveKind.PlayCode, 0);
        var view = DuelSnapshot.For(g, -1, 3);
        Check(view.Resolution!.Responses.Select(r => r.Actor).SequenceEqual([1, 0]));
        g = GameEngine.FromSnapshot(g.SaveSnapshot(false), new SeededRandom(71));
        Check(Public(g) == JsonSerializer.Serialize(new { History = view.History, Resolution = view.Resolution }));
        Play(g, 1, DuelMoveKind.PassError);
        var last = g.PublicHistory!.Entries.Last();
        Check(last.Status == DuelResolutionStatus.Resolved && last.CardId == CardCatalog.Senshuni.Id && last.SourceActor == 0);
        Check(Lines(g, last).First() == "Error de Carga de Ana anula Error de Carga de Beto." && Lines(g, last).Contains("Ana roba 2 carta(s)."));
        Check(g.PublicHistory.Entries.Count == 4 && g.State.Players[0].Hand.Count == 2);
    }
    private static void Canceled()
    {
        var g = Fixture([CardCatalog.Senshuni.Id], [CardCatalog.LoadError.Id]);
        Play(g, 0, DuelMoveKind.PlayCode, 0); Play(g, 1, DuelMoveKind.PlayCode, 0);
        var last = g.PublicHistory!.Entries.Last();
        Check(last.Status == DuelResolutionStatus.Canceled && last.CardId == CardCatalog.Senshuni.Id && last.SourceActor == 0);
        Check(Lines(g, last).First().Contains("anula Senshuni") && Lines(g, last).All(s => !s.Contains("roba")) && g.State.Players[0].ActionsLeft == 2);
    }
    private static void Recovery()
    {
        var g = Fixture([], []);
        for (var i = 0; i < 70; i++)
        {
            var actor = g.State.DecisionPlayer;
            var option = DuelOptions.For(g, actor).First(o => o.Move.Kind != DuelMoveKind.Concede);
            Check(option.Move.Apply(g, actor).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(false), new SeededRandom(4));
        }
        var history = g.PublicHistory!;
        Check(history.Entries.Count == DuelHistory.Capacity && history.Sequence == 70 && history.Entries.Select(e => e.Id).Distinct().Count() == DuelHistory.Capacity);
        var json = Public(g); var saved = g.SaveSnapshot(false);
        for (var i = 0; i < 4; i++) g = GameEngine.FromSnapshot(g.SaveSnapshot(false), new SeededRandom(4));
        Check(Public(g) == json && saved == g.SaveSnapshot(false));
    }
    private static void HiddenMoves()
    {
        var g = Fixture([CardCatalog.Agility.Id], []); g.State.Active.Relikia = 1000;
        Play(g, 0, DuelMoveKind.Relikia, value: (int)RelikiaEffect.DrawAndPlace);
        Play(g, 0, DuelMoveKind.Placement, 0, (int)CardDestination.DeckBottom);
        Check(!Public(g).Contains(CardCatalog.Agility.Id) && !Public(g).Contains(CardCatalog.Agility.Name) && !Public(g).Contains(CardCatalog.DiamondHelmet.Name));
        g = Fixture(); g.State.Phase = Phase.RelikiaChoice; g.State.PendingRelikia = RelikiaChoice.CorruptCode; g.State.CorruptionsRemaining = 1;
        g.State.Players[1].Memory.Add(CardCatalog.Agility.Id);
        Play(g, 0, DuelMoveKind.Corrupt, 0, target: 1);
        Check(!Public(g).Contains(CardCatalog.Agility.Id) && !Public(g).Contains(CardCatalog.Agility.Name));
        g = Fixture([CardCatalog.KonamyCode.Id], []); Play(g, 0, DuelMoveKind.PlayCode, 0);
        Play(g, 0, DuelMoveKind.Choose, 0);
        Check(!Public(g).Contains(CardCatalog.DiamondHelmet.Id) && !Public(g).Contains(CardCatalog.DiamondHelmet.Name));
        g = Fixture([CardCatalog.Attack.Id], [CardCatalog.Agility.Id], 5);
        g.State.Phase = Phase.Combat; g.State.Active.Skills.Add(CardCatalog.Magnet.Id);
        Play(g, 0, DuelMoveKind.Attack, 0, 0);
        if (g.State.Phase == Phase.AttackRerollChoice) Play(g, 0, DuelMoveKind.AttackDie);
        Check(g.State.Phase == Phase.MagnetChoice); Play(g, 0, DuelMoveKind.Magnet, 0);
        Check(!Public(g).Contains(CardCatalog.Agility.Id) && !Public(g).Contains(CardCatalog.Agility.Name));
    }
    private static void Preference()
    {
        var g = Fixture([CardCatalog.Senshuni.Id], [CardCatalog.LoadError.Id]);
        Play(g, 0, DuelMoveKind.PlayCode, 0); var history = Public(g);
        Check(g.SetComboResponses(0, false).Success && Public(g) == history);
        Check(g.SetComboResponses(1, false, out var resumed).Success && resumed);
        Check(g.PublicHistory!.Entries.Count == 2 && !Public(g).Contains("AutoPassCombos") && !Public(g).Contains("preferencia"));
        var saved = Public(g); Check(g.SetComboResponses(1, true).Success && Public(g) == saved);
    }
    private static void Grouping()
    {
        var g = Fixture([CardCatalog.Senshuni.Id, CardCatalog.LoadError.Id], [CardCatalog.LoadError.Id, CardCatalog.LoadError.Id]);
        Play(g, 0, DuelMoveKind.PlayCode, 0); Play(g, 1, DuelMoveKind.PlayCode, 0); Play(g, 0, DuelMoveKind.PlayCode, 0);
        Check(g.PublicHistory!.OpenResolution == g.PublicHistory.Entries[0].Id);
        g = GameEngine.FromSnapshot(g.SaveSnapshot(false), new SeededRandom(71));
        Play(g, 1, DuelMoveKind.PassError);
        var entries = g.PublicHistory!.Entries;
        Check(entries.All(e => e.Group == entries[0].Id && e.Stage == Phase.Actions) && g.PublicHistory.OpenResolution == 0);
        Check(entries.Select(e => e.Routine).SequenceEqual([false, false, false, true]));
        Play(g, 0, DuelMoveKind.Advance);
        var advance = g.PublicHistory.Entries.Last();
        Check(advance.Routine && advance.Group == advance.Id && advance.Stage == Phase.Actions);
        var json = JsonSerializer.Serialize(DuelSnapshot.For(g, 0, 9), DuelJson.Default.DuelSnapshot);
        Check(!json.Contains("\"Details\"") && JsonSerializer.Deserialize(json, DuelJson.Default.DuelSnapshot)!.History!.Last().Group == advance.Id);
    }
    private static void ReadingAndLegacy()
    {
        string Name(int seat) => seat == 0 ? "Ana" : "Beto";
        var hit = new DuelHistoryEntry { Id = 3, Events = [new() { Kind = DuelHistoryEventKind.Die, Value = 5 },
            new() { Kind = DuelHistoryEventKind.Damage, Player = 1, Value = 2 }, new() { Kind = DuelHistoryEventKind.Health, Player = 1, From = 5, Value = 3 },
            new() { Kind = DuelHistoryEventKind.Pr, Player = 0, Value = 200 }] };
        Check(DuelHistoryText.Lines(hit, Name).SequenceEqual(["D6: 5.", "Beto recibe 2 de daño (vidas 5 → 3).", "Ana: +200 PR."]));
        var legacy = JsonSerializer.Deserialize<DuelHistoryEntry>("{\"Id\":7,\"Text\":\"Ana activa Senshuni.\",\"Details\":[\"Ana roba 2 carta(s).\"]}")!;
        Check(legacy.Group == 7 && legacy.Stage == Phase.Preparation && DuelHistoryText.Lines(legacy, Name).Single() == "Ana roba 2 carta(s).");
    }
}
