using Relick.Core;

static class PlumberStarTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Estrella: Objeto COMBO, una acción, un descarte y una copia en 42 cartas", () =>
        {
            var c = CardCatalog.PlumberStar;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Object && c.IsCombo && c.ActionCost == 1 && c.PrCost == 0);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
            var (g, _) = Fixture(); var before = Count(g);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.TurnBuffDiscard && g.State.Active.ActionsLeft == 1);
            Check(g.State.Active.StarEffects == 0 && g.ResolveTurnBuffDiscard(0, 0).Success);
            Check(g.State.Phase == Phase.Combat && g.State.Active.StarEffects == 1 && Count(g) == before);
        }),
        ("Estrella: duplica mitad, normal y crítico antes del Casco, conservando el premio", () =>
        {
            foreach (var actor in new[] { 0, 1 }) foreach (var die in new[] { 4, 5, 6 }) foreach (var helmet in new[] { false, true })
            {
                var (g, rng) = Fixture(actor); Cast(g, actor);
                var target = g.State.Players[1 - actor]; if (helmet) target.Equipment = CardCatalog.DiamondHelmet.Id;
                var cues = DuelVisualEvents.Capture(g); rng.EnqueueDie(die);
                Check(g.Attack(actor, 0, 0).Success && g.ResolveAttackReroll(actor, false).Success);
                var amount = GameEngine.DamageFor(die, 3, 5) * 2 - (helmet ? 2 : 0);
                Check(target.Health == Math.Max(0, 8 - amount) && cues.Single(v => v.Kind == DuelVisualKind.Hit).Value == amount);
                Check(g.State.Active.Relikia == 600 && g.State.Active.StarEffects == 1);
            }
        }),
        ("Estrella: dos activaciones duplican dos veces y la Mira se suma antes de duplicar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active;
            p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id]); p.Equipment = CardCatalog.Sniper.Id;
            Cast(g, 0); Cast(g, 0); Check(p.StarEffects == 2 && p.ActionsLeft == 0);
            var damage = 0; g.DamageReceived += (_, amount, _) => damage = amount; rng.EnqueueDie(5);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && damage == 16);
        }),
        ("Estrella: abre defensa sin otras defensas, protege incluso en cero y vence al acabar el turno rival", () =>
        {
            var (g, rng) = Fixture(); var defender = g.State.Players[1];
            defender.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id]); defender.Health = 0; defender.HasReachedZero = true;
            g.State.Active.Hand.Clear(); g.State.Active.Hand.Add(CardCatalog.Attack.Id); rng.EnqueueDie(5);
            Check(g.Attack(0, 0, 0).Success && g.ResolveAttackReroll(0, false).Success && g.State.Phase == Phase.DefenseChoice);
            Cast(g, 1); var cues = DuelVisualEvents.Capture(g);
            Check(g.State.Phase == Phase.DefenseChoice && g.PassDefense(1).Success && !g.State.IsOver && defender.Health == 0);
            Check(cues.All(v => v.Kind != DuelVisualKind.Hit) && g.State.Active.Relikia == 600 && defender.StarEffects == 1);
            Check(g.Advance(0).Success && g.Advance(0).Success && g.State.CurrentPlayer == 1 && defender.StarEffects == 0);
        }),
        ("Estrella: permanece durante los descartes finales y desaparece para ambos al pasar turno", () =>
        {
            var (g, _) = Fixture(); Cast(g, 0); var p = g.State.Active;
            p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 8)); g.State.Players[1].StarEffects = 1;
            Check(g.Advance(0).Success && g.Advance(0).Success && g.State.Phase == Phase.Final && p.StarEffects == 1);
            Check(g.Discard(0, 0).Success && g.State.Phase == Phase.Final && p.StarEffects == 1);
            Check(g.Discard(0, 0).Success && g.State.CurrentPlayer == 1 && g.State.Players.All(player => player.StarEffects == 0));
        }),
        ("Estrella: Error impide el descarte, devuelve la acción y conserva el dado pendiente al reconectar", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active; rng.EnqueueDie(5);
                Check(g.Attack(actor, 2, 0).Success && g.State.Phase == Phase.AttackRerollChoice);
                g.State.Players[1 - actor].Hand.Add(CardCatalog.LoadError.Id);
                Check(g.PlayCode(actor, 0).Success && g.State.Phase == Phase.ErrorResponse);
                Check(p.Hand.Single() == CardCatalog.Senshuni.Id && !p.Memory.Contains(CardCatalog.Senshuni.Id));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.PlayCode(1 - actor, g.State.Players[1 - actor].Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
                Check(g.State.Phase == Phase.AttackRerollChoice && g.State.PendingAttackRoll?.Die == 5 && p.StarEffects == 0 && p.ActionsLeft == 2);
                Check(p.Hand.Single() == CardCatalog.Senshuni.Id && p.Memory.Contains(CardCatalog.PlumberStar.Id));
                Check(g.ResolveAttackReroll(actor, false).Success && g.State.Players[1 - actor].Health == 5);
            }
        }),
        ("Estrella: Caja conserva el descarte en memoria al cancelar; dejar resolver activa los efectos", () =>
        {
            foreach (var cancel in new[] { false, true })
            {
                var (g, _) = Fixture(); var p = g.State.Active; g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
                Cast(g, 0); Check(g.State.Phase == Phase.CodeResponse && p.StarEffects == 0);
                Check(g.ResolveCodeResponse(1, cancel).Success && g.State.Phase == Phase.Combat);
                Check(p.Memory.Contains(CardCatalog.Senshuni.Id) && !p.Hand.Contains(CardCatalog.Senshuni.Id));
                Check(p.StarEffects == (cancel ? 0 : 1) && p.ActionsLeft == (cancel ? 2 : 1));
            }
        }),
        ("Estrella: contraerror permite pagar el descarte después de las respuestas y activa una sola vez", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.Add(CardCatalog.LoadError.Id); g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.ErrorResponse);
            Check(g.PlayCode(1, 1).Success && g.PlayCode(0, p.Hand.IndexOf(CardCatalog.LoadError.Id)).Success);
            Check(g.State.Phase == Phase.TurnBuffDiscard && g.ResolveTurnBuffDiscard(0, 0).Success);
            Check(p.StarEffects == 1 && p.Memory.Contains(CardCatalog.Senshuni.Id) && p.ActionsLeft == 0 && g.State.PendingTurnBuff == null);
        }),
        ("Estrella: Relikia exige descarte, cobra 400 PR y corrompe solo su copia", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions;
            p.Hand.Clear(); p.Relikia = 400; p.Memory.Add(CardCatalog.PlumberStar.Id);
            Reject(g, () => g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0)); p.Hand.Add(CardCatalog.Senshuni.Id);
            Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success && p.ActionsLeft == 0 && p.Relikia == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.ResolveTurnBuffDiscard(0, 0).Success && p.StarEffects == 1 && p.CorruptedMemory.Single() == CardCatalog.PlumberStar.Id);
            Check(p.Memory.Single() == CardCatalog.Senshuni.Id && g.State.Phase == Phase.Actions);
        }),
        ("Estrella: Mira descartada dispara al resolver o con Caja; Error evita el descarte y su disparo", () =>
        {
            foreach (var mode in new[] { "normal", "box", "error", "relikia" })
            {
                var (g, rng) = Fixture(); var p = g.State.Active;
                p.Hand[1] = CardCatalog.Sniper.Id;
                if (mode == "box") g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
                if (mode == "error") g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
                if (mode == "relikia")
                {
                    g.State.Phase = Phase.Actions; p.Hand.RemoveAt(0); p.Memory.Add(CardCatalog.PlumberStar.Id); p.Relikia = 400;
                    Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success);
                }
                else Check(g.PlayCode(0, 0).Success);
                if (mode == "error")
                {
                    Check(g.PlayCode(1, 1).Success && p.Hand.Contains(CardCatalog.Sniper.Id) && p.StarEffects == 0 && g.State.PendingSniper == null);
                    continue;
                }
                Check(g.ResolveTurnBuffDiscard(0, 0).Success);
                if (mode == "box") Check(g.ResolveCodeResponse(1, true).Success);
                Check(g.State.Phase == Phase.SniperChoice);
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.ResolveSniper(0, true).Success && p.Equipment == CardCatalog.Sniper.Id && g.State.PendingTurnBuff == null);
                Check(p.StarEffects == (mode == "box" ? 0 : 1) && g.State.Phase == (mode == "relikia" ? Phase.Actions : Phase.Combat));
            }
        }),
        ("Estrella: suspende y recupera el dado y la activación de Vida Extra", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; p.Health = 2;
            p.Hand.Insert(0, CardCatalog.ExtraLife.Id); rng.EnqueueDie(5);
            Check(g.PlayCode(0, 0).Success && g.State.Phase == Phase.DieRerollChoice);
            Cast(g, 0); g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.State.Phase == Phase.DieRerollChoice && g.State.PendingDieRoll?.Die == 5 && p.StarEffects == 1);
            Check(g.ResolveDieReroll(0, false).Success && p.Health == 5 && p.ActionsLeft == 0 && g.State.Phase == Phase.Actions);
        }),
        ("Estrella: cancelar el equipo gratuito de la Mira no devuelve su descarte ni bloquea el turno", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Hand[1] = CardCatalog.Sniper.Id;
            g.State.Players[1].Hand.Add(CardCatalog.LoadError.Id);
            Check(g.PlayCode(0, 0).Success && g.PassErrorResponse(1).Success && g.ResolveTurnBuffDiscard(0, 0).Success);
            Check(g.State.Phase == Phase.SniperChoice && g.ResolveSniper(0, true).Success && g.State.Phase == Phase.ErrorResponse);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(g.PlayCode(1, 1).Success && g.State.Phase == Phase.Combat && g.State.PendingTurnBuff == null);
            Check(p.StarEffects == 1 && p.Equipment == null && p.Memory.Contains(CardCatalog.Sniper.Id) && !p.Hand.Contains(CardCatalog.Sniper.Id));
        }),
        ("Estrella: conserva la respuesta original de Caja al interrumpirla con otro Objeto", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; g.State.Phase = Phase.Actions; p.Hand.Insert(0, CardCatalog.ExtraLife.Id);
            g.State.Players[1].Equipment = CardCatalog.CardboardBox.Id;
            Check(g.PlayCode(0, 0).Success && g.State.PendingCode?.CardId == CardCatalog.ExtraLife.Id);
            Cast(g, 0); Check(g.State.PendingCode?.CardId == CardCatalog.PlumberStar.Id);
            Check(g.ResolveCodeResponse(1, true).Success && g.State.PendingCode?.CardId == CardCatalog.ExtraLife.Id);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(DuelOptions.For(g, 1).Where(o => o.Move.Kind == DuelMoveKind.CodeResponse).All(o => !o.Move.Flag));
            rng.EnqueueDie(1); Check(g.ResolveCodeResponse(1, false).Success && g.State.Phase == Phase.Actions);
        }),
        ("Estrella: no protege de las dos vidas inevitables del segundo agotamiento", () =>
        {
            var (g, _) = Fixture(); Cast(g, 0); var p = g.State.Active;
            g.State.Phase = Phase.Actions; p.Deck.Clear(); p.Recycled = true; p.Hand.Clear(); p.Hand.Add(CardCatalog.Senshuni.Id);
            Check(g.PlayCode(0, 0).Success && g.State.IsOver && p.Health == 6);
        }),
        ("Estrella: valida mano, acciones, actor y coste antes de mutar", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.RemoveRange(1, 2); Reject(g, () => g.PlayCode(0, 0));
            p.Hand.Add(CardCatalog.Senshuni.Id); p.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); p.ActionsLeft = 2;
            Check(g.PlayCode(0, 0).Success); Reject(g, () => g.ResolveTurnBuffDiscard(1, 0)); Reject(g, () => g.ResolveTurnBuffDiscard(0, -1));
            Reject(g, () => g.ResolveTurnBuffDiscard(0, 99)); Reject(g, () => g.Advance(0));
            Check(g.ResolveTurnBuffDiscard(0, 0).Success); Reject(g, () => g.ResolveTurnBuffDiscard(0, 0));
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0)
    {
        var rng = new ScriptedRandom(); var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor,
            Hands = [[CardCatalog.Attack.Id], [CardCatalog.Attack.Id]],
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        var p = g.State.Active; p.Hand.Clear(); p.Hand.AddRange([CardCatalog.PlumberStar.Id, CardCatalog.Senshuni.Id, CardCatalog.Attack.Id]);
        p.Skills.Add(CardCatalog.Destiny.Id);
        foreach (var player in g.State.Players) player.Dexterity = 0;
        return (g, rng);
    }
    static void Cast(GameEngine g, int actor)
    {
        var p = g.State.Players[actor];
        Check(g.PlayCode(actor, p.Hand.IndexOf(CardCatalog.PlumberStar.Id)).Success);
        Check(g.ResolveTurnBuffDiscard(actor, p.Hand.FindIndex(id => id == CardCatalog.Senshuni.Id)).Success);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + p.Skills.Count);
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Estrella del Fontanero incorrecta."); }
}
