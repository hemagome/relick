using Relick.Core;

static class FloppyDiskTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Disquete: Objeto sin COMBO, una acción y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.FloppyDisk;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Object && !c.IsCombo && c.ActionCost == 1 && c.PrCost == 0);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Disquete: recupera cualquier tipo de ambas memorias sin robar ni perder cartas", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var owner in new[] { 0, 1 })
            foreach (var id in new[] { "attack", "destiny-shuffle", "senshuni", "bonfire", "floppy-disk" })
            {
                var (g, _) = Fixture(actor); var p = g.State.Active; var source = g.State.Players[owner];
                source.Memory.AddRange([id, id]); var total = Count(g); var cues = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(actor, 0).Success && p.ActionsLeft == 1);
                Check(g.ResolveMemoryRecovery(actor, owner, 1).Success && g.State.Phase == Phase.Actions);
                Check(p.Hand.Single() == id && source.Memory[0] == id && Count(g) == total);
                Check(p.Memory.Contains(CardCatalog.FloppyDisk.Id) && cues.All(v => v.Kind != DuelVisualKind.Draw));
                var claim = cues.Single(v => v.Kind == DuelVisualKind.MemoryClaim);
                Check(claim.Player == owner && claim.Target == actor && claim.SourceIndex == 1 && claim.Value == 0);
            }
        }),
        ("Disquete: excluye solo la copia que se resuelve y permite otra copia propia o rival", () =>
        {
            foreach (var owner in new[] { 0, 1 })
            {
                var (g, _) = Fixture(); g.State.Players[owner].Memory.Add(CardCatalog.FloppyDisk.Id);
                var cues = DuelVisualEvents.Capture(g);
                Check(g.PlayCode(0, 0).Success && g.State.Active.Hand.Single() == CardCatalog.FloppyDisk.Id);
                Check(g.State.Phase == Phase.Actions && cues.Single(c => c.Kind == DuelVisualKind.MemoryClaim).Player == owner);
                Check(g.State.Active.Memory.Single() == CardCatalog.FloppyDisk.Id);
            }
        }),
        ("Disquete: no se activa sin objetivos ni recupera corruptas; rechazos no mutan", () =>
        {
            var (g, _) = Fixture(); g.State.Active.CorruptedMemory.Add(CardCatalog.Destiny.Id);
            Reject(g, () => g.PlayCode(0, 0)); g.State.Players[1].Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Pendant.Id]);
            g.State.Phase = Phase.Combat; Reject(g, () => g.PlayCode(0, 0)); g.State.Phase = Phase.Actions;
            g.State.Active.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); g.State.Active.ActionsLeft = 2;
            Check(g.PlayCode(0, 0).Success);
            Reject(g, () => g.ResolveMemoryRecovery(1, 1, 0)); Reject(g, () => g.ResolveMemoryRecovery(0, -1, 0));
            Reject(g, () => g.ResolveMemoryRecovery(0, 2, 0)); Reject(g, () => g.ResolveMemoryRecovery(0, 1, -1));
            Reject(g, () => g.ResolveMemoryRecovery(0, 1, 99)); Reject(g, () => g.Advance(0));
            Check(g.ResolveMemoryRecovery(0, 1, 0).Success && g.State.Active.CorruptedMemory.Single() == CardCatalog.Destiny.Id);
        }),
        ("Disquete: recupera selección y opciones al reconectar y mantiene privada la mano", () =>
        {
            var (g, rng) = Fixture(1); g.State.Players[0].Memory.AddRange([CardCatalog.Destiny.Id, CardCatalog.Pendant.Id]);
            Check(g.PlayCode(1, 0).Success && g.HasAvailablePlay(1) && !g.HasAvailablePlay(0));
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            var mine = DuelSnapshot.For(g, 1, 4); var other = DuelSnapshot.For(g, 0, 4);
            Check(mine.Stage == Phase.Actions && mine.Options.Where(o => o.Move.Kind == DuelMoveKind.Choose).Select(o => o.CardId)
                .SequenceEqual([CardCatalog.Destiny.Id, CardCatalog.Pendant.Id]));
            Check(other.Options.All(o => o.Move.Kind != DuelMoveKind.Choose && o.CardId == null));
            Check(mine.Options.Single(o => o.Move.Kind == DuelMoveKind.Choose && o.CardId == CardCatalog.Destiny.Id).Move.Apply(g, 1).Success);
            Check(DuelSnapshot.For(g, 1, 5).Players[1].Hand.Single() == CardCatalog.Destiny.Id);
            Check(DuelSnapshot.For(g, 0, 5).Players[1].Hand.Single() == null && !g.State.Active.IsHandCardRevealed(0));
        }),
        ("Disquete: Error cancela antes de seleccionar; una respuesta tardía se rechaza sin mutación", () =>
        {
            foreach (var late in new[] { false, true })
            {
                var (g, rng) = Fixture(); g.State.Players[1].Memory.Add(CardCatalog.Bonfire.Id);
                if (!late) g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                Check(g.PlayCode(0, 0).Success);
                if (late) g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                if (late)
                {
                    Reject(g, () => g.PlayCode(1, g.State.Players[1].Hand.IndexOf(CardCatalog.LoadError.Id)));
                    Check(!g.CanPlayLoadError(1) && g.State.Phase == Phase.Actions);
                    Check(g.State.Active.ActionsLeft == 1 && g.State.Active.Hand.Single() == CardCatalog.Bonfire.Id);
                    continue;
                }
                Check(g.PlayCode(1, g.State.Players[1].Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
                Check(g.State.Phase == Phase.Actions && g.State.Active.ActionsLeft == 2 && g.State.Active.Hand.Count == 0);
                Check(g.State.Players[1].Memory.Contains(CardCatalog.Bonfire.Id) && g.State.Active.Memory.Single() == CardCatalog.FloppyDisk.Id);
            }
        }),
        ("Disquete: contraerror conserva la elección y sigue excluyendo la copia activa", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Memory.Add(CardCatalog.Bonfire.Id);
            g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id); Check(g.PlayCode(0, 0).Success);
            Check(g.PlayCode(1, 1).Success && g.PlayCode(0, 0).Success && g.State.Phase == Phase.Decision);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Reject(g, () => g.ResolveMemoryRecovery(0, 0, 0));
            Check(g.ResolveMemoryRecovery(0, 1, 0).Success && g.State.Active.Hand.Single() == CardCatalog.Bonfire.Id);
        }),
        ("Disquete: Caja puede cancelarlo o dejarlo resolver como código Objeto", () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var (g, _) = Fixture(); var rival = g.State.Players[1]; rival.Equipment = CardCatalog.CardboardBox.Id;
                rival.Memory.Add(CardCatalog.Destiny.Id);
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.CodeResponse && g.ResolveCodeResponse(1, cancel).Success);
                if (cancel) Check(g.State.Phase == Phase.Actions && g.State.Active.ActionsLeft == 2 && rival.Equipment == null && g.State.Active.Hand.Count == 0);
                else Check(g.State.Active.Hand.Single() == CardCatalog.Destiny.Id && g.State.Active.ActionsLeft == 1 && rival.Equipment == CardCatalog.CardboardBox.Id);
            }
        }),
        ("Disquete: Relikia corrompe la copia activada y recupera otra carta por 400 PR y dos acciones", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Relikia = 400; p.Memory.Add(CardCatalog.FloppyDisk.Id);
            Reject(g, () => g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0));
            p.Memory.Add(CardCatalog.FloppyDisk.Id);
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.CorruptedMemory.Single() == CardCatalog.FloppyDisk.Id);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.State.Phase == Phase.Actions && p.Hand.Single() == CardCatalog.FloppyDisk.Id && p.Memory.Count == 0);
            Check(p.ActionsLeft == 0 && p.Relikia == 0 && p.CorruptedMemory.Single() == CardCatalog.FloppyDisk.Id);
        }),
        ("Disquete: resuelve si queda una carta o ninguna tras Resetear la Mente", () =>
        {
            foreach (var corruptReset in new[] { false, true })
            {
                var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Add(CardCatalog.MindReset.Id);
                if (corruptReset) p.MarkForCorruption(CardZone.Hand, 1);
                p.Memory.AddRange([CardCatalog.Attack.Id, CardCatalog.Pendant.Id]);
                Check(g.PlayCode(0, 0).Success && g.DecisionChoices(0).Count == 2);
                Check(g.PlayCode(0, 0).Success && g.State.PendingDecision?.Step == "memory");
                Check(g.ChooseMindResetMemory(0, 0, 1).Success && g.ChooseMindResetMemory(0, 0, corruptReset ? 0 : 3).Success);
                Check(g.State.Phase == Phase.Actions && g.State.PendingDecision == null);
                Check(corruptReset ? p.Hand.Count == 0 : p.Hand.Single() == CardCatalog.Attack.Id);
                Check(p.Memory.Single() == CardCatalog.FloppyDisk.Id);
            }
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
            Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
            Decks = [Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList(), Enumerable.Repeat(CardCatalog.Attack.Id, 20).ToList()],
            Bonuses = [null, null]
        }, rng);
        g.State.Phase = Phase.Actions; g.State.Active.Hand[0] = CardCatalog.FloppyDisk.Id;
        foreach (var player in g.State.Players) player.Dexterity = 0;
        return (g, rng);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count);
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Disquete incorrecta."); }
}
