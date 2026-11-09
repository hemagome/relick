using Relick.Core;

static class TimeBenderTests
{
    public static (string Name, Action Run)[] Cases => new (string, Action)[]
    {
        ("Doblador: Objeto COMBO, dos descartes, una acción y una copia en el mazo de 42 cartas", () =>
        {
            var c = CardCatalog.TimeBender;
            Check(c.Kind == CardKind.Code && c.CodeType == CodeType.Object && c.IsCombo && c.DiscardCost == 2 && c.ActionCost == 1 && c.PrCost == 0);
            Check(CardCatalog.TrainingDeck().Count == 42 && CardCatalog.TrainingDeck().Count(id => id == c.Id) == 1);
        }),
        ("Doblador: repite Combate al salir, renueva aprendizaje y ataque pero conserva recursos del turno", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, rng) = Fixture(actor); var p = g.State.Active;
                g.State.AttackedThisTurn = true; g.State.LearnedThisTurn = true; p.AttackRerollUsedThisTurn = true; p.StarEffects = 1;
                p.Skills.Add(CardCatalog.Destiny.Id); p.Hand.Add(CardCatalog.Magnet.Id); p.Relikia = 300; var deck = p.Deck.Count;
                Cast(g, actor); Check(g.State.AttackedThisTurn && g.State.LearnedThisTurn && p.ActionsLeft == 1);
                Reject(g, () => g.Attack(actor, 0, 0));
                g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.Advance(actor).Success && g.State.Phase == Phase.Combat && !g.State.AttackedThisTurn && !g.State.LearnedThisTurn);
                Check(g.State.Turn == 1 && g.State.CurrentPlayer == actor && p.ActionsLeft == 1 && p.AttackRerollUsedThisTurn && p.StarEffects == 1 && p.Deck.Count == deck);
                Check(g.Learn(actor, p.Hand.IndexOf(CardCatalog.Magnet.Id)).Success);
                rng.EnqueueDie(5); Check(g.Attack(actor, p.Hand.IndexOf(CardCatalog.Attack.Id), 0).Success && g.State.Phase == Phase.Combat);
                Check(!g.CanUseYunoOnPendingDie(actor) && g.State.Players[1 - actor].Health == 2);
            }
        }),
        ("Doblador: la etapa de Combate repetida renueva a Jessica sin volver a enseñarla", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Bonus = CardCatalog.Jessica.Id; p.BonusPlacedTurn = 1;
            rng.EnqueueDie(1); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && p.BonusEffectUsed);
            Cast(g, 0); Check(p.BonusEffectUsed && g.Advance(0).Success && !p.BonusEffectUsed);
            rng.EnqueueDie(5); Check(g.UseFieldCode(0, FieldZone.Bonus).Success && g.State.Players[1].Health == 6 && p.BonusPlacedTurn == 1);
        }),
        ("Doblador: repetir Acciones concede dos nuevas solo después de terminar las actuales", () =>
        {
            var (g, _) = Fixture(stage: Phase.Actions); var p = g.State.Active; Cast(g, 0);
            Check(g.State.Phase == Phase.Actions && p.ActionsLeft == 1 && g.State.ExtraActionStages == 1);
            p.Hand.Add(CardCatalog.Senshuni.Id); Check(g.PlayCode(0, 1).Success && p.ActionsLeft == 0);
            Check(g.Advance(0).Success && g.State.Phase == Phase.Actions && p.ActionsLeft == 2 && g.State.Turn == 1);
            Check(g.Advance(0).Success && g.State.CurrentPlayer == 1 && g.State.ExtraActionStages == 0);
        }),
        ("Doblador: varias copias añaden varias etapas sin pasar turno ni recuperar acciones de Combate", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active;
            p.Hand.AddRange([CardCatalog.TimeBender.Id, CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id]);
            Cast(g, 0); Cast(g, 0); Check(g.State.ExtraCombatStages == 2 && p.ActionsLeft == 0);
            Check(g.Advance(0).Success && g.State.ExtraCombatStages == 1 && g.State.Phase == Phase.Combat);
            Check(g.Advance(0).Success && g.State.ExtraCombatStages == 0 && g.State.Phase == Phase.Combat);
            Check(g.Advance(0).Success && g.State.Phase == Phase.Actions && p.ActionsLeft == 0 && g.State.Turn == 1);
        }),
        ("Doblador: la ventana de robo inicial permite repetir un robo sin volver a iniciar el turno", () =>
        {
            foreach (var actor in new[] { 0, 1 })
            {
                var (g, _) = Fixture(actor, Phase.Draw); var p = g.State.Active; var draws = 0; g.CardDrawn += (_, _) => draws++;
                Check(g.HasAvailablePlay(actor)); Cast(g, actor); Check(g.State.ExtraDrawStages == 1 && draws == 0);
                Check(g.Advance(actor).Success && g.State.Phase == Phase.Draw && draws == 1 && p.Hand.Count == 2 && p.ActionsLeft == 1);
                Check(g.State.Turn == 1 && g.State.CurrentPlayer == actor && g.Advance(actor).Success && g.State.Phase == Phase.Combat);
            }
        }),
        ("Doblador: aparece tras el robo normal y la repetición roba tres si el coste deja la mano vacía", () =>
        {
            var (g, rng) = Fixture(); g.State.Active.Hand.Clear(); var next = g.State.Players[1];
            next.Hand.Clear(); next.Deck.Clear(); next.Deck.AddRange([CardCatalog.TimeBender.Id, CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id]);
            next.Deck.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 20));
            Check(g.Advance(0).Success && g.Advance(0).Success && g.State.Phase == Phase.Draw && g.State.CurrentPlayer == 1);
            Cast(g, 1); Check(next.Hand.Count == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.Advance(1).Success && g.State.Players[1].Hand.Count == 3 && g.State.Phase == Phase.Draw && g.State.Turn == 2);
        }),
        ("Doblador: la etapa Final repetida espera confirmación sin terminar prematuramente Estrella", () =>
        {
            var (g, rng) = Fixture(stage: Phase.Final); var p = g.State.Active; p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 5)); p.StarEffects = 1;
            Cast(g, 0); Check(g.State.Phase == Phase.Final && g.State.RepeatedFinalAwaitingAdvance && p.StarEffects == 1 && g.State.CurrentPlayer == 0);
            g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
            Check(DuelOptions.For(g, 0).Any(o => o.Move.Kind == DuelMoveKind.Advance) && DuelOptions.For(g, 0).All(o => o.Move.Kind != DuelMoveKind.Discard));
            Check(g.Advance(0).Success && g.State.CurrentPlayer == 1 && p.StarEffects == 0 && !g.State.RepeatedFinalAwaitingAdvance);
        }),
        ("Doblador: Final respeta el límite de siete antes de empezar su repetición", () =>
        {
            var (g, _) = Fixture(stage: Phase.Final); var p = g.State.Active; p.Hand.AddRange(Enumerable.Repeat(CardCatalog.Attack.Id, 9));
            Cast(g, 0); Check(p.Hand.Count == 10 && g.State.ExtraFinalStages == 1 && !g.State.RepeatedFinalAwaitingAdvance);
            Reject(g, () => g.Advance(0)); while (p.Hand.Count > 7) Check(g.Discard(0, 0).Success);
            Check(g.State.RepeatedFinalAwaitingAdvance && g.State.CurrentPlayer == 0 && g.Advance(0).Success && g.State.CurrentPlayer == 1);
        }),
        ("Doblador: dos Miras descartadas resuelven sus efectos sin duplicar etapas ni perder cartas", () =>
        {
            foreach (var memory in new[] { false, true })
            {
                var (g, rng) = Fixture(stage: memory ? Phase.Actions : Phase.Combat); var p = g.State.Active;
                p.Hand[1] = CardCatalog.Sniper.Id; p.Hand[2] = CardCatalog.Sniper.Id; var count = Count(g);
                if (memory) { p.Hand.RemoveAt(0); p.Memory.Add(CardCatalog.TimeBender.Id); p.Relikia = 400; Check(g.UseRelikiaEffect(0, RelikiaEffect.ReplayCode, 0).Success); }
                else Check(g.PlayCode(0, 0).Success);
                Check(g.ResolveTurnBuffDiscard(0, 0).Success && g.ResolveTurnBuffDiscard(0, 0).Success && g.State.Phase == Phase.SniperChoice);
                Check(g.ResolveSniper(0, true).Success); g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng); p = g.State.Active;
                Check(g.ResolveSniper(0, true).Success && p.Equipment == CardCatalog.Sniper.Id && g.State.PendingTurnBuff == null && Count(g) == count);
                Check(memory ? g.State.ExtraActionStages == 1 && p.ActionsLeft == 0 && p.CorruptedMemory.Single() == CardCatalog.TimeBender.Id :
                    g.State.ExtraCombatStages == 1 && p.ActionsLeft == 1);
            }
        }),
        ("Doblador: puede programar Combate durante un ataque pendiente y conserva su dado al reconectar", () =>
        {
            var (g, rng) = Fixture(); var p = g.State.Active; p.Skills.Add(CardCatalog.Destiny.Id); rng.EnqueueDie(5);
            Check(g.Attack(0, p.Hand.IndexOf(CardCatalog.Attack.Id), 0).Success && g.State.Phase == Phase.AttackRerollChoice);
            Cast(g, 0); g = GameEngine.FromSnapshot(g.SaveSnapshot(), rng);
            Check(g.State.PendingAttackRoll?.Die == 5 && g.State.ExtraCombatStages == 1 && g.State.Phase == Phase.AttackRerollChoice);
            Check(g.ResolveAttackReroll(0, false).Success && g.Advance(0).Success && !g.State.AttackedThisTurn && g.State.Phase == Phase.Combat);
        }),
        ("Doblador: valida dos cartas adicionales, acciones, turno propio y decisiones incompatibles", () =>
        {
            var (g, _) = Fixture(); var p = g.State.Active; p.Hand.RemoveRange(2, 2); Reject(g, () => g.PlayCode(0, 0));
            p.Hand.Add(CardCatalog.ExtraLife.Id); p.ActionsLeft = 0; Reject(g, () => g.PlayCode(0, 0)); p.ActionsLeft = 2;
            g.State.CurrentPlayer = 1; Reject(g, () => g.PlayCode(0, 0)); g.State.CurrentPlayer = 0;
            Check(g.PlayCode(0, 0).Success); Reject(g, () => g.ResolveTurnBuffDiscard(1, 0)); Reject(g, () => g.ResolveTurnBuffDiscard(0, 99));
            Check(g.ResolveTurnBuffDiscard(0, 0).Success); Reject(g, () => g.Advance(0));
        })
    };

    static (GameEngine, ScriptedRandom) Fixture(int actor = 0, Phase stage = Phase.Combat)
    {
        var rng = new ScriptedRandom(); var hands = new List<string>[] { [CardCatalog.Attack.Id], [CardCatalog.Attack.Id] };
        hands[actor] = [CardCatalog.TimeBender.Id, CardCatalog.Senshuni.Id, CardCatalog.ExtraLife.Id, CardCatalog.Attack.Id];
        var g = GameEngine.FromOpening(new DuelOpening
        {
            Names = ["Jugador 1", "Jugador 2"], StartingSeat = actor, Hands = hands,
            Decks = [Enumerable.Repeat("attack", 30).ToList(), Enumerable.Repeat("attack", 30).ToList()], Bonuses = [null, null]
        }, rng);
        foreach (var player in g.State.Players) player.Dexterity = 0;
        Check(stage != Phase.Draw || g.State.Phase == Phase.Draw); g.State.Phase = stage;
        return (g, rng);
    }
    static void Cast(GameEngine g, int actor)
    {
        var p = g.State.Players[actor]; Check(g.PlayCode(actor, p.Hand.IndexOf(CardCatalog.TimeBender.Id)).Success);
        Check(g.ResolveTurnBuffDiscard(actor, p.Hand.IndexOf(CardCatalog.Senshuni.Id)).Success);
        Check(g.ResolveTurnBuffDiscard(actor, p.Hand.IndexOf(CardCatalog.ExtraLife.Id)).Success);
    }
    static int Count(GameEngine g) => g.State.Players.Sum(p => p.Hand.Count + p.Deck.Count + p.Memory.Count + p.CorruptedMemory.Count + (p.Equipment == null ? 0 : p.EquipmentStackedCopies + 1) + (p.EquipmentCovers?.Count ?? 0));
    static void Reject(GameEngine g, Func<CommandResult> action)
    {
        var before = g.SaveSnapshot(); Check(!action().Success && before == g.SaveSnapshot());
    }
    static void Check(bool value) { if (!value) throw new Exception("Regla de Doblador de Tiempo incorrecta."); }
}
