using Relick.Core;

static class QuickLoadTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Carga Rápida: Jugada sin COMBO, una acción y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.QuickLoad;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Play && !c.IsCombo && c.ActionCost == 1 && c.PrCost == 0 && c.Draw == 2);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Carga Rápida: los seis resultados, robo inicial antes del dado y descarte elegido", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var die in Enumerable.Range(1, 6))
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active; var count = Count(g); var order = new List<string>();
                g.CardDrawn += (_, _) => order.Add("draw"); g.DieRolled += _ => order.Add("die"); rng.EnqueueDie(die);
                Check(g.PlayCode(actor, 0).Success && order.Take(3).SequenceEqual(new[] { "draw", "draw", "die" }));
                var draws = die < 5 ? 2 : die == 5 ? 3 : 4;
                Check(p.Hand.Count == draws && p.ActionsLeft == 1 && p.Relikia == 0 && p.Health == 8 && Count(g) == count);
                if (die <= 4)
                {
                    var discardCount = die <= 2 ? 2 : 1; Check(g.State.Phase == Phase.Discard && g.State.DiscardsRemaining == discardCount);
                    for (var i = 0; i < discardCount; i++) Check(g.Discard(actor, p.Hand.Count - 1).Success);
                    Check(p.Hand.Count == 2 - discardCount && p.Memory[1] == CardCatalog.Senshuni.Id);
                }
                Check(g.State.Phase == Phase.Actions && p.Memory.Contains(CardCatalog.QuickLoad.Id) && Count(g) == count);
                Check(order.Count(e => e == "draw") == draws && order.Count(e => e == "die") == 1);
            }
        }),
        ("Carga Rápida: Error inicial devuelve costes sin robar ni lanzar; Caja no responde", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; var deck = p.Deck.ToArray(); var dice = 0;
            g.DieRolled += _ => dice++; g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse && p.Hand.Count == 0);
            Check(g.PlayCode(1, 0).Success && p.ActionsLeft == 2 && p.Hand.Count == 0 && p.Deck.SequenceEqual(deck) && dice == 0);
            (g, _) = Fixture(); g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.Phase != Phase.CodeResponse && g.State.Players[1].Equipment == CardCatalog.CardboardBox.Id);
        }),
        ("Carga Rápida: Error responde tras el dado final, no en la ventana del dueño; anularla no revierte los robos ni con Relikia se abre", () =>
        {
            foreach (var memory in new[] { false, true })
            foreach (var cancel in new[] { false, true })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
                if (memory) { p.Hand.Clear(); p.Memory.Add(CardCatalog.QuickLoad.Id); p.Relikia = 400; }
                var top = p.Deck.Take(2).ToArray(); rng.EnqueueDie(2);
                Check((memory ? g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0) : g.PlayCode(0, 0)).Success);
                Check(g.State.Phase == Phase.DieRerollChoice && p.Hand.SequenceEqual(top));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active; g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                // While the owner may still modify the die, the result is not final: no Error yet.
                Reject(g, () => g.PlayCode(1, 0));
                Check(!g.CanPlayLoadError(1) && g.State.Phase == Phase.DieRerollChoice && p.Hand.SequenceEqual(top));
                Check(p.ActionsLeft == (memory ? 0 : 1) && p.Relikia == 0);
                Check(memory ? p.CorruptedMemory.Single() == CardCatalog.QuickLoad.Id : p.Memory.Contains(CardCatalog.QuickLoad.Id));
                Check(g.ResolveDieReroll(0, false).Success);
                if (memory) { Check(g.State.DiscardsRemaining == 2 && !g.State.ErrorAfterDie); continue; }
                Check(g.State.Phase == Phase.ErrorResponse && g.State.ErrorAfterDie && g.CanPlayLoadError(1) && g.State.PendingDieRoll?.Die == 2);
                Check(DuelSnapshot.For(g, 1, 1) is { ErrorAfterDie: true, Die: 2 } && DuelSnapshot.For(g, 0, 1).ErrorAfterDie);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                if (!cancel) { Check(g.PassErrorResponse(1).Success && g.State.DiscardsRemaining == 2 && !g.State.ErrorAfterDie); continue; }
                Check(g.PlayCode(1, 0).Success && g.State.Phase == Phase.Actions && g.State.DiscardsRemaining == 0 && g.State.PendingDieRoll == null);
                // The text is void from here: the action comes back, the cards already drawn stay.
                Check(p.ActionsLeft == 2 && p.Hand.SequenceEqual(top) && p.Memory.Contains(CardCatalog.QuickLoad.Id) && !g.State.ErrorAfterDie);
            }
        }),
        ("Carga Rápida: Yuno Lv 2 relanza el efecto y Reverse aumenta una vez sin repetir el robo inicial", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Relikario = RelikarioCatalog.YunoLv2; p.Skills.Add(CardCatalog.Reverse.Id);
            rng.EnqueueDie(2); Check(g.PlayCode(0, 0).Success && p.Hand.Count == 2 && g.State.PendingDieRoll?.Die == 2);
            rng.EnqueueDie(6); Check(g.ResolveDieReroll(0, true).Success && p.Hand.Count == 2 && g.State.PendingDieRoll?.Die == 6);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveDieReroll(0, false).Success && p.Hand.Count == 4 && p.CountersOnSkill(0) == 1 && !p.HasNaturalSix && g.State.Phase == Phase.Actions);
        }),
        ("Carga Rápida: una Agilidad recién robada modifica el dado y evita el descarte sin relanzar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Deck[0] = CardCatalog.Agility.Id; var dice = 0;
            g.DieRolled += _ => dice++; rng.EnqueueDie(1);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice && p.Hand[0] == CardCatalog.Agility.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 2 && p.ActionsLeft == 0);
            Check(dice == 1 && g.State.DiscardsRemaining == 0 && p.Memory.Contains(CardCatalog.Agility.Id));
        }),
        ("Carga Rápida: los descartes y su cantidad sobreviven a reconectar y disparan Mira", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Deck[0] = CardCatalog.Sniper.Id; rng.EnqueueDie(1);
            Check(g.PlayCode(0, 0).Success && g.Discard(0, 0).Success && g.State.Phase == Phase.SniperChoice && g.State.DiscardsRemaining == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveSniper(0, true).Success && g.State.Phase == Phase.Discard && p.Equipment == CardCatalog.Sniper.Id);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.Discard(0, 0).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 0 && p.ActionsLeft == 1);
        }),
        ("Carga Rápida: completa el reciclaje y ambos robos iniciales antes de su propio dado", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Deck.Clear(); p.Deck.Add(CardCatalog.Attack.Id);
            p.Memory.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 8)); rng.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.RecycleChoice && p.Hand.Count == 1);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            g.CardsReturnedToDeck += _ => rng.EnqueueDie(6);
            Check(g.ResolveRecycling(0, [0, 1, 2, 3, 4, 5]).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 4 && p.Deck.Count == 3);
        }),
        ("Carga Rápida: el segundo agotamiento interrumpe el robo inicial o adicional sin pasos extra", () =>
        {
            foreach (var initialCards in new[] { 1, 3 })
            {
                var (g, rng) = Fixture(); var p = g.State.Active; p.Deck.Clear(); p.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, initialCards));
                p.Recycled = true; var dice = 0; g.DieRolled += _ => dice++; rng.EnqueueDie(6);
                Check(g.PlayCode(0, 0).Success && g.State.IsOver && p.Health == 6 && p.Hand.Count == initialCards);
                Check(dice == (initialCards == 1 ? 0 : 1));
            }
        }),
        ("Carga Rápida: Relikia corrompe su copia antes de robar y conserva el descarte obligatorio", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.Clear(); p.Memory.Add(CardCatalog.QuickLoad.Id); p.Relikia = 400;
            rng.EnqueueDie(2); Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && g.State.Phase == Phase.Discard);
            Check(p.CorruptedMemory.Single() == CardCatalog.QuickLoad.Id && p.Hand.Count == 2 && p.ActionsLeft == 0 && p.Relikia == 0);
            Check(g.Discard(0, 1).Success && g.Discard(0, 0).Success && g.State.Phase == Phase.Actions && p.Hand.Count == 0);
        }),
        ("Carga Rápida: el límite de siete se comprueba al terminar, después de resolver sus robos", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 10)); rng.EnqueueDie(6);
            Check(g.PlayCode(0, 0).Success && p.Hand.Count == 14 && g.State.Phase == Phase.Actions);
            Check(g.Advance(0).Success && g.State.Phase == Phase.Final);
            for (var i = 0; i < 7; i++) Check(g.Discard(0, 0).Success);
            Check(p.Hand.Count == 7 && g.State.CurrentPlayer == 1);
        }),
        ("Carga Rápida: rechazos no consumen cartas, costes ni azar", () =>
        {
            var (g, _) = Fixture(); var dice = 0; g.DieRolled += _ => dice++;
            g.State.Phase = Phase.Combat; Reject(g, () => g.PlayCode(0, 0)); g.State.Phase = Phase.Actions;
            Reject(g, () => g.PlayCode(1, 0)); Reject(g, () => g.PlayCode(0, -1)); Reject(g, () => g.PlayCode(0, 99));
            g.State.Active.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); Check(dice == 0);
        }),
        ("Carga Rápida: ajusta el descarte a las cartas disponibles tras Resetear la Mente", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Equipment = CardCatalog.Pendant.Id;
            p.Memory.Add(CardCatalog.Attack.Id); p.Deck[0] = CardCatalog.MindReset.Id; rng.EnqueueDie(2);
            Check(g.PlayCode(0, 0).Success && p.Hand.Count == 2 && g.PlayCode(0, 0).Success);
            Check(g.State.PendingDecision is { Step: "memory", ShowsHandOf: 0 } && g.ChooseMindResetMemory(0, 0, 0).Success && g.ChooseMindResetMemory(0, 0, 2).Success);
            Check(p.Hand.Count == 1 && g.State.PendingDieRoll?.Die == 2);
            Check(g.ResolveDieReroll(0, false).Success && g.State.Phase == Phase.Discard && g.State.DiscardsRemaining == 1);
            Check(g.Discard(0, 0).Success && p.Hand.Count == 0 && g.State.Phase == Phase.Actions);
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = [[], []],
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        foreach (var player in g.State.Players) player.Dexterity = 0;
        g.State.Phase = Phase.Actions; g.State.Active.Hand.Add(CardCatalog.QuickLoad.Id); g.State.Active.Deck[1] = CardCatalog.Senshuni.Id;
        return (g, rng);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0));
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Carga Rápida incorrecta."); }
}
