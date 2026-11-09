using Relick.Core;

static class ReloadCartridgeTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Cartucho: Jugada sin COMBO, turno mínimo dos y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.ReloadCartridge;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Play && !c.IsCombo && c.ActionCost == 1 && c.PrCost == 0 && c.EarliestTurn == 2);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, _) = Fixture(actor); g.State.Turn = 1; g.State.Active.Memory.Add(c.Id); g.State.Active.Relikia = 400;
                Reject(g, () => g.PlayCode(actor, 0)); Reject(g, () => g.UseRelikiaEffect(actor, RelikiaEffect.ReplayCode, 0));
                g.State.Turn = 2; Check(g.PlayCode(actor, 0).Success && g.State.Active.ActionsLeft == 1);
            }
        }),
        ("Cartucho: descarta ambas manos antes de los ocho robos, conserva cartas y cuesta una acción", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, _) = Fixture(actor); var p = g.State.Active; var other = g.State.Players[1 - actor];
                var count = Count(g); var order = new List<string>(); g.HandDiscarded += (_, _) => order.Add("discard");
                g.CardDrawn += (_, _) => order.Add("draw");
                Check(g.PlayCode(actor, 0).Success && g.State.Phase == Phase.Actions && g.State.PendingReload == null);
                Check(order.Take(2).SequenceEqual(new[] { "discard", "discard" }) && order.Count(s => s == "draw") == 8);
                Check(p.Memory.SequenceEqual(new[] { CardCatalog.ReloadCartridge.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id }));
                Check(other.Memory.SequenceEqual(new[] { CardCatalog.Jessica.Id, CardCatalog.Bonfire.Id }));
                Check(p.Hand.Count == 4 && other.Hand.Count == 4 && p.ActionsLeft == 1 && p.Relikia == 0 && Count(g) == count);
                Check(g.State.Players.All(player => player.Health == 8 && player.Deck.Count == 26));
            }
        }),
        ("Cartucho: exige una carta restante en ambas manos y valida etapa y acciones sin mutar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.RemoveRange(1, 2); Reject(g, () => g.PlayCode(0, 0));
            p.Hand.Add(CardCatalog.Attack.Id); g.State.Players[1].Hand.Clear(); Reject(g, () => g.PlayCode(0, 0));
            g.State.Players[1].Hand.Add(CardCatalog.Attack.Id); g.State.Phase = Phase.Combat; Reject(g, () => g.PlayCode(0, 0));
            g.State.Phase = Phase.Actions; p.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); p.ActionsLeft = 2;
            Reject(g, () => g.PlayCode(1, 0)); Reject(g, () => g.PlayCode(0, 99));
        }),
        ("Cartucho: Error cancela antes de descartar y devuelve la acción sin robar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var other = g.State.Players[1]; other.Hand.Add(CardCatalog.LoadError.Id);
            var count = Count(g); var cues = DuelVisualEvents.Capture(g);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && p.Hand.Count == 2 && other.Hand.Count == 3);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; other = g.State.Players[1];
            Check(g.PlayCode(1, 2).Success && p.Hand.Count == 2 && other.Hand.Count == 2 && p.ActionsLeft == 2 && Count(g) == count);
            Check(g.State.Phase == Phase.Actions && g.State.PendingReload == null && g.State.Players.All(player => player.Deck.Count == 30));
            Check(cues.All(v => v.Kind != DuelVisualKind.HandDiscard && v.Kind != DuelVisualKind.Draw));
        }),
        ("Cartucho: el mínimo se comprueba al activar; las respuestas pueden consumir las últimas cartas", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; var other = g.State.Players[1];
            p.Hand.Clear(); p.Hand.AddRange([CardCatalog.ReloadCartridge.Id, CardCatalog.LoadError.Id]);
            other.Hand.Clear(); other.Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.PlayCode(1, 0).Success && g.PlayCode(0, 0).Success);
            Check(p.Hand.Count == 4 && other.Hand.Count == 4 && p.ActionsLeft == 0 && other.ActionsLeft == 2 && g.State.PendingReload == null);
        }),
        ("Cartucho: Caja no responde a la Jugada y conserva su equipo", () =>
        {
            var (g, _) = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && g.State.Players[1].Equipment == CardCatalog.CardboardBox.Id);
            Check(g.State.Players.All(player => player.Hand.Count == 4));
        }),
        ("Cartucho: Mira de ambos jugadores se resuelve antes de robar y se recupera cada decisión", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); g.State.Active.Hand[1] = CardCatalog.Sniper.Id; g.State.Players[1 - actor].Hand[0] = CardCatalog.Sniper.Id;
                var count = Count(g);
                Check(g.PlayCode(actor, 0).Success && g.State.Phase == Phase.SniperChoice && g.State.DecisionPlayer == actor);
                Check(g.State.Players.All(player => player.Hand.Count == 0 && player.Deck.Count == 30));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.ResolveSniper(actor, true).Success && g.State.Phase == Phase.SniperChoice && g.State.DecisionPlayer == 1 - actor);
                Check(g.State.Players.All(player => player.Hand.Count == 0));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.ResolveSniper(1 - actor, true).Success && g.State.Phase == Phase.Actions && g.State.PendingReload == null);
                Check(g.State.Players.All(player => player.Equipment == CardCatalog.Sniper.Id && player.Hand.Count == 4) && Count(g) == count);
            }
        }),
        ("Cartucho: varias Miras y canjes no descartan el Bonus obtenido durante sus disparadores", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var other = g.State.Players[1]; p.Hand[1] = CardCatalog.Sniper.Id;
            p.Hand.Add(CardCatalog.Sniper.Id); other.Hand[0] = CardCatalog.Sniper.Id; p.Bonus = CardCatalog.Jessica.Id;
            var count = Count(g); Check(g.PlayCode(0, 0).Success && g.ResolveSniper(0, false).Success);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveSniper(0, false).Success && g.State.DecisionPlayer == 1 && p.Hand.Single() == CardCatalog.Jessica.Id);
            Check(g.ResolveSniper(1, false).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 5);
            Check(p.Hand.Contains(CardCatalog.Jessica.Id) && g.State.Players[1].Hand.Count == 4 && g.State.Players[1].Exchange.Single() == CardCatalog.Sniper.Id && Count(g) == count);
        }),
        ("Cartucho: Error puede cancelar el equipo gratuito rival sin deshacer descartes ni bloquear los robos", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; var other = g.State.Players[1];
            p.Hand[1] = CardCatalog.Sniper.Id; other.Hand[0] = CardCatalog.Sniper.Id;
            p.Exchange.Add(CardCatalog.Attack.Id); p.Bonus = CardCatalog.LoadError.Id;
            Check(g.PlayCode(0, 0).Success && g.ResolveSniper(0, false).Success && p.Hand.Single() == CardCatalog.LoadError.Id);
            Check(g.ResolveSniper(1, true).Success && g.State.Phase == Phase.ErrorResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; other = g.State.Players[1];
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && g.State.PendingReload == null);
            Check(p.Hand.Count == 4 && other.Hand.Count == 4 && other.Equipment == null && other.Memory.Contains(CardCatalog.Sniper.Id));
        }),
        ("Cartucho: reciclar durante el robo de cualquiera conserva el progreso sin repetir descartes", () =>
        {
            foreach (var deplete in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(); var p = g.State.Players[deplete]; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
                p.Memory.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 5)); var count = Count(g); rng.EnqueueDie(6);
                Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.RecycleChoice && g.State.DecisionPlayer == deplete);
                Check(p.Hand.Count == 1 && g.State.Players[1 - deplete].Hand.Count == (deplete == 0 ? 0 : 4));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
                Check(g.ResolveRecycling(deplete, [0, 1, 2, 3, 4, 5]).Success && g.State.Phase == Phase.Actions && g.State.PendingReload == null);
                Check(g.State.Players.All(player => player.Hand.Count == 4) && Count(g) == count);
            }
        }),
        ("Cartucho: un segundo agotamiento termina antes de los robos restantes", () =>
        {
            foreach (var deplete in new[] { 0, 1 })
            {
                var (g, _) = Fixture(); var p = g.State.Players[deplete]; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id); p.Recycled = true;
                Check(g.PlayCode(0, 0).Success && g.State.IsOver && p.Hand.Count == 1 && p.Health == 6);
                Check(g.State.Players[1 - deplete].Hand.Count == (deplete == 0 ? 0 : 4));
                Check(DuelSnapshot.For(g, 0, 1).Stage == Phase.Finished);
            }
        }),
        ("Cartucho: Relikia exige ambas manos y corrompe su copia por dos acciones y 400 PR", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(CardCatalog.ReloadCartridge.Id); p.Relikia = 400;
            Reject(g, () => g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0)); p.Hand.Add(CardCatalog.Attack.Id);
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.CorruptedMemory.Single() == CardCatalog.ReloadCartridge.Id);
            Check(p.ActionsLeft == 0 && p.Relikia == 0 && g.State.Players.All(player => player.Hand.Count == 4));
        }),
        ("Cartucho: conserva memoria pública y vuelve a ocultar las manos nuevas", () =>
        {
            var (g, _) = Fixture(); foreach (var p in g.State.Players) Check(p.RevealHandCard(p.Hand.Count - 1));
            var cues = DuelVisualEvents.Capture(g); Check(g.PlayCode(0, 0).Success);
            Check(cues.Count(v => v.Kind == DuelVisualKind.HandDiscard) == 2 && cues.Count(v => v.Kind == DuelVisualKind.Draw) == 8);
            Check(cues.Where(v => v.Kind == DuelVisualKind.HandDiscard).All(v => v.Value == 2));
            foreach (var seat in new[] { 0, 1 })
            {
                var view = DuelSnapshot.For(g, seat, 1);
                Check(view.Players[seat].Hand.All(id => id != null) && view.Players[1 - seat].Hand.All(id => id == null));
                Check(g.State.Players[seat].RevealedHandSlots.Count == 0 && view.Players[1 - seat].Memory.Length >= 2);
            }
        }),
        ("Cartucho: Reverse puede conservar el equipo anterior sin detener la recarga", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand[1] = CardCatalog.Sniper.Id;
            p.Equipment = CardCatalog.Pendant.Id; p.Skills.Add(CardCatalog.Reverse.Id);
            Check(g.PlayCode(0, 0).Success && g.ResolveSniper(0, true).Success && g.State.Phase == Phase.DestructionResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDestructionProtection(0, 0).Success && g.State.Phase == Phase.Actions && g.State.PendingReload == null);
            Check(p.Equipment == CardCatalog.Pendant.Id && p.Memory.Contains(CardCatalog.Sniper.Id) && g.State.Players.All(player => player.Hand.Count == 4));
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        g.State.Phase = Phase.Actions; g.State.Turn = 2;
        g.State.Active.Hand.AddRange([CardCatalog.ReloadCartridge.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]);
        g.State.Players[1 - actor].Hand.AddRange([CardCatalog.Jessica.Id, CardCatalog.Bonfire.Id]);
        foreach (var p in g.State.Players) p.Dexterity = 0;
        return (g, rng);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Exchange.Count + p.Skills.Count + (p.Bonus == null ? 0 : 1) + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0));
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Cartucho de Re-carga incorrecta."); }
}
